using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace CP77SaveCodec
{
    //TODO: Minimize explicit casting
    class Savegame
    {
        public Header Header { get { return header; } }
        public string DataFilePath {  get { return dataFilePath; } }
        public string MetadataFilePath { get { return metadataFilePath; } }

        private string metadataFilePath;
        private string imageFilePath;
        private string dataFilePath;

        private DirectoryInfo splitDir;
        private DirectoryInfo outputDir;
        private Header header;

        private int splittedParts;
        private bool succesfullyRead = false;
        public Savegame(string _data, string _meta = null, string _img = null)
        {
            if (!File.Exists(_data))
            {
                throw new FileNotFoundException("File not found", dataFilePath);
            }

            if (Path.GetFileName(_data) != "sav.dat")
            {
                throw new FormatException("Filepath doesn't lead to a sav.dat");
            }

            dataFilePath = _data;

            outputDir = new DirectoryInfo(Path.GetDirectoryName(dataFilePath) + "/output/");
            splitDir = new DirectoryInfo(outputDir.FullName + "/split/");

            if (_meta != null) metadataFilePath = _meta;
            if (_img != null) imageFilePath = _img;

            LZ4Codec.Enforce32 = true;
        }

        public void Read()
        {
            outputDir.Create();
            splitDir.Create();

            using (FileStream file = File.OpenRead(dataFilePath))
            {
                splittedParts = depackSave(file);
            }

            header = new Header(splitDir + "/part0.dat", splittedParts);
            header.Read();
            decompressBlocks(header.getblockList());
            succesfullyRead = true;

#if DEBUG
#else
            Directory.Delete(splitDir.FullName,true);
#endif
        }

        public void Write()
        {
            if (!succesfullyRead) throw new Exception("Savegame not ready, nothing to write");

            string uncompressedFilePath = outputDir + "/sav.bin";
            string compressedFilePath = outputDir + "/resav.dat";
            string trailerFilePath = outputDir + "/trailer.dat";

            File.Delete(compressedFilePath);

            using (MemoryStream compressedBlocksStream = new MemoryStream())
            using (MemoryStream trailerMemStream = new MemoryStream())
            using (FileStream trailerStream = File.OpenRead(trailerFilePath))
            using (FileStream output = File.OpenWrite(compressedFilePath))
            using (FileStream input = File.OpenRead(uncompressedFilePath))
            using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, true))
            {
                //Compress Blocks
                LinkedList<BlockInfo> blockInfos = compressBlocks(input, compressedBlocksStream);
                //Create Header
                writeHeader(writer, blockInfos);
                //Append compressed Blocks to header File
                writer.Write(compressedBlocksStream.ToArray());
                trailerStream.CopyTo(trailerMemStream);
                writer.Write(trailerMemStream.ToArray());            
            }

        }

        private int depackSave(Stream dataStream)
        {
            byte[] saveFile = new byte[dataStream.Length];
            byte[] patternBlock = Encoding.ASCII.GetBytes(Constant.BLOCK_START);
            byte[] patternTrailer = Encoding.ASCII.GetBytes(Constant.TRAILER_START);

            dataStream.Read(saveFile, 0, saveFile.Length);

            int lastIndex = 0;
            int newIndex = search(saveFile, patternBlock, lastIndex);
            int part = 0;

            while (newIndex != -1)
            {
                writeBlock(saveFile, lastIndex, newIndex, splitDir.FullName + "part" + part.ToString() + ".dat");
                lastIndex = newIndex;
                newIndex = search(saveFile, patternBlock, lastIndex + 1);
                part++;
            }

            int trailerIndex = search(saveFile, patternTrailer, lastIndex);

            writeBlock(saveFile, lastIndex, trailerIndex, splitDir.FullName + "part" + part.ToString() + ".dat");
            writeBlock(saveFile, trailerIndex, saveFile.Length, outputDir + "trailer.dat");
            return part;
        }

        //TODO: Optimizing
        //https://stackoverflow.com/questions/283456/byte-array-pattern-search
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
        private void writeBlock(byte[] src, int start, int end, string name)
        {
            using (FileStream fs = File.Open(name, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write(src, start, end - start);
            }      
        }

        private void decompressBlocks(LinkedList<BlockInfo> blocks)
        {
            File.Delete(outputDir + "/sav.bin");

            while (blocks.Count > 0)
            {
                BlockInfo info = blocks.First.Value;
                string compressedBlockFilePath = splitDir + "/part" + info.BlockPart.ToString() + ".dat";
                string uncompressedSaveFilePath = outputDir + "/sav.bin";
#if DEBUG
                string uncompressedBlockFilePath = splitDir + "/part" + info.BlockPart.ToString() + ".bin";
                Block block = new Block { CompressedBlockFilePath = compressedBlockFilePath, 
                                          UncompressedSaveFilePath = uncompressedSaveFilePath, 
                                          UncompressedBlockFilePath = uncompressedBlockFilePath,
                                          Info = info};
#else
                Block block = new Block {CompressedBlockFilePath = compressedBlockFilePath, 
                                         UncompressedSaveFilePath = uncompressedSaveFilePath, 
                                         Info = info};
#endif

                block.Read();
                blocks.RemoveFirst();
            }
        }

        private LinkedList<BlockInfo> compressBlocks(Stream input, Stream output)
        {
            LinkedList<BlockInfo> blocks = new LinkedList<BlockInfo>();
           
            using (BinaryReader reader = new BinaryReader(input, Encoding.UTF8, true))
            using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, true))
            {
                long filelength = input.Length;
                byte[] buffer = new byte[Constant.BLOCKSIZE];
                             
                int cFullChunks = (int)(filelength / (long)Constant.BLOCKSIZE);
                int sizeLastChunk = (int)(filelength % (long)Constant.BLOCKSIZE);
                int part = 1;
                for (int i = 0; i < cFullChunks; i++)
                {
                    reader.Read(buffer);
                    blocks.AddLast(compressBlock(writer, (uint)Constant.BLOCKSIZE, buffer));
                }

                if (sizeLastChunk > 0)
                {
                    reader.Read(buffer);
                    blocks.AddLast(compressBlock(writer, (uint)sizeLastChunk, buffer));
                }

                return blocks;
            }
        }

        //TODO: Check if compiler generates identifier each call or stays in memory
        private BlockInfo compressBlock(BinaryWriter writer, UInt32 blockSize, Span<byte> inputBuffer)
        {
            byte[] identifier = Encoding.ASCII.GetBytes(Constant.BLOCK_START);
            byte[] compressedBuffer = new byte[LZ4Codec.MaximumOutputSize(Constant.BLOCKSIZE)];

            
            writer.Write(identifier);
            writer.Write(blockSize);
          
            int compressedBytes = LZ4Codec.Encode(inputBuffer.ToArray(), 0, (int)blockSize, compressedBuffer, 0, compressedBuffer.Length);

            if (compressedBytes == -1) throw new Exception("compressing chunk failed");

            writer.Write(compressedBuffer, 0, compressedBytes);

            File.Delete("blockTest.dat");
            using( FileStream file = File.OpenWrite("blockTest.dat"))
            using (BinaryWriter writ = new BinaryWriter(file, Encoding.UTF8, true))
            {
                writ.Write(identifier);
                writ.Write(blockSize);
                writ.Write(compressedBuffer, 0, compressedBytes);
            }
            return new BlockInfo { SizeCompressed = (uint)(compressedBytes + identifier.Length + sizeof(UInt32)), SizeUncompressed = blockSize };
        }

        private void writeHeader(BinaryWriter writer, LinkedList<BlockInfo> blocks)
        {
            byte[] header_start = Encoding.ASCII.GetBytes(Constant.HEADER_INFO_START);
            UInt32 saveVersion = (UInt32)header.SaveVersion;
            UInt32 gameVersion = (UInt32)header.GameVersion;
            /* byte[] unknownData = { 0x00, 0xD8, 0x37, 0x88, 0x03, 0x00, 0xC0 };
             byte[] unknownConstant = { 0x45, 0x7E, 0xC3, 0x00, 0x00, 0x00 };*/
            byte[] unknown = header.Unknown;
            byte[] header_end = Encoding.ASCII.GetBytes(Constant.HEADER_INFO_END);
            Int32 nBlocks = blocks.Count;

            int sizeOfData = Constant.SIZE_OF_FIXED_HEADER + (blocks.Count * 3 * sizeof(UInt32));
            int currentSize = Constant.SIZE_OF_FULL_HEADER;

            byte[] spacer = new byte[Constant.SIZE_OF_FULL_HEADER - sizeOfData];
            Array.Fill<byte>(spacer, 0x00);

            writer.Write(header_start);
            writer.Write(saveVersion);
            writer.Write(gameVersion);
            writer.Write(unknown);
            //writer.Write(unknownConstant);
            writer.Write(header_end);
            writer.Write(nBlocks);
            writer.Write(Constant.SIZE_OF_FULL_HEADER);

            BlockInfo info;
            while (blocks.Count > 1)
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
        }
    }
}
