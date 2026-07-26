using LibRTIC.Conversation.UpdatesReceiver;
using System.Diagnostics;
using Xunit;

namespace LibRTIC.Tests;

public sealed class ConversationCancellationTests
{
    [Fact]
    public void CancelMicrophone_DoesNotWaitForBlockingCallback()
    {
        using var cancellation = new ConversationCancellation();
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using CancellationTokenRegistration registration =
            cancellation.MicrophoneToken.Register(() =>
            {
                callbackEntered.Set();
                releaseCallback.Wait();
            });

        try
        {
            var stopwatch = Stopwatch.StartNew();

            cancellation.CancelMicrophone();

            stopwatch.Stop();
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Cancellation blocked for {stopwatch.Elapsed}.");
            Assert.True(
                callbackEntered.Wait(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            releaseCallback.Set();
        }
    }

    [Fact]
    public void CancelConversation_PreservesWebSocketForGracefulShutdown()
    {
        using var cancellation = new ConversationCancellation();

        cancellation.CancelConversation();
        cancellation.CancelConversation();

        Assert.True(cancellation.ShellToken.IsCancellationRequested);
        Assert.True(cancellation.SpeechToken.IsCancellationRequested);
        Assert.True(cancellation.MicrophoneToken.IsCancellationRequested);
        Assert.False(cancellation.WebSocketToken.IsCancellationRequested);

        cancellation.CancelWebSocket();

        Assert.True(cancellation.WebSocketToken.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_BaseStatePreventsFurtherCancellationRequests()
    {
        var cancellation = new ConversationCancellation();
        CancellationToken webSocketToken = cancellation.WebSocketToken;

        cancellation.Dispose();
        cancellation.CancelWebSocket();

        Assert.True(cancellation.IsDisposed);
        Assert.False(webSocketToken.IsCancellationRequested);
    }
}
