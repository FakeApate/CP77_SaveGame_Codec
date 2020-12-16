using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CP77_depack_save
{

    class Header
    {
        /*
         * Structure:
         * 4 Bytes: "VASC"
         * 21 Bytes: Unknown 
         * 4 Bytes: "FZLC"
         * 4 Bytes: Number of blocks following
         * 4 Bytes: Size of Header (this)
         * 
         * For every block except Last:
         * 4 Bytes: Size compressed
         * 4 Bytes: Size uncompressed
         * 4 Bytes: End of block (Uninmported with this method)
         * 
         * Last block:
         * 4 Bytes: Size compressed
         * 4 Bytes: Size uncompressed
         * 
         */

        public string HeaderFilePath { get; set; }
        public int SplittedParts { get; set; }

        private static string HEADERINFOSTART = "VASC";
        private static string HEADERINFOEND = "FZLC";
        
        private byte[] unknown = new byte[21];

        private UInt32 numberOf_blocks;
        private UInt32 sizeOf_Header;

        private LinkedList<BlockInfo> blocks = new LinkedList<BlockInfo>();

        public void Read()
        {
            if (!File.Exists(HeaderFilePath))
            {
                throw new FileNotFoundException("Headerfile not found.", HeaderFilePath);
            }

            using (FileStream input = File.OpenRead(HeaderFilePath))
            {
                using(BinaryReader reader = new BinaryReader(input, Encoding.UTF8, true))
                {
                    //Start of Protocoll

                    //INFO START
                    string info = new string(reader.ReadChars(4));
                    if (  info != HEADERINFOSTART)
                    {
                        throw new Exception("Headerfile corrupted");
                    }

                    //Unknown
                    reader.Read(unknown);

                    //INFO END
                    info = new string(reader.ReadChars(4));
                    if (info != HEADERINFOEND)
                    {
                        throw new Exception("Headerfile corrupted");
                    }

                    //Number of blocks
                    numberOf_blocks = reader.ReadUInt32();
                    if(SplittedParts != numberOf_blocks)
                    {
                        throw new Exception("Number of splitted Parts mismatches with header info");
                    }

                    //Size of Header
                    sizeOf_Header = reader.ReadUInt32();
                    if(sizeOf_Header != input.Length)
                    {
                        throw new Exception("Actuall Size mismatches with Header Size");
                    }

                    //blocks
                    int blockPart = 1;
                    UInt32 blockOffset;                  
                    do
                    {
                        blocks.AddLast(new BlockInfo
                        {
                            blockPart = blockPart++,
                            SizeCompressed = reader.ReadUInt32(),
                            SizeUncompressed = reader.ReadUInt32()
                        });

                        //block Offset
                        blockOffset = reader.ReadUInt32();

                    } while (blockOffset > 0);


                }

            }



        }

        public LinkedList<BlockInfo> getblockList () {
            LinkedList<BlockInfo> newList = new LinkedList<BlockInfo>();

            var current = blocks.First;
            while(current != null)
            {
                newList.AddLast(current.Value);
                current = current.Next;
            }

            return newList;
        }
        public struct BlockInfo
        {
            public UInt32 SizeCompressed { get; set; }
            public UInt32 SizeUncompressed { get; set; }
            public int blockPart { get; set; }

        }

    }
}
