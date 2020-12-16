using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace CP77_depack_save
{
    class Savegame
    {
        public string MetadataFilePath { get; set; }
        public string DataFilePath { 
            get
            {
                return dataFilePath;
            }
            set
            {
                if(Path.GetFileName(value) != "sav.dat")
                {
                    throw new FormatException("Filepath doesn't lead to a sav.dat");
                }
                dataFilePath = value;
            }
        }

        
        public string ImageFilePath { get; set; }


        private string dataFilePath;
        private DirectoryInfo splitDir;
        private DirectoryInfo outputDir;
        private int splittedParts;
        public void Read()
        {
            if (!File.Exists(dataFilePath))
            {
                throw new FileNotFoundException("File not found", dataFilePath);
            }

            outputDir = new DirectoryInfo(Path.GetDirectoryName(dataFilePath) + "/output/");
            splitDir = new DirectoryInfo(outputDir.FullName + "/split/");

            outputDir.Create();
            splitDir.Create();

            using (FileStream file = File.OpenRead(dataFilePath))
            {
                //Splits save with pattern 4ZLX
                splittedParts = depackSave(file);
            }

            Header header = new Header { HeaderFilePath = splitDir + "/part0.dat", SplittedParts = splittedParts };
            header.Read();

            decompressBlocks(header.getblockList());
            
           
        }

        private int depackSave(Stream data)
        {
            long len = data.Length;

            byte[] saveFile = new byte[len];
            byte[] pattern = { 0x34, 0x5A, 0x4C, 0x58 };

            data.Read(saveFile, 0, saveFile.Length);

            int last = 0;
            int result = search(saveFile, pattern, last);
            int part = 0;
            while (result != -1)
            {
                write(saveFile, last, result, splitDir.FullName + "part" + part.ToString() + ".dat");
                last = result;
                result = search(saveFile, pattern, last + 1);
                part++;
            }
            write(saveFile, last, saveFile.Length, splitDir.FullName + "part" + part.ToString() + ".dat");
            return part;
        }
        private int search(byte[] src, byte[] pattern, int start)
        {
            int c = src.Length - start - pattern.Length + 1;
            int j;
            for (int i = start; i < start + c; i++)
            {
                if (src[i] != pattern[0]) continue;
                for (j = pattern.Length - 1; j >= 1 && src[i + j] == pattern[j]; j--) ;
                if (j == 0) return i;
            }
            return -1;
        }

        private void write(byte[] src, int start, int end, string name)
        {
            using (FileStream fs = File.Open(name, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    writer.Write(src, start, end-start);
                }
            }      
        }

        private void decompressBlocks(LinkedList<Header.BlockInfo> blocks)
        {
            File.Delete(splitDir + "/sav.bin");
            while (blocks.Count > 0)
            {
                Header.BlockInfo info = blocks.First.Value;
                
                string compressedBlockFilePath = splitDir + "/part" + info.blockPart.ToString() + ".dat";
                string unompressedBlockFilePath = splitDir + "/part" + info.blockPart.ToString() + ".bin";
                string unompressedSaveFilePath = splitDir + "/sav.bin";
                Block block = new Block { CompressedBlockFilePath = compressedBlockFilePath, UncompressedBlockFilePath = unompressedBlockFilePath, Info = info, unompressedSaveFilePath = unompressedSaveFilePath };
                block.Read();

                blocks.RemoveFirst();
            }
        }

    
    }
}
