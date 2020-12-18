using System;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace CP77SaveCodec
{
    class Block
    {
        /*
         * Structure:
         * 4 Bytes: Identifier
         * 4 Bytes: Size uncompressed
         * Rest: Lz4Block
         * 
         */
        public string CompressedBlockFilePath { get; set; }
        public string UncompressedBlockFilePath { get; set; }
        public string UncompressedSaveFilePath { get; set; }
        public BlockInfo Info { get; set; }

        public void Read()
        {
            if(!File.Exists(CompressedBlockFilePath))
            {
                throw new FileNotFoundException("Block not found", CompressedBlockFilePath);
            }

            using (FileStream input = File.OpenRead(CompressedBlockFilePath))
            using (BinaryReader compressedDataStream = new BinaryReader(input, Encoding.UTF8, true))
            {
                if (input.Length != Info.SizeCompressed)
                {
                    //Last blocksize missmatches,makes checking impossible
                    //throw new Exception("Block size missmatch with header size");
                }

                byte[] compressedData = new byte[Info.SizeCompressed - 8];
                byte[] uncompressedData = new byte[Info.SizeUncompressed];
                byte[] identifier = new byte[4];
                UInt32 uncompressedSize;

                compressedDataStream.Read(identifier);
                uncompressedSize = compressedDataStream.ReadUInt32();
                compressedDataStream.Read(compressedData);

                int uncompressed_bytes = LZ4Codec.Decode(compressedData, uncompressedData);

                if (uncompressed_bytes != Info.SizeUncompressed && uncompressedSize != Info.SizeUncompressed)
                {
                    throw new Exception("uncompressed data bytes mismatch with header info");
                }

                using (FileStream output = File.Open(UncompressedSaveFilePath, FileMode.Append))
                using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, true))
                {
                    writer.Write(uncompressedData);
                }

#if DEBUG
                using (FileStream output = File.OpenWrite(UncompressedBlockFilePath))
                using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, true))
                {
                    writer.Write(uncompressedData);
                }
#endif

            }
        }
    }
}
