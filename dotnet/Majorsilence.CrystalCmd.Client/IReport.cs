using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public interface IReport
    {
        Task<Stream> GenerateAsync(Data reportData, Stream report,
               System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        Task<Stream> GenerateAsync(Data reportData, Stream report, HttpClient httpClient,
                    System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));

        Stream Generate(Data reportData, Stream report);
        Stream Generate(Data reportData, Stream report, HttpClient httpClient);
    }
}