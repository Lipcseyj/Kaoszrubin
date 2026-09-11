using System.Runtime.InteropServices;

namespace KaoszRubin.Tests.Coop;

/// <summary>
/// Opcionalis bemenetvezerles a felig manualis coop szcenariokhoz. Alapertelmezesben nem tortenik
/// automatikus billentyubeadas: a szcenariokat ember vezerli a ket konzolablakban.
/// </summary>
internal interface ICoopKeyScript
{
    void SendKeys(CoopHarnessProcess target, params char[] characters);
}

internal sealed class NoOpCoopKeyScript : ICoopKeyScript
{
    public static readonly NoOpCoopKeyScript Instance = new();

    private NoOpCoopKeyScript()
    {
    }

    public void SendKeys(CoopHarnessProcess target, params char[] characters)
    {
    }
}

/// <summary>
/// Windows-specifikus implementacio: a cel gyermek konzoljara csatlakozva injektal billentyuket.
/// Csak akkor hasznald, ha egy szcenario kifejezetten automatizalt bemenetet ker.
/// </summary>
internal sealed class WindowsConsoleKeyScript : ICoopKeyScript
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const ushort KeyEvent = 0x0001;

    public void SendKeys(CoopHarnessProcess target, params char[] characters)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (characters is null || characters.Length == 0) return;
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("A konzolbemenet injektalas csak Windowson tamogatott.");

        if (!FreeConsole()) throw new InvalidOperationException("A sajat konzol levalasztasa sikertelen.");
        try
        {
            if (!AttachConsole((uint)target.Id))
                throw new InvalidOperationException("A cel folyamat konzolja nem erheto el.");

            var handle = CreateFile("CONIN$", GenericRead | GenericWrite, FileShareRead | FileShareWrite,
                IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                throw new InvalidOperationException("A CONIN$ nem nyithato meg.");

            var records = new InputRecord[characters.Length * 2];
            for (var index = 0; index < characters.Length; index++)
            {
                records[index * 2] = CreateKeyRecord(characters[index], keyDown: true);
                records[index * 2 + 1] = CreateKeyRecord(characters[index], keyDown: false);
            }

            if (!WriteConsoleInput(handle, records, (uint)records.Length, out _))
                throw new InvalidOperationException("A billentyuesemenyek irasa sikertelen.");
        }
        finally
        {
            FreeConsole();
        }
    }

    private static InputRecord CreateKeyRecord(char character, bool keyDown) => new()
    {
        EventType = KeyEvent,
        KeyEvent = new KeyEventRecord
        {
            BKeyDown = keyDown,
            WRepeatCount = 1,
            WVirtualKeyCode = 0,
            WVirtualScanCode = 0,
            UnicodeChar = character,
            DwControlKeyState = 0
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct InputRecord
    {
        public ushort EventType;
        public KeyEventRecord KeyEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyEventRecord
    {
        [MarshalAs(UnmanagedType.Bool)] public bool BKeyDown;
        public ushort WRepeatCount;
        public ushort WVirtualKeyCode;
        public ushort WVirtualScanCode;
        public char UnicodeChar;
        public uint DwControlKeyState;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteConsoleInput(IntPtr consoleInput, InputRecord[] buffer, uint length,
        out uint eventsWritten);
}
