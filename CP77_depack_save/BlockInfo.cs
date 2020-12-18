using System;

namespace CP77SaveCodec
{
    public struct BlockInfo
    {
        public UInt32 SizeCompressed { get; set; }
        public UInt32 SizeUncompressed { get; set; }
        public UInt32 BlockEndOffset { get; set; }
        public int BlockPart { get; set; }
    }
}
