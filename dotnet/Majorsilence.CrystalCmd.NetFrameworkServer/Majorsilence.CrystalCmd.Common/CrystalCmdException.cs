using System;

namespace Majorsilence.CrystalCmd.Common
{
    public class CrystalCmdException : Exception
    {
        public CrystalCmdException(string message) : base(message) { }
        public CrystalCmdException(string message, Exception ex) : base(message, ex) { }
    }
}
