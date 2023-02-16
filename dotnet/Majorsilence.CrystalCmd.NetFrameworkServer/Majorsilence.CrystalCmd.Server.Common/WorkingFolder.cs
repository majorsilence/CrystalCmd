using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Common
{
    public static class WorkingFolder
    {
        public static string GetMajorsilenceTempFolder()
        {
            return Path.Combine(Path.GetTempPath(), "majorsilence", "crystalcmd");
        }
    }
}
