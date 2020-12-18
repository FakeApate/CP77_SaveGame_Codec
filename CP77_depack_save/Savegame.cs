using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        public static int BLOCKSIZE = 262144; //265kb

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
            File.Delete(outputDir + "/sav.bin");
            while (blocks.Count > 0)
            {
                Header.BlockInfo info = blocks.First.Value;
                
                string compressedBlockFilePath = splitDir + "/part" + info.blockPart.ToString() + ".dat";
                string unompressedBlockFilePath = splitDir + "/part" + info.blockPart.ToString() + ".bin";
                string unompressedSaveFilePath = outputDir + "/sav.bin";
                Block block = new Block { CompressedBlockFilePath = compressedBlockFilePath, UncompressedBlockFilePath = unompressedBlockFilePath, Info = info, unompressedSaveFilePath = unompressedSaveFilePath };
                block.Read();

                blocks.RemoveFirst();
            }
        }

        /*
         * 
         * Testing Phase
         * 
         * 
         * 
         */
        public void Write()
        {
            string uncompressedFilePath  = outputDir + "/sav.bin";
            string compressedFilePath = outputDir + "/resav.dat";
            File.Delete(compressedFilePath);

            LinkedList<Header.BlockInfo> blocks = new LinkedList<Header.BlockInfo>();
            MemoryStream dataStream = new MemoryStream();
            using(FileStream input = File.OpenRead(uncompressedFilePath))
            using(BinaryReader reader = new BinaryReader(input, Encoding.UTF8, true))
            using(BinaryWriter writer = new BinaryWriter(dataStream, Encoding.UTF8, true))
            {
                
                long filelength = input.Length;
                byte[] buffer = new byte[BLOCKSIZE];
                byte[] compressedBuffer = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(BLOCKSIZE)];
                byte[] identifier = { 0x34, 0x5A, 0x4C, 0x58 };

                //Calculating amount of chunks
                int cFullChunks = (int)(filelength / (long)BLOCKSIZE);
                int sizeLastChunk = (int)(filelength % (long)BLOCKSIZE);

                K4os.Compression.LZ4.LZ4Level compLevel = K4os.Compression.LZ4.LZ4Level.L00_FAST;
                for (int i = 0; i < cFullChunks; i++){
                    writer.Write(identifier);
                    writer.Write((Int32)BLOCKSIZE);
                    reader.Read(buffer);
                    
                    int compressedBytes = K4os.Compression.LZ4.LZ4Codec.Encode(buffer, 0, buffer.Length, compressedBuffer, 0, compressedBuffer.Length,compLevel);
                    if(compressedBytes == -1)
                    {
                        throw new Exception("compressing chunk failed");
                    }
                    blocks.AddLast(new Header.BlockInfo { SizeCompressed = (UInt32)compressedBytes+8, SizeUncompressed = (UInt32)BLOCKSIZE });
                    writer.Write(compressedBuffer, 0,compressedBytes);
                }

                if(sizeLastChunk > 0)
                {
                    writer.Write(identifier);
                    writer.Write((Int32)sizeLastChunk);
                    reader.Read(buffer);

                    int compressedBytes = K4os.Compression.LZ4.LZ4Codec.Encode(buffer, 0, sizeLastChunk, compressedBuffer, 0, compressedBuffer.Length, compLevel);
                    if (compressedBytes == -1)
                    {
                        throw new Exception("compressing chunk failed");
                    }
                    blocks.AddLast(new Header.BlockInfo { SizeCompressed = (UInt32)compressedBytes + 8, SizeUncompressed = (UInt32)sizeLastChunk });
                    writer.Write(compressedBuffer, 0,compressedBytes);
                }

                

            }

            
            using (FileStream output = File.OpenWrite(compressedFilePath))
            using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, true))
            {
                byte[] magic = { 0x56, 0x41, 0x53, 0x43 };
                UInt32 saveVersion = 193;
                UInt32 gameVersion = 8;
                byte[] unknownData = { 0x00, 0xD8, 0x37, 0x88, 0x03, 0x00, 0xC0 };
                byte[] unknownConstant = { 0x45, 0x7E, 0xC3, 0x00, 0x00, 0x00 };
                byte[] magic2 = { 0x46, 0x5A, 0x4C, 0x43 };
                Int32 nBlocks = blocks.Count;
                Int32 sizeOfHeader = 3105;
                int sizeOfData = 0x25 + (blocks.Count * 3 * sizeof(UInt32));
                int currentSize = sizeOfHeader;
                Header.BlockInfo info;
                byte[] spacer = new byte[sizeOfHeader - sizeOfData];
                Array.Fill<byte>(spacer, 0x00);

                writer.Write(magic);
                writer.Write(saveVersion);
                writer.Write(gameVersion);
                writer.Write(unknownData);
                writer.Write(unknownConstant);
                writer.Write(magic2);
                writer.Write(nBlocks);
                writer.Write(sizeOfHeader);

                while(blocks.Count > 1)
                {
                    info = blocks.First.Value;
                    writer.Write((UInt32)info.SizeCompressed);
                    writer.Write((UInt32)info.SizeUncompressed);
                    writer.Write((UInt32)(currentSize + info.SizeCompressed));
                    blocks.RemoveFirst();
                    currentSize += (int)info.SizeCompressed;
                }

                info = blocks.First.Value;
                writer.Write((UInt32)info.SizeCompressed);
                writer.Write((UInt32)info.SizeUncompressed);
                writer.Write(0x00000000);
                writer.Write(spacer);
                writer.Write(dataStream.ToArray());
                dataStream.Close();
            }



        }

    }
}
