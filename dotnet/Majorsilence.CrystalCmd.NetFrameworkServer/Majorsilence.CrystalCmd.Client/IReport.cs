using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public interface IReport
    {
        Task<Stream> GenerateAsync(Common.Data reportData, Stream report,
               System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        Task<Stream> GenerateAsync(Common.Data reportData, Stream report, HttpClient httpClient,
                    System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));

        Stream Generate(Common.Data reportData, Stream report);
        Stream Generate(Common.Data reportData, Stream report, HttpClient httpClient);
    }
}