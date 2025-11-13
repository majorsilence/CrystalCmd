using System.ComponentModel.DataAnnotations.Schema;


namespace Majorsilence.CrystalCmd.WorkQueues
{
    [Table("generatedreports")]
    public class GeneratedReportPoco
    {
        [Column("id")]
        public string Id { get; set; }

        [Column("format")]
        public string Format { get; set; }

        [Column("generatedutc")]
        public DateTime GeneratedUtc { get; set; }

        [Column("filecontent")]
        public byte[] FileContent { get; set; }

        [Column("filename")]
        public string FileName { get; set; }

        [Column("metadata")]
        public string? Metadata { get; set; } // Optional: store extra info as JSON

    }
}
