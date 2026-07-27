using LibRTIC.Conversation.Control;
using OpenAI.Realtime;

namespace LibRTIC.Conversation.OpenAI.Realtime;

#pragma warning disable OPENAI002

internal sealed class OpenAIRealtimeCommandTransport
    : IRTICConversationCommandTransport
{
    private readonly RealtimeSessionClient _session;
    private long _nextEventId;

    public OpenAIRealtimeCommandTransport(RealtimeSessionClient session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public Task RequestResponseAsync(
        RTICResponseRequest request,
        CancellationToken cancellationToken)
    {
        RealtimeClientCommandResponseCreate command =
            OpenAIRealtimeCommandFactory.CreateResponse(
                request,
                CreateEventId());
        return _session.SendCommandAsync(command, cancellationToken);
    }

    public Task CancelResponseAsync(
        string? responseId,
        CancellationToken cancellationToken)
    {
        RealtimeClientCommandResponseCancel command =
            OpenAIRealtimeCommandFactory.CreateCancel(
                responseId,
                CreateEventId());
        return _session.SendCommandAsync(command, cancellationToken);
    }

    public Task TruncateOutputAsync(
        RTICOutputCursor cursor,
        TimeSpan playedThrough,
        CancellationToken cancellationToken)
    {
        RealtimeClientCommandConversationItemTruncate command =
            OpenAIRealtimeCommandFactory.CreateTruncate(
                cursor,
                playedThrough,
                CreateEventId());
        return _session.SendCommandAsync(command, cancellationToken);
    }

    private string CreateEventId()
        => $"rtic_control_{Interlocked.Increment(ref _nextEventId)}";
}

internal static class OpenAIRealtimeCommandFactory
{
    public static RealtimeClientCommandResponseCreate CreateResponse(
        RTICResponseRequest request,
        string eventId)
    {
        RealtimeClientCommandResponseCreate command = new()
        {
            EventId = eventId,
        };
        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            command.ResponseOptions = new RealtimeResponseOptions
            {
                Instructions = request.Instructions,
            };
        }
        return command;
    }

    public static RealtimeClientCommandResponseCancel CreateCancel(
        string? responseId,
        string eventId)
    {
        RealtimeClientCommandResponseCancel command = new()
        {
            EventId = eventId,
        };
        if (!string.IsNullOrWhiteSpace(responseId))
        {
            command.ResponseId = responseId;
        }
        return command;
    }

    public static RealtimeClientCommandConversationItemTruncate CreateTruncate(
        RTICOutputCursor cursor,
        TimeSpan playedThrough,
        string eventId)
        => new(
            cursor.ItemId,
            cursor.ContentIndex,
            playedThrough)
        {
            EventId = eventId,
        };
}

#pragma warning restore OPENAI002
