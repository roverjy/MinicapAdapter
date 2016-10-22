using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TW.Minicap
{
    public class MinicapBanner
    {
        public int Version { get; set; }
        public int Length { get; set; }
        public int PID { get; set; }
        public int ReadWidth { get; set; }
        public int ReadHeight { get; set; }
        public int VirtualWidth { get; set; }
        public int VirtualHeight { get; set; }
        public int Orientation { get; set; }
        public int Quirks { get; set; }

        public MinicapBanner()
        {
            Version = 0;
            PID = 0;
            Length = 0;
            ReadWidth = 0;
            ReadHeight = 0;
            VirtualHeight = 0;
            VirtualWidth = 0;
            Orientation = 0;
            Quirks = 0;
        }
    }
}
