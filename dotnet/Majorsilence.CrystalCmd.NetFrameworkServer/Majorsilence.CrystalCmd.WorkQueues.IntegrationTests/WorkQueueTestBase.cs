namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

/// <summary>
/// Shared test logic run against both SQL Server and PostgreSQL.
/// Subclasses supply the database-specific WorkQueue factory.
/// </summary>
public abstract class WorkQueueTestBase
{
    // Each test gets its own channel so rows from parallel tests don't interfere.
    protected string Channel { get; private set; } = string.Empty;

    protected abstract WorkQueue CreateQueue(string channel);

    [SetUp]
    public async Task SetUpTest()
    {
        Channel = Guid.NewGuid().ToString("N")[..20];
        // Migrate is idempotent; safe to call per-test.
        await CreateQueue(Channel).Migrate();
    }

    // -------------------------------------------------------------------------
    // Schema
    // -------------------------------------------------------------------------

    [Test]
    public async Task Migrate_CreatesSchema_Idempotent()
    {
        // Running twice must not throw.
        var queue = CreateQueue(Channel);
        await queue.Migrate();
        await queue.Migrate();
        Assert.Pass("No exception on repeated Migrate()");
    }

    // -------------------------------------------------------------------------
    // Basic enqueue / dequeue
    // -------------------------------------------------------------------------

    [Test]
    public async Task Enqueue_And_Dequeue_RoundTrip()
    {
        var queue = CreateQueue(Channel);
        var itemId = Guid.NewGuid().ToString();
        await queue.Enqueue(MakeQueueItem(itemId));

        string? processedId = null;
        await queue.Dequeue(async workItem =>
        {
            processedId = workItem.Id;
            return MakeReport(workItem.Id);
        });

        Assert.That(processedId, Is.EqualTo(itemId));

        var (report, status) = await queue.Get(itemId);
        Assert.That(status, Is.EqualTo(WorkItemStatus.Completed));
        Assert.That(report, Is.Not.Null);
        Assert.That(report!.FileContent, Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task Dequeue_WhenQueueEmpty_CallbackNotInvoked()
    {
        var queue = CreateQueue(Channel);
        bool callbackInvoked = false;

        await queue.Dequeue(async _ =>
        {
            callbackInvoked = true;
            return MakeReport(Guid.NewGuid().ToString());
        });

        Assert.That(callbackInvoked, Is.False);
    }

    [Test]
    public async Task Enqueue_MultipleItems_ProcessedInFifoOrder()
    {
        var queue = CreateQueue(Channel);
        var ids = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

        // Small delays so timecreatedutc ordering is deterministic.
        foreach (var id in ids)
        {
            await queue.Enqueue(MakeQueueItem(id));
            await Task.Delay(10);
        }

        var processed = new List<string>();
        for (int i = 0; i < ids.Length; i++)
        {
            await queue.Dequeue(async workItem =>
            {
                processed.Add(workItem.Id);
                return MakeReport(workItem.Id);
            });
        }

        Assert.That(processed, Is.EqualTo(ids));
    }

    // -------------------------------------------------------------------------
    // Status transitions (verifies the two-phase transaction fix)
    // -------------------------------------------------------------------------

    [Test]
    public async Task Dequeue_StatusIsProcessing_WhileCallbackIsRunning()
    {
        var queue = CreateQueue(Channel);
        var itemId = Guid.NewGuid().ToString();
        await queue.Enqueue(MakeQueueItem(itemId));

        WorkItemStatus statusDuringCallback = WorkItemStatus.Unknown;

        await queue.Dequeue(async workItem =>
        {
            // The claim transaction has already committed; a fresh connection
            // must see Processing, not Pending and not Completed yet.
            var observer = CreateQueue(workItem.Channel);
            var (_, s) = await observer.Get(workItem.Id);
            statusDuringCallback = s;
            return MakeReport(workItem.Id);
        });

        Assert.That(statusDuringCallback, Is.EqualTo(WorkItemStatus.Processing),
            "Status must be Processing while the callback runs (claim transaction committed before callback)");

        var (_, finalStatus) = await queue.Get(itemId);
        Assert.That(finalStatus, Is.EqualTo(WorkItemStatus.Completed));
    }

    // -------------------------------------------------------------------------
    // Failure / retry
    // -------------------------------------------------------------------------

    [Test]
    public async Task Dequeue_WhenCallbackThrows_StatusResetsToPending()
    {
        var queue = CreateQueue(Channel);
        var itemId = Guid.NewGuid().ToString();
        await queue.Enqueue(MakeQueueItem(itemId));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await queue.Dequeue(_ => throw new InvalidOperationException("boom"));
        });

        // Status must be back to Pending so the item can be retried.
        var (_, status) = await queue.Get(itemId);
        Assert.That(status, Is.EqualTo(WorkItemStatus.Pending));
    }

