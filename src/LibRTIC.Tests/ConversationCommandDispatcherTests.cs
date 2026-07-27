using DotBase.Log;
using LibRTIC.Conversation;
using LibRTIC.Conversation.Control;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Xunit;

namespace LibRTIC.Tests;

public sealed class ConversationCommandDispatcherTests
{
    [Fact]
    public async Task Interrupt_AfterStartIgnoresCallerCancellationAndPrecedesLaterRequest()
    {
        using CancellationTokenSource forceCancellation = new();
        using CancellationTokenSource callerCancellation = new();
        TaskCompletionSource cancelEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCancel =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTransport transport = new()
        {
            OnCancel = async (_, _) =>
            {
                cancelEntered.TrySetResult();
                await releaseCancel.Task;
            },
        };
        using ConversationCommandDispatcher dispatcher =
            CreateDispatcher(transport, forceCancellation.Token);
        RTICOutputCursor cursor = ObserveActiveAudio(dispatcher);

        Task interruption = dispatcher.InterruptOutputAsync(
            new RTICOutputInterruption(cursor, TimeSpan.FromMilliseconds(20), true),
            callerCancellation.Token);
        await cancelEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        callerCancellation.Cancel();
        Task response = dispatcher.RequestResponseAsync(
            new RTICResponseRequest("hello"),
            CancellationToken.None);

        Assert.Equal(["cancel:response-1"], transport.Commands);

        releaseCancel.TrySetResult();
        await interruption;
        await response;

        Assert.Equal(
            [
                "cancel:response-1",
                "truncate:item-1:0:20",
                "response:hello",
            ],
            transport.Commands);
    }

    [Fact]
    public async Task Interrupt_TargetsCursorResponseWhenResponsesOverlap()
    {
        using CancellationTokenSource forceCancellation = new();
        FakeTransport transport = new();
        using ConversationCommandDispatcher dispatcher =
            CreateDispatcher(transport, forceCancellation.Token);
        RTICOutputCursor first = ObserveActiveAudio(dispatcher);
        dispatcher.Observe(ResponseStarted("response-2"));

        await dispatcher.InterruptOutputAsync(
            new RTICOutputInterruption(first, TimeSpan.Zero, true),
            CancellationToken.None);

        Assert.Equal("cancel:response-1", transport.Commands[0]);
        Assert.DoesNotContain("cancel:response-2", transport.Commands);
    }

    [Fact]
    public async Task Interrupt_CompletedResponseSkipsCancelButStillTruncates()
    {
        using CancellationTokenSource forceCancellation = new();
        FakeTransport transport = new();
        using ConversationCommandDispatcher dispatcher =
            CreateDispatcher(transport, forceCancellation.Token);
        RTICOutputCursor cursor = ObserveActiveAudio(dispatcher);
        dispatcher.Observe(ResponseCompleted("response-1"));

        await dispatcher.InterruptOutputAsync(
            new RTICOutputInterruption(
                cursor,
                TimeSpan.FromMilliseconds(125),
                true),
            CancellationToken.None);

        Assert.Equal(["truncate:item-1:0:125"], transport.Commands);
    }

