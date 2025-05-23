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
    SessionStarted,
    SessionFinished,
    ItemStarted,
    ItemFinished,
}
