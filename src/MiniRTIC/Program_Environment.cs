using LibRTIC.Config;
using System.Text;
using LibRTIC_Win.BasicDevices;

namespace MiniRTIC;

public partial class Program
{
    static private WinConsoleWriter COut = new();

    static private WinConsoleAudio? ConsoleAudio = null;

    static private void InitializeEnvironment()
    {
        // Enable Unicode in Windows console.
        Console.OutputEncoding = Encoding.UTF8;

        ConsoleCancelEventHandler sessionCanceler = (sender, e) =>
        {
            exitSource.Cancel();

#if DEBUG
            Console.Write("[ Ctrl-C ]");
#endif
            e.Cancel = true; // Execution continues after the delegate.
        };
        Console.CancelKeyPress += sessionCanceler;

        // 'game_music_loop_6' sample is playing on speaker while session is being created.
        // It is a free sample from https://pixabay.com/sound-effects/game-music-loop-6-144641/
        byte[] inactiveStateMusic = Properties.Resources.game_music_loop_6;

        // 'Hello there' sample is enqueued into audio input stream when session starts.
        // It is a free sample from https://pixabay.com/sound-effects/quothello-therequot-158832/
        byte[] helloBuffer = Properties.Resources.hello_there;

        ConsoleAudio = new WinConsoleAudio(COut.Info, ConversationSessionConfig.AudioFormat, exitSource.Token);
        ConsoleAudio.Start(inactiveStateMusic, helloBuffer);
        COut.AddStateEventHandler(ConsoleAudio.HandleEvent);
    }

    private static ConsoleKeyInfo WaitForKey(CancellationToken programCancellation)
    {
        while (!programCancellation.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                return Console.ReadKey(true);
            }
            Thread.Sleep(50);
        }
        return new ConsoleKeyInfo((char)0, (ConsoleKey)0, false, false, false);
    }
}
