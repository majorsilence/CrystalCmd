using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.ClientTests
{
    [TestFixture]
    internal class UnitTestSetup
    {
        private System.Diagnostics.Process _workerProcess;
        private System.Diagnostics.Process _serverProcess;
        [OneTimeSetUp]
        public async Task Init()
        {
#if DEBUG
            string configuration = "Debug";
#else
            string configuration = "Release";
#endif

            string currentWorkingDir = System.IO.Directory.GetCurrentDirectory();
            string baseDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(currentWorkingDir, @"..\..\..\.."));

            // start the net48 worker process
            string workerExePath = System.IO.Path.Combine(baseDir,
                "Majorsilence.CrystalCmd.Console",
                "bin",
                configuration,
                "net48",
                "Majorsilence.CrystalCmd.NetframeworkConsole.exe");
            _workerProcess = new System.Diagnostics.Process();
            _workerProcess.StartInfo.FileName = workerExePath;
            _workerProcess.StartInfo.WorkingDirectory = System.IO.Path.Combine(baseDir,
                "Majorsilence.CrystalCmd.Console",
                "bin",
                configuration,
                "net48");
            _workerProcess.StartInfo.UseShellExecute = false;
            _workerProcess.StartInfo.CreateNoWindow = true;
            _workerProcess.Start();

            // start the net10.0 web server process
            _serverProcess = new System.Diagnostics.Process();
            _serverProcess.StartInfo.FileName = "dotnet";
            _serverProcess.StartInfo.Arguments = "Majorsilence.CrystalCmd.Server.dll";
            _serverProcess.StartInfo.WorkingDirectory = System.IO.Path.Combine(baseDir, 
                "Majorsilence.CrystalCmd.Server",
                "bin",
                configuration,
                "net10.0");
            _serverProcess.StartInfo.UseShellExecute = false;
            _serverProcess.StartInfo.CreateNoWindow = true;
            _serverProcess.Start();

            await Task.Delay(5000); // Wait for server to start
        }

        [Test]
        public async Task IsOnlineTest()
        {
            // ping the server to see if it's online
            using (var client = new System.Net.Http.HttpClient())
            {
                var response = await client.GetAsync("http://localhost:44355/healthz/ready");
                Assert.That(response.IsSuccessStatusCode, "Server is not online.");
            }

        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            _workerProcess.Close();
            _workerProcess?.Dispose();
            _serverProcess.Close();
            _serverProcess?.Dispose();

            await Task.Delay(2000); // Wait for processes to exit
        }
    }
}