    [Test]
    public async Task Dequeue_WhenCallbackThrows_IncrementsRetryCount()
    {
        var queue = CreateQueue(Channel);
        var itemId = Guid.NewGuid().ToString();
        await queue.Enqueue(MakeQueueItem(itemId));

        // Swallow the rethrow — we just want the side-effect.
        try { await queue.Dequeue(_ => throw new InvalidOperationException()); } catch { }

        // Attempt a second successful dequeue.
        bool secondAttemptRan = false;
        await queue.Dequeue(async workItem =>
        {
            secondAttemptRan = true;
            return MakeReport(workItem.Id);
        });

        Assert.That(secondAttemptRan, Is.True,
            "Item should be retryable after failure (MaxRetries defaults to 2)");
    }

    [Test]
    public async Task Dequeue_AfterMaxRetries_ItemNotDequeued()
    {
        var queue = CreateQueue(Channel);
        var itemId = Guid.NewGuid().ToString();
        await queue.Enqueue(MakeQueueItem(itemId));

        // Exhaust all retries (MaxRetries = 2, so 3 attempts total).
        for (int i = 0; i < 3; i++)
        {
            try { await queue.Dequeue(_ => throw new InvalidOperationException()); } catch { }
        }

        bool callbackInvoked = false;
        await queue.Dequeue(async _ =>
        {
            callbackInvoked = true;
            return MakeReport(Guid.NewGuid().ToString());
        });

        Assert.That(callbackInvoked, Is.False,
            "Item must not be dequeued after RetryCount exceeds MaxRetries");
    }

    // -------------------------------------------------------------------------
    // Concurrency — only exercisable against a real DB with proper locking hints
    // -------------------------------------------------------------------------

    [Test]
    public async Task ConcurrentDequeue_EachItemProcessedExactlyOnce()
    {
        var queue = CreateQueue(Channel);
        const int itemCount = 5;
        var ids = Enumerable.Range(0, itemCount).Select(_ => Guid.NewGuid().ToString()).ToArray();
        foreach (var id in ids)
            await queue.Enqueue(MakeQueueItem(id));

        var processedIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Eight workers compete for five items.
        var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            for (int i = 0; i < itemCount; i++)
            {
                await CreateQueue(Channel).Dequeue(async workItem =>
                {
                    processedIds.Add(workItem.Id);
                    await Task.Delay(20); // simulate light work
                    return MakeReport(workItem.Id);
                });
            }
        }));

        await Task.WhenAll(workers);

        Assert.That(processedIds, Has.Count.EqualTo(itemCount),
            "Each item must be processed exactly once regardless of worker count");
        Assert.That(processedIds.Distinct().Count(), Is.EqualTo(itemCount),
            "No item must be processed twice");
    }

    // -------------------------------------------------------------------------
    // Garbage collection
    // -------------------------------------------------------------------------

    [Test]
    public async Task GarbageCollection_DoesNotThrow()
    {
        var queue = CreateQueue(Channel);
        await queue.Enqueue(MakeQueueItem(Guid.NewGuid().ToString()));
        await queue.Dequeue(async workItem => MakeReport(workItem.Id));

        // Nothing is old enough to be deleted here, but the queries must execute cleanly.
        Assert.DoesNotThrowAsync(async () => await queue.GarbageCollection());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static QueueItem MakeQueueItem(string id) => new()
    {
        Id = id,
        ReportTemplate = Array.Empty<byte>(),
        Data = null!
    };

    private static GeneratedReportPoco MakeReport(string id) => new()
    {
        Id = id,
        Format = "pdf",
        GeneratedUtc = DateTime.UtcNow,
        FileContent = new byte[] { 1, 2, 3 },
        FileName = "test.pdf"
    };
}
