using LibRTIC.Config;
using System.Text;
using LibRTIC_Win.BasicDevices;

namespace MiniRTIC;

// 'game_music_loop_6' is a free sample from https://pixabay.com/sound-effects/game-music-loop-6-144641/
// 'Hello there' is a free sample from https://pixabay.com/sound-effects/quothello-therequot-158832/

public partial class Program
{
    static private WinConsoleWriter Output = new();

    static private WinConsoleAudio? AudioOutput = null;

    static private void InitializeEnvironment()
    {
        // Enable Unicode in Windows console.
        Console.OutputEncoding = Encoding.UTF8;

        ConsoleCancelEventHandler sessionCanceler = (sender, e) =>
        {
            exitSource.Cancel();
            Console.Write("[ Ctrl-C ]");
            e.Cancel = true; // Execution continues after the delegate.
        };
        Console.CancelKeyPress += sessionCanceler;

        // 'game_music_loop_6' sample is playing on speaker while session is being created.
        byte[] inactiveStateMusic = Properties.Resources.game_music_loop_6;

        // 'Hello there' sample is enqueued into audio input stream when session starts.
        byte[] helloBuffer = Properties.Resources.hello_there;

        AudioOutput = new WinConsoleAudio(Output.Info, ConversationSessionConfig.AudioFormat, exitSource.Token);
        AudioOutput.Start(inactiveStateMusic, helloBuffer);
        Output.AddStateEventHandler(AudioOutput.HandleEvent);
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
