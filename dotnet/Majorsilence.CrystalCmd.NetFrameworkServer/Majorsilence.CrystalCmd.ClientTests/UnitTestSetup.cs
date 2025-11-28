using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.ClientTests
{
    [SetUpFixture]
    internal class UnitTestSetup
    {
#pragma warning disable NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
        private System.Diagnostics.Process _workerProcess;
        private System.Diagnostics.Process _serverProcess;
#pragma warning restore NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method

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
            _serverProcess.StartInfo.EnvironmentVariables["ASPNETCORE_URLS"] = "http://*:44355;https://*:44356";
            _serverProcess.StartInfo.WorkingDirectory = System.IO.Path.Combine(baseDir, 
                "Majorsilence.CrystalCmd.Server",
                "bin",
                configuration,
                "net10.0");
            _serverProcess.StartInfo.UseShellExecute = false;
            _serverProcess.StartInfo.CreateNoWindow = true;
            _serverProcess.Start();

            await Task.Delay(5000); // Wait for server to start

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
            await TerminateProcessAsync(_workerProcess, 5000).ConfigureAwait(false);
            await TerminateProcessAsync(_serverProcess, 5000).ConfigureAwait(false);
        }

        private static async Task TerminateProcessAsync(System.Diagnostics.Process process, int gracefulTimeoutMs)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    process.Dispose();
                    return;
                }

                try
                {
                    // Try graceful close (works for GUI apps)
                    bool sent = process.CloseMainWindow();
                    if (sent)
                    {
                        if (process.WaitForExit(gracefulTimeoutMs))
                        {
                            process.Dispose();
                            return;
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // process exited between checks, ignore
                }

                // If still running, try Kill()
                try
                {
                    process.Kill();
                    if (process.WaitForExit(gracefulTimeoutMs))
                    {
                        process.Dispose();
                        return;
                    }
                }
                catch (System.PlatformNotSupportedException)
                {
                    // Kill overloads might not be supported on all targets; fall back to taskkill
                }
                catch (InvalidOperationException)
                {
                    // process already exited
                    process.Dispose();
                    return;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // insufficient permissions or already exiting; fallback below
                }

                // Final fallback: use taskkill to kill the process tree on Windows
                try
                {
                    int pid = process.Id;
                    using (var killer = new System.Diagnostics.Process())
                    {
                        killer.StartInfo.FileName = "taskkill";
                        killer.StartInfo.Arguments = "/PID " + pid + " /T /F";
                        killer.StartInfo.CreateNoWindow = true;
                        killer.StartInfo.UseShellExecute = false;
                        killer.Start();
                        // do not block indefinitely
                        killer.WaitForExit(2000);
                    }
                }
                catch
                {
                    // best-effort only
                }
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                    {
                        // last attempt
                        process.Kill();
                    }
                }
                catch
                {
                    // ignore
                }

                try
                {
                    process.Dispose();
                }
                catch
                {
                    // ignore
                }
            }

            await Task.Delay(200).ConfigureAwait(false);
        }
    }
}
