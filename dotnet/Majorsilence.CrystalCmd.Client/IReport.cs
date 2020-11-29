using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public interface IReport
    {
        Task<Stream> GenerateAsync(Data reportData, Stream report);
        Task<Stream> GenerateAsync(Data reportData, Stream report, HttpClient httpClient);
    }
}