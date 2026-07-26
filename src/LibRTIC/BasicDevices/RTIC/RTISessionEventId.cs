namespace LibRTIC.BasicDevices.RTIC;

/// <summary>
/// Important events about general state of conversation session.
/// </summary>
public enum RTISessionEventId
{
    ConnectingStarted,
    AnswerAccepted,
    MediaAccepted,
    ConnectingFailed,
    OperationFailed,
    SessionStarted,
    SessionFinished,
    ItemStarted,
    ItemFinished,
}
