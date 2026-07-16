using Majorsilence.CrystalCmd.NetframeworkConsole;
using NUnit.Framework;

namespace Majorsilence.CrystalCmd.Tests
{
    [TestFixture]
    public class ExportQueueChannelTests
    {
        // The server controllers (ExportController/AnalyzerController in
        // Majorsilence.CrystalCmd.Server) enqueue on these exact channel names, so the
        // worker must consume the same ones. The Windows Service once listened on
        // "analyzer-reports" while the server enqueued on "crystal-analyzer", leaving
        // analyzer jobs pending forever in service deployments.
        [Test]
        public void WorkerChannelNamesMatchServerControllers()
        {
            Assert.That(ExportQueue.ReportsChannel, Is.EqualTo("crystal-reports"));
            Assert.That(ExportQueue.AnalyzerChannel, Is.EqualTo("crystal-analyzer"));
        }
    }
}
