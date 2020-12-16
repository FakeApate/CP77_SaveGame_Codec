
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace CP77_depack_save
{
    class Program
    {
        static void Main(string[] args)
        {
            Savegame save = new Savegame { DataFilePath = @"C:\Users\Sam\Documents\CP Save Editing\Saves\EndGameSave-0\sav.dat" };
            save.Read();
           
        }
       
    }
}
