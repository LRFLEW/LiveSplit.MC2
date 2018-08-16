using System;
using System.Diagnostics;
using LiveSplit.ComponentUtil;

namespace LiveSplit.MC2
{
    class Version
    {
        public readonly int game, movie, frontend, raceeditor, carviewer, disclaimer,
            moviestr, frontendstr, raceeditorstr, carviewerstr;

        private Version(
                int game, int movie, int frontend, int raceeditor, int carviewer, int disclaimer,
                int moviestr, int frontendstr, int raceeditorstr, int carviewerstr)
        {
            this.game = game; this.movie = movie; this.frontend = frontend; this.raceeditor = raceeditor;
            this.carviewer = carviewer; this.disclaimer = disclaimer; this.moviestr = moviestr;
            this.frontendstr = frontendstr; this.raceeditorstr = raceeditorstr; this.carviewerstr = carviewerstr;
        }
        
        private static readonly Version retail = new Version(
            0x003B80, 0x003860, 0x0038E0, 0x003AE0, 0x003B30,
            0x2622B0, 0x22D984, 0x22D9D8, 0x22D9E4, 0x22D9F4);
        private static readonly Version steam = new Version(
            0x003B80, 0x003860, 0x0038E0, 0x003AE0, 0x003B30,
            0x2622B0, 0x22D980, 0x22D9D4, 0x22D9E0, 0x22D9F0);

        public static Version GetVersion(Process mc2, IntPtr baseaddr)
        {
            int magic = mc2.ReadValue<int>(baseaddr + 0x230000);
            if (magic == 0x6F727241) return retail;
            if (magic == 0x5F737475) return steam;
            return null;
        }
    }
}
