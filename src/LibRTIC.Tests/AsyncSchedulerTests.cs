using LibRTIC.MiniTaskLib.Base;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace LibRTIC.Tests;

public sealed class AsyncSchedulerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ScheduledTaskUsesInjectedFunctionTaskAndUnwrappedLifetime()
    {
        FakeTimeProvider time = new();
        int invocationCount = 0;
        ScheduledTask task = new(
            () => Interlocked.Increment(ref invocationCount),
            100,
            false,
            time);

        Task innerTask;

        try
        {
            task.Start(TaskScheduler.Default);
            innerTask = await task.WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);

            TaskCreationOptions expectedOptions =
                TaskCreationOptions.DenyChildAttach |
                TaskCreationOptions.HideScheduler |
                TaskCreationOptions.RunContinuationsAsynchronously;

            Assert.True(task.IsCompletedSuccessfully);
            Assert.Equal(
                expectedOptions,
                task.CreationOptions & expectedOptions);
            Assert.False(innerTask.IsCompleted);
            Assert.False(task.Completion.IsCompleted);

            time.Advance(TimeSpan.FromMilliseconds(100));

            await task.Completion.WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);

            Assert.True(innerTask.IsCompletedSuccessfully);
            Assert.Equal(1, Volatile.Read(ref invocationCount));
        }
        finally
        {
            if (task.Completion.IsCompleted)
            {
                task.Dispose();
            }
            else
            {
                _ = task.RequestCancellation();

                try
                {
                    await task.Completion;
                }
                catch (OperationCanceledException)
                { }

                task.Dispose();
            }
        }
    }

    [Fact]
    public async Task OneShotRunsOnceAtItsDeadline()
    {
        FakeTimeProvider time = new();
        using Scheduler scheduler = new(time);
        int invocationCount = 0;

        ScheduledTask task = Assert.IsType<ScheduledTask>(
            scheduler.Execute(
                () => Interlocked.Increment(ref invocationCount),
                100));

        await AwaitLauncher(task);

        time.Advance(TimeSpan.FromMilliseconds(99));
        Assert.Equal(0, Volatile.Read(ref invocationCount));
        Assert.False(task.Completion.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await task.Completion.WaitAsync(
            TestTimeout,
            TestContext.Current.CancellationToken);
        await WaitForScheduledCountAsync(scheduler, 0);

        Assert.Equal(1, Volatile.Read(ref invocationCount));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, Volatile.Read(ref invocationCount));
    }

    [Fact]
    public async Task PeriodicTicksUseOneSerializedExecutionLoop()
    {
        FakeTimeProvider time = new();
        using Scheduler scheduler = new(time);
        using ManualResetEventSlim releaseFirstTick = new(false);
        TaskCompletionSource firstTickStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource firstTickCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondTickStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int activeActions = 0;
        int maximumActiveActions = 0;
        int invocationCount = 0;

        ScheduledTask task = Assert.IsType<ScheduledTask>(
            scheduler.Execute(
                () =>
                {
                    int active = Interlocked.Increment(ref activeActions);
                    int invocation =
                        Interlocked.Increment(ref invocationCount);
                    SetMaximum(ref maximumActiveActions, active);

                    try
                    {
                        if (invocation == 1)
                        {
                            firstTickStarted.TrySetResult();

                            if (!releaseFirstTick.Wait(TestTimeout))
                            {
                                throw new TimeoutException(
                                    "The first periodic action was not released.");
                            }
                        }
                        else if (invocation == 2)
                        {
                            secondTickStarted.TrySetResult();
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref activeActions);

                        if (invocation == 1)
                        {
                            firstTickCompleted.TrySetResult();
                        }
                    }
                },
                100,
                true));

        await AwaitLauncher(task);

        try
        {
            Task firstAdvance = Task.Run(
                () => time.Advance(TimeSpan.FromMilliseconds(100)),
                TestContext.Current.CancellationToken);
            await firstTickStarted.Task.WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, Volatile.Read(ref invocationCount));
            Assert.Equal(1, Volatile.Read(ref maximumActiveActions));

            releaseFirstTick.Set();
            await firstTickCompleted.Task.WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);
            await firstAdvance.WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);

            time.Advance(TimeSpan.FromMilliseconds(100));
            await secondTickStarted.Task.WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);

            Assert.Equal(2, Volatile.Read(ref invocationCount));
            Assert.Equal(1, Volatile.Read(ref maximumActiveActions));
        }
        finally
        {
            releaseFirstTick.Set();
            _ = task.RequestCancellation();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => task.Completion);
        await WaitForScheduledCountAsync(scheduler, 0);
    }

    [Fact]
    public async Task SameDelegateCanBeScheduledMoreThanOnce()
    {
        FakeTimeProvider time = new();
        using Scheduler scheduler = new(time);
        int invocationCount = 0;
        Action action = () => Interlocked.Increment(ref invocationCount);

        ScheduledTask first = Assert.IsType<ScheduledTask>(
            scheduler.Execute(action, 100));
        ScheduledTask second = Assert.IsType<ScheduledTask>(
            scheduler.Execute(action, 100));

        await Task.WhenAll(AwaitLauncher(first), AwaitLauncher(second));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, scheduler.ScheduledCount);

        time.Advance(TimeSpan.FromMilliseconds(100));

        await Task.WhenAll(first.Completion, second.Completion)
            .WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);
        await WaitForScheduledCountAsync(scheduler, 0);

        Assert.Equal(2, Volatile.Read(ref invocationCount));
    }

    [Fact]
    public async Task DisposeCancelsOutstandingSchedulesAndRejectsNewOnes()
    {
        FakeTimeProvider time = new();
        Scheduler scheduler = new(time);
        int invocationCount = 0;
        ScheduledTask task = Assert.IsType<ScheduledTask>(
            scheduler.Execute(
                () => Interlocked.Increment(ref invocationCount),
                100,
                true));

        await AwaitLauncher(task);
        Assert.Equal(1, scheduler.ScheduledCount);

        await Task.Run(
                scheduler.Dispose,
                TestContext.Current.CancellationToken)
            .WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);

        Assert.True(task.IsCancellationRequested);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => task.Completion.WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken));
        await WaitForScheduledCountAsync(scheduler, 0);

        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0, Volatile.Read(ref invocationCount));
        Assert.Null(scheduler.Execute(() => { }, 100));
    }

    [Fact]
    public async Task ActionFaultIsObservedReportedAndRemoved()
    {
        FakeTimeProvider time = new();
        TaskCompletionSource<Exception> reportedFault =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        using Scheduler scheduler = new(
            time,
            exception => reportedFault.TrySetResult(exception));
        InvalidOperationException expected =
            new("Scheduled action failed.");
        ScheduledTask task = Assert.IsType<ScheduledTask>(
            scheduler.Execute(() => throw expected, 100));

        await AwaitLauncher(task);
        time.Advance(TimeSpan.FromMilliseconds(100));

        InvalidOperationException completionFault =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => task.Completion);
        Exception observedFault =
            await reportedFault.Task.WaitAsync(
                TestTimeout,
                TestContext.Current.CancellationToken);
        await WaitForScheduledCountAsync(scheduler, 0);

        Assert.Same(expected, completionFault);
        Assert.Same(expected, observedFault);
    }

    [Fact]
    public async Task CompletionAndDisposalRaceLeavesNoTrackedSchedules()
    {
        for (int iteration = 0; iteration < 32; iteration++)
        {
            FakeTimeProvider time = new();
            Scheduler scheduler = new(time);
            int invocationCount = 0;
            ScheduledTask task = Assert.IsType<ScheduledTask>(
                scheduler.Execute(
                    () => Interlocked.Increment(ref invocationCount),
                    1));

            await AwaitLauncher(task);

            Task advance = Task.Run(
                () => time.Advance(TimeSpan.FromMilliseconds(1)),
                TestContext.Current.CancellationToken);
            Task dispose = Task.Run(
                scheduler.Dispose,
                TestContext.Current.CancellationToken);

            await Task.WhenAll(advance, dispose)
                .WaitAsync(
                    TestTimeout,
                    TestContext.Current.CancellationToken);

            try
            {
                await task.Completion.WaitAsync(
                    TestTimeout,
                    TestContext.Current.CancellationToken);
            }
            catch (OperationCanceledException)
            { }

            await WaitForScheduledCountAsync(scheduler, 0);

            Assert.True(task.Completion.IsCompleted);
            Assert.InRange(Volatile.Read(ref invocationCount), 0, 1);
        }
    }

    [Fact]
    public void IntervalsAndDependenciesAreValidated()
    {
        FakeTimeProvider time = new();
        using Scheduler scheduler = new(time);

        Assert.Throws<ArgumentOutOfRangeException>(
            (Action)(() => { _ = scheduler.Execute(() => { }, 0); }));
        Assert.Throws<ArgumentOutOfRangeException>(
            (Action)(() => { _ = scheduler.Execute(() => { }, -1, true); }));
        Assert.Throws<ArgumentNullException>(
            (Action)(() => { _ = scheduler.Execute(null!, 1); }));
        Assert.Throws<ArgumentNullException>(
            () => new Scheduler(null!));
    }

    private static async Task AwaitLauncher(ScheduledTask task)
    {
        _ = await task.WaitAsync(
            TestTimeout,
            TestContext.Current.CancellationToken);
    }

    private static async Task WaitForScheduledCountAsync(
        Scheduler scheduler,
        int expectedCount)
    {
        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
        timeout.CancelAfter(TestTimeout);

        while (scheduler.ScheduledCount != expectedCount)
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private static void SetMaximum(ref int target, int value)
    {
        int current = Volatile.Read(ref target);

        while (value > current)
        {
            int previous = Interlocked.CompareExchange(
                ref target,
                value,
                current);

            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }
}
