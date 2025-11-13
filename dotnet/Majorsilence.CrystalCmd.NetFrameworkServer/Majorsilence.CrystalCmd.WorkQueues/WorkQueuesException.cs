using Majorsilence.CrystalCmd.Common;
using System;


namespace Majorsilence.CrystalCmd.WorkQueues
{
    internal class WorkQueuesException : CrystalCmdException
    {
        public WorkQueuesException(string message)
            : base(message)
        {
        }
        public WorkQueuesException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
