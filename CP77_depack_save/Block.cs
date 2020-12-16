using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace CP77_depack_save
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
        public string unompressedSaveFilePath { get; set; }
        public Header.BlockInfo Info { get; set; }
        public void Read()
        {
            if(!File.Exists(CompressedBlockFilePath))
            {
                throw new FileNotFoundException("Block not found", CompressedBlockFilePath);
            }

            using (FileStream input = File.OpenRead(CompressedBlockFilePath))
            {
                if(input.Length != Info.SizeCompressed)
                {
                    //throw new Exception("Block size mismatch with header size");
                }

                using(BinaryReader compressedDataStream = new BinaryReader(input,Encoding.UTF8, true))
                {
                    byte[] compressedData = new byte[Info.SizeCompressed-8];
                    byte[] uncompressedData = new byte[Info.SizeUncompressed];
                    byte[] identifier = new byte[8];

                    compressedDataStream.Read(identifier);
                    compressedDataStream.Read(compressedData);

                    int uncompressed_bytes = LZ4Codec.Decode(compressedData, uncompressedData);

                    if(uncompressed_bytes != Info.SizeUncompressed)
                    {
                        throw new Exception("uncompressed data bytes mismatch with header info");
                    }

                    using (FileStream output = File.Open(unompressedSaveFilePath, FileMode.Append))
                    {
                        using (BinaryWriter writer = new BinaryWriter(output,Encoding.UTF8, true))
                        {
                            writer.Write(uncompressedData);
                        }
                    }

                    using (FileStream output = File.OpenWrite(UncompressedBlockFilePath))
                    using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, true))
                    {
                        writer.Write(uncompressedData);
                    }
                }
            }
        }
    }
}
