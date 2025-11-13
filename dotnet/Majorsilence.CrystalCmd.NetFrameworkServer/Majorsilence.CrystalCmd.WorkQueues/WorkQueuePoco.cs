using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Majorsilence.CrystalCmd.WorkQueues
{

    [Table("workqueue")]
    public class WorkQueuePoco
    {
        [Key][Column("id")] public string Id { get; set; }

        [Column("timecreatedutc")] public DateTime TimeCreatedUtc { get; set; }

        [Column("retrycount")] public int RetryCount { get; set; }

        [Column("nextretryutc")] public DateTime? NextRetryUtc { get; set; }

        [Column("maxretries")] public int MaxRetries { get; set; }

        [Column("status")][Required] public WorkItemStatus Status { get; set; } = WorkItemStatus.Pending;

        [Column("timeprocessedutc")] public DateTime? TimeProcessedUtc { get; set; }

        /// <summary>
        /// for sqlite, this is the discriminator column
        /// </summary>
        [Column("lockid")]
        [StringLength(50)]
        public string? LockId { get; set; }

        /// <summary>
        /// for sqlite, this is the discriminator column
        /// </summary>
        [Column("lockeduntilutc")]
        public DateTime? LockedUntilUtc { get; set; }

        [Column("channel")]
        [Required]
        [StringLength(50)]
        public string Channel { get; set; }

        [Column("payload")][Required] public string Payload { get; set; }

        [Column("errormessage")]
        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        [NotMapped]
        public QueueItem PayloadAsQueueItem
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Payload))
                {
                    return default!;
                }

                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                };

                return JsonConvert.DeserializeObject<QueueItem>(Payload, settings);
            }
        }
    }
}
