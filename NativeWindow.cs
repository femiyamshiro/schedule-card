using System.Runtime.InteropServices;

namespace ScheduleCard;

internal static class NativeWindow
{
    private const int ExtendedStyle = -20;
    private const int ToolWindow = 0x00000080;
    private const int AppWindow = 0x00040000;
    internal const int ShowCardMessage = 0x8038;

    internal static void Configure(IntPtr window)
    {
        var style = GetWindowLong(window, ExtendedStyle);
        SetWindowLong(window, ExtendedStyle, (style | ToolWindow) & ~AppWindow);
    }

    internal static void ShowExistingCard()
    {
        var card = FindWindow(null, "课表卡片");
        if (card == IntPtr.Zero)
            return;

        SetForegroundWindow(card);
        PostMessage(card, ShowCardMessage, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? windowClass, string windowTitle);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
