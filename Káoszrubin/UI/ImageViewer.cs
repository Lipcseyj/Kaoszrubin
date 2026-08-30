using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace KaoszRubin.UI;

public static class ImageViewer
{
    private static Form? _form;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, IntPtr processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint sourceThreadId, uint targetThreadId, bool attach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr windowHandle);

    public static string FileNameForLevel(string levelName)
    {
        var normalized = levelName.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(character))
                result.Append(char.ToLowerInvariant(character));
        return result.Append(".png").ToString();
    }

    public static bool Show(string fileName)
    {
        if (!File.Exists(fileName)) return false;
        using var ready = new ManualResetEventSlim();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var image = Image.FromFile(fileName);
                _form = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    BackColor = Color.Black,
                    StartPosition = FormStartPosition.CenterScreen,
                    Width = 1000,
                    Height = 700,
                    TopMost = true,
                    KeyPreview = true
                };
                var pictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = image
                };
                _form.Controls.Add(pictureBox);
                _form.KeyDown += (_, _) => _form.Close();
                _form.MouseDown += (_, _) => _form.Close();
                pictureBox.MouseDown += (_, _) => _form.Close();
                _form.Shown += (_, _) =>
                {
                    // A konzol és a külön STA UI-szál között a Windows időnként megtagadja az egyszerű
                    // SetForegroundWindow hívást. A message loop első körében, ideiglenesen összekapcsolt
                    // input queue-val aktiváljuk az ablakot, majd rövid ideig újrapróbáljuk, ha szükséges.
                    _form.BeginInvoke(() => FocusViewerWindow(_form));
                    var focusRetry = new System.Windows.Forms.Timer { Interval = 100 };
                    var attempts = 0;
                    focusRetry.Tick += (_, _) =>
                    {
                        if (_form is null || _form.IsDisposed || _form.ContainsFocus || ++attempts >= 5)
                        {
                            focusRetry.Stop();
                            focusRetry.Dispose();
                            return;
                        }
                        FocusViewerWindow(_form);
                    };
                    focusRetry.Start();
                };
                ready.Set();
                System.Windows.Forms.Application.Run(_form);
                _form = null;
            }
            catch (Exception exception)
            {
                failure = exception;
                ready.Set();
            }
        }) { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (failure is not null || _form is null) return false;
        thread.Join();
        return true;
    }

    private static void FocusViewerWindow(Form form)
    {
        if (form.IsDisposed || !form.IsHandleCreated) return;
        var foreground = GetForegroundWindow();
        var foregroundThreadId = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, IntPtr.Zero);
        var viewerThreadId = GetCurrentThreadId();
        var attached = foregroundThreadId != 0 && foregroundThreadId != viewerThreadId &&
                       AttachThreadInput(viewerThreadId, foregroundThreadId, true);
        try
        {
            form.TopMost = true;
            BringWindowToTop(form.Handle);
            form.Activate();
            SetForegroundWindow(form.Handle);
            SetFocus(form.Handle);
        }
        finally
        {
            if (attached) AttachThreadInput(viewerThreadId, foregroundThreadId, false);
        }
    }

    public static void Close()
    {
        var form = _form;
        if (form is null || form.IsDisposed) return;
        form.BeginInvoke(() => form.Close());
    }
}
