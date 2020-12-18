
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace CP77SaveCodec
{
    class Program
    {
        static void Main(string[] args)
        {
            Savegame save = new Savegame(_data: @"C:\Users\Sam\Documents\CP Save Editing\Saves\ManualSave-10\sav.dat");
            save.Read();
            save.Write();
        }
       
    }
}
