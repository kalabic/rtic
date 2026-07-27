namespace LibRTIC.MiniTaskLib.Queues;


internal interface IActionQueue : IActionQueueWriter, IDisposable
{
    void CompleteAdding();

    TaskWithEvents? GetAwaiter();

    void Run();

    void Run(CancellationToken cancellation);

    TaskWithEvents RunAsync();

    void DelayedAction(Action action, int delayMs);

    void RepeatAction(Action action, int delayMs);
}