    [Fact]
    public async Task Interrupt_BothSendFailuresAreReportedTogether()
    {
        using CancellationTokenSource forceCancellation = new();
        FakeTransport transport = new()
        {
            OnCancel = (_, _) => throw new IOException("cancel failed"),
            OnTruncate = (_, _, _) => throw new IOException("truncate failed"),
        };
        using ConversationCommandDispatcher dispatcher =
            CreateDispatcher(transport, forceCancellation.Token);
        RTICOutputCursor cursor = ObserveActiveAudio(dispatcher);

        RTICConversationControlException exception =
            await Assert.ThrowsAsync<RTICConversationControlException>(
                () => dispatcher.InterruptOutputAsync(
                    new RTICOutputInterruption(cursor, TimeSpan.Zero, true),
                    CancellationToken.None));

        Assert.Equal(
            RTICConversationControlOperation.InterruptOutput,
            exception.Operation);
        AggregateException aggregate =
            Assert.IsType<AggregateException>(exception.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Equal(
            ["cancel:response-1", "truncate:item-1:0:0"],
            transport.Commands);
    }

    [Fact]
    public async Task CallerCancellationWhileQueuedSkipsProviderCommand()
    {
        using CancellationTokenSource forceCancellation = new();
        using CancellationTokenSource queuedCancellation = new();
        TaskCompletionSource firstRequestEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstRequest =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTransport transport = new()
        {
            OnRequest = async (request, _) =>
            {
                if (request.Instructions == "first")
                {
                    firstRequestEntered.TrySetResult();
                    await releaseFirstRequest.Task;
                }
            },
        };
        using ConversationCommandDispatcher dispatcher =
            CreateDispatcher(transport, forceCancellation.Token);

        Task first = dispatcher.RequestResponseAsync(
            new RTICResponseRequest("first"),
            CancellationToken.None);
        await firstRequestEntered.Task.WaitAsync(
            TestContext.Current.CancellationToken);
        Task queued = dispatcher.RequestResponseAsync(
            new RTICResponseRequest("queued"),
            queuedCancellation.Token);

        queuedCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        releaseFirstRequest.TrySetResult();
        await first;

        Assert.Equal(["response:first"], transport.Commands);
    }

    [Fact]
    public async Task ResponseSendFailureUsesNeutralControlException()
    {
        using CancellationTokenSource forceCancellation = new();
        FakeTransport transport = new()
        {
            OnRequest = (_, _) => throw new IOException("send failed"),
        };
        using ConversationCommandDispatcher dispatcher =
            CreateDispatcher(transport, forceCancellation.Token);

        RTICConversationControlException exception =
            await Assert.ThrowsAsync<RTICConversationControlException>(
                () => dispatcher.RequestResponseAsync(
                    new RTICResponseRequest(),
                    CancellationToken.None));

        Assert.Equal(
            RTICConversationControlOperation.RequestResponse,
            exception.Operation);
        Assert.IsType<IOException>(exception.InnerException);
    }

    [Fact]
    public async Task UnknownCursorIsRejectedBeforeProviderCommands()
    {
        using CancellationTokenSource forceCancellation = new();
        FakeTransport transport = new();
        using ConversationCommandDispatcher dispatcher =
            CreateDispatcher(transport, forceCancellation.Token);
        RTICOutputCursor cursor = new("response-x", "item-x", 0, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.InterruptOutputAsync(
                new RTICOutputInterruption(cursor, TimeSpan.Zero, true),
                CancellationToken.None));

        Assert.Empty(transport.Commands);
    }

    private static ConversationCommandDispatcher CreateDispatcher(
        FakeTransport transport,
        CancellationToken forceCancellation)
    {
        ConversationCommandDispatcher dispatcher = new(
            new ConsoleInfo(EventLevel.Critical),
            forceCancellation);
        dispatcher.Start(transport);
        return dispatcher;
    }

    private static RTICOutputCursor ObserveActiveAudio(
        ConversationCommandDispatcher dispatcher)
    {
        RTICOutputCursor cursor = new("response-1", "item-1", 2, 0);
        dispatcher.Observe(ResponseStarted(cursor.ResponseId));
        dispatcher.Observe(
            new RTICOutputAudioDelta(
                cursor.ResponseId,
                cursor.ItemId,
                cursor.OutputIndex,
                cursor.ContentIndex,
                new byte[] { 0, 0 }));
        return cursor;
    }

    private static RTICResponseStarted ResponseStarted(string responseId)
        => new(Response(responseId, RTICResponseStatus.InProgress));

    private static RTICResponseCompleted ResponseCompleted(string responseId)
        => new(Response(responseId, RTICResponseStatus.Completed));

    private static RTICResponse Response(
        string responseId,
        RTICResponseStatus status)
        => new(responseId, status, [], []);

    private sealed class FakeTransport : IRTICConversationCommandTransport
    {
        private readonly ConcurrentQueue<string> _commands = new();

        public Func<RTICResponseRequest, CancellationToken, Task>? OnRequest { get; init; }

        public Func<string?, CancellationToken, Task>? OnCancel { get; init; }

        public Func<RTICOutputCursor, TimeSpan, CancellationToken, Task>? OnTruncate
        {
            get;
            init;
        }

        public IReadOnlyList<string> Commands => _commands.ToArray();

        public Task RequestResponseAsync(
            RTICResponseRequest request,
            CancellationToken cancellationToken)
        {
            _commands.Enqueue($"response:{request.Instructions}");
            return OnRequest?.Invoke(request, cancellationToken)
                ?? Task.CompletedTask;
        }

        public Task CancelResponseAsync(
            string? responseId,
            CancellationToken cancellationToken)
        {
            _commands.Enqueue($"cancel:{responseId}");
            return OnCancel?.Invoke(responseId, cancellationToken)
                ?? Task.CompletedTask;
        }

        public Task TruncateOutputAsync(
            RTICOutputCursor cursor,
            TimeSpan playedThrough,
            CancellationToken cancellationToken)
        {
            _commands.Enqueue(
                $"truncate:{cursor.ItemId}:{cursor.ContentIndex}:{playedThrough.TotalMilliseconds:0}");
            return OnTruncate?.Invoke(cursor, playedThrough, cancellationToken)
                ?? Task.CompletedTask;
        }
    }
}
