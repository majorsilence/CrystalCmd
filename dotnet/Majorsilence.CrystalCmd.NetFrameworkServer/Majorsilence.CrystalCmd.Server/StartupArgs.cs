using System;

namespace Majorsilence.CrystalCmd.Server
{
    public class StartupArgs
    {
        public StartupArgs(string[] args)
        {
            Args = args ?? throw new ArgumentNullException(nameof(args), "Startup arguments cannot be null.");
        }
        public string[] Args { get; }
    }
}
