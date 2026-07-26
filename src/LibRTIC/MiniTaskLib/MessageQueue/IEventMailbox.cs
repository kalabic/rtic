namespace LibRTIC.MiniTaskLib.MessageQueue;


internal interface IEventMailbox : IDisposable
{
    void CloseMailbox();

    TaskWithEvents? GetAwaiter();

    void Run();

    void Run(CancellationToken cancellation);

    TaskWithEvents RunAsync();

    void DelayedAction(Action action, int delayMs);

    void RepeatAction(Action action, int delayMs);
}
