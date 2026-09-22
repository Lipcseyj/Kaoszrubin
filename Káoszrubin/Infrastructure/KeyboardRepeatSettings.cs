using System.Runtime.InteropServices;

namespace KaoszRubin.Infrastructure;

internal static class KeyboardRepeatSettings
{
    private const uint SPI_GETKEYBOARDDELAY = 0x0016;
    private const uint SPI_SETKEYBOARDDELAY = 0x0017;

    private static uint? _originalDelay;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        ref uint pvParam,
        uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        IntPtr pvParam,
        uint fWinIni);

    public static void ApplyFastestDelay()
    {
        if (_originalDelay is null)
        {
            uint currentDelay = 0;

            if (SystemParametersInfo(
                    SPI_GETKEYBOARDDELAY,
                    0,
                    ref currentDelay,
                    0))
            {
                _originalDelay = currentDelay;
            }
        }

        // 0 = legrövidebb repeat delay, kb. 250 ms
        SystemParametersInfo(
            SPI_SETKEYBOARDDELAY,
            0,
            IntPtr.Zero,
            0);
    }

    public static void Restore()
    {
        if (_originalDelay is not { } delay)
            return;

        SystemParametersInfo(
            SPI_SETKEYBOARDDELAY,
            delay,
            IntPtr.Zero,
            0);

        _originalDelay = null;
    }
}