using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading;

namespace Majorsilence.CrystalCmd.Tests
{
    [TestFixture]
    public class HealthCheckTaskTests
    {
        // Stop() must return promptly and without throwing even while the background
        // task is mid-loop or failing. The old implementation waited on the background
        // task with no timeout, which could block forever (and the failure path inside
        // the task called Stop() on itself, deadlocking instead of exiting).
        [Test]
        public void StopReturnsPromptlyWhileTaskIsFailing()
        {
            var healthCheck = new HealthCheckTask(new Mock<ILogger>().Object,
                "no-such-report.rpt", failureShouldExitProcess: false);
            healthCheck.Start();

            // Give the task time to attempt (and fail) at least one export.
            Thread.Sleep(TimeSpan.FromSeconds(2));

            var stopwatch = Stopwatch.StartNew();
            Assert.DoesNotThrow((Action)(() => healthCheck.Stop()));
            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
        }

        // After repeated failures the background task must end on its own (the path
        // that previously called Stop() on itself and deadlocked, so the process was
        // never restarted). failureShouldExitProcess is false so the test process
        // survives; the same code path calls Environment.Exit(1) when it is true.
        [Test]
        public void RepeatedFailuresTerminateTaskWithoutDeadlock()
        {
            var healthCheck = new HealthCheckTask(new Mock<ILogger>().Object,
                "no-such-report.rpt", failureShouldExitProcess: false,
                checkInterval: TimeSpan.FromMilliseconds(100));
            healthCheck.Start();

            bool completed = healthCheck.BackgroundTask.Wait(TimeSpan.FromSeconds(30));

            Assert.Multiple((Action)(() =>
            {
                Assert.That(completed, Is.True,
                    "background task should stop itself after repeated failures instead of deadlocking");
                Assert.That(HealthCheckTask.IsHealthy, Is.False);
            }));
        }
    }
}
