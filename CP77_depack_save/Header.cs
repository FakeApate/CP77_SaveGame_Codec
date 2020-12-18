using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CP77SaveCodec
{

    class Header
    {
        /*
         * Structure:
         * 4 Bytes: "VASC" / "EVAS"
         * 4 Bytes: Save Version
         * 4 Bytes: Game Version
         * 7 Bytes: Unknown 
         * 6 Bytes: 45 7E C3 00 00 00 Unknown Function
         * 4 Bytes: "FZLC"
         * 4 Bytes: Number of blocks following
         * 4 Bytes: Size of Header (this)
         * 
         * For every block except Last:
         * 4 Bytes: Size compressed
         * 4 Bytes: Size uncompressed
         * 4 Bytes: End of block (Uninmported with this method)
         * 
         */

        public string HeaderFilePath { get { return headerFilePath; } }
        public int SplittedParts { get { return splittedParts; } }
        public int GameVersion {  get { return (int)gameVersion; } }
        public int SaveVersion {  get { return (int)saveVersion; } }
        public int NumberOfBlocks {  get { return (int)numberOfBlocks; } }
        public byte[] Unknown {  get { return unknown; } }

        private string headerFilePath;
        private int splittedParts;
        private LinkedList<BlockInfo> blocks = new LinkedList<BlockInfo>();

        private byte[] unknown = new byte[13];
        private UInt32 gameVersion;
        private UInt32 saveVersion;
        private UInt32 numberOfBlocks;

        public Header(string _path, int _splitted)
        {
            if (!File.Exists(_path)) throw new FileNotFoundException("Headerfile not found.", _path);
            headerFilePath = _path;

            if (_splitted < 1) throw new Exception(" Invalid number of blocks");
            splittedParts = _splitted;
        }

        public void Read()
        {
            using (FileStream input = File.OpenRead(headerFilePath))
            using (BinaryReader reader = new BinaryReader(input, Encoding.UTF8, true))
            {
                //INFO START
                string headerInfo = new string(reader.ReadChars(4));
                if (headerInfo != Constant.HEADER_INFO_START && headerInfo != Constant.HEADER_INFO_START_ALTERNATIVE)
                {
                    throw new Exception("Headerfile corrupted");
                }

                //GAME&SAVE VERSION
                saveVersion = reader.ReadUInt32();
                gameVersion = reader.ReadUInt32();

                //UNKNOWN DATA
                reader.Read(unknown);

                //INFO END
                headerInfo = new string(reader.ReadChars(4));
                if (headerInfo != Constant.HEADER_INFO_END)
                {
                    throw new Exception("Headerfile corrupted");
                }

                //BLOCK COUNT
                numberOfBlocks = reader.ReadUInt32();
                if (splittedParts != numberOfBlocks)
                {
                    throw new Exception("Number of splitted Parts mismatches with header info");
                }

                //HEADER BYTE SIZE
                UInt32 sizeOf_Header = reader.ReadUInt32();
                if (sizeOf_Header != input.Length)
                {
                    throw new Exception("Actuall Size mismatches with Header Size");
                }

                //BLOCKS
                int blockPart = 1;
                BlockInfo blockInfo;
                do
                {
                    blockInfo = new BlockInfo
                    {
                        BlockPart = blockPart++,
                        SizeCompressed = reader.ReadUInt32(),
                        SizeUncompressed = reader.ReadUInt32(),
                        BlockEndOffset = reader.ReadUInt32()
                    };
                    blocks.AddLast(blockInfo);

                } while (blockInfo.BlockEndOffset > 0);
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
        

    }
}
