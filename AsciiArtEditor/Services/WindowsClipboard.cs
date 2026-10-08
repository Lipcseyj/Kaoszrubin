using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AsciiArtEditor.Services;

internal static class WindowsClipboard
{
    private const uint UnicodeText = 13;
    private const uint MoveableMemory = 0x0002;

    public static bool TryCopy(string text, out string? error)
    {
        error = null;
        var memory = IntPtr.Zero;
        var clipboardOpened = false;
        try
        {
            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            memory = GlobalAlloc(MoveableMemory, (nuint)bytes.Length);
            if (memory == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var buffer = GlobalLock(memory);
            if (buffer == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            if (!OpenClipboard(GetConsoleWindow()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            clipboardOpened = true;
            if (!EmptyClipboard() || SetClipboardData(UnicodeText, memory) == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            memory = IntPtr.Zero;
            return true;
        }
        catch (Win32Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (clipboardOpened)
                CloseClipboard();
            if (memory != IntPtr.Zero)
                GlobalFree(memory);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
}
