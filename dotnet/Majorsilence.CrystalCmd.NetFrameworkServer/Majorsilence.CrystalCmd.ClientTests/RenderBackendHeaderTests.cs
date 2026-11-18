using Majorsilence.CrystalCmd.Client;
using Majorsilence.CrystalCmd.Common;
using NUnit.Framework;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// Outside Majorsilence.CrystalCmd.ClientTests on purpose: that namespace's [SetUpFixture] starts a
// server and workers, and these tests need neither.
namespace Majorsilence.CrystalCmd.ClientUnitTests
{
    [TestFixture]
    public class RenderBackendHeaderTests
    {
        [Test]
        public async Task TheBackendTheServerChoseIsRecorded()
        {
            var report = await Generate(new FakeServer("RptEngine", "RptEngine, by the serviceable rule"));

            Assert.That(report.LastRenderBackend, Is.EqualTo(RenderBackend.RptEngine));
            Assert.That(report.LastRenderBackendReason, Is.EqualTo("RptEngine, by the serviceable rule"));
        }

        [Test]
        public async Task AServerWithoutTheHeaderLeavesItUnknown()
        {
            var report = await Generate(new FakeServer(null, null));

            Assert.That(report.LastRenderBackend, Is.Null);
            Assert.That(report.LastRenderBackendReason, Is.Null);
        }

        private static async Task<Report> Generate(FakeServer server)
        {
            var report = new Report("http://127.0.0.1:1", bearerToken: "token");
            using (var client = new HttpClient(server))
            using (var template = new MemoryStream(new byte[] { 1, 2, 3 }))
            using (var output = await report.GenerateAsync(new Data(), template, client))
            {
                Assert.That(output, Is.Not.Null);
            }
            return report;
        }

        private sealed class FakeServer : HttpMessageHandler
        {
            private readonly string _backend;
            private readonly string _reason;

            public FakeServer(string backend, string reason)
            {
                _backend = backend;
                _reason = reason;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response;
                if (request.Method == HttpMethod.Post)
                {
                    response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("poll-id") };
                    if (_backend != null)
                        response.Headers.Add(RenderBackendHeaders.Backend, _backend);
                    if (_reason != null)
                        response.Headers.Add(RenderBackendHeaders.Reason, _reason);
                }
                else
                {
                    response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }) };
                }
                return Task.FromResult(response);
            }
        }
    }
}
