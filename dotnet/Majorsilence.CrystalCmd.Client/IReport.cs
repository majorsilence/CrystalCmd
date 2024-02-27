using Majorsilence.CrystalCmd.Common;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public interface IReport
    {
        Task<Stream> GenerateAsync(ReportData reportData, Stream report,
               System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        Task<Stream> GenerateAsync(ReportData reportData, Stream report, HttpClient httpClient,
                    System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));

        Stream Generate(ReportData reportData, Stream report);
        Stream Generate(ReportData reportData, Stream report, HttpClient httpClient);
    }
}