using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server
{
    internal class AnalyzerRoute : BaseRoute
    {
        public AnalyzerRoute(ILogger logger) : base(logger)
        {
        }

        // Keep existing pattern for internal use; prefer using AnalyzerController instead
        public async Task<byte[]> AnalyzerResultsBytes(byte[] report)
        {
            string id = Guid.NewGuid().ToString();
            var queue = WorkQueue.CreateDefault();
            await queue.Enqueue(new QueueItem()
            {
                Data = null,
                ReportTemplate = report,
                Id = id
            });

            FullReportAnalysisResponse response = null;
            for (int i = 0; i < 60; i++)
            {
                var result = await queue.Get(id);
                if (result.Status == WorkItemStatus.Processing || result.Status == WorkItemStatus.Pending)
                {
                    await Task.Delay(500); // Wait before polling again
                    continue;
                }
                else if (result.Status == WorkItemStatus.Completed)
                {
                    response = JsonConvert.DeserializeObject<FullReportAnalysisResponse>(Encoding.UTF8.GetString(result.Report.FileContent));
                    break;
                }
                else
                {
                    throw new Exception("Analyzer failed");
                }
            }

            var jsonResponse = JsonConvert.SerializeObject(response);
            return Encoding.UTF8.GetBytes(jsonResponse);
        }
    }
}
