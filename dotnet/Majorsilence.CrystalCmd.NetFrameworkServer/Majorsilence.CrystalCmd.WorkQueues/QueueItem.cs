namespace Majorsilence.CrystalCmd.WorkQueues
{
    public class QueueItem
    {
        public string Id { get; set; }
        public byte[] ReportTemplate { get; set; }
        public CrystalCmd.Common.Data Data { get; set; }
    }
}
