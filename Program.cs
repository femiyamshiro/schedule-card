using System.IO;
using System.Threading;
using System.Windows;

namespace ScheduleCard;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--verify", var testDirectory])
        {
            CardVerification.Run(testDirectory);
            return;
        }

        using var instance = new Mutex(true, @"Local\Fenok.ScheduleCard", out var first);
        if (!first)
        {
            if (!args.Contains("--background"))
                NativeWindow.ShowExistingCard();
            return;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScheduleCard");
        var card = new CardWindow(data) { ShowActivated = !args.Contains("--background") };
        app.Run(card);
    }
}
