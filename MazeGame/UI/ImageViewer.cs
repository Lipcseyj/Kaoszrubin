using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MazeGame.UI;

public static class ImageViewer
{
    private static Form? _form;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

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
                    _form.BringToFront();
                    _form.Activate();
                    _form.Focus();
                    SetForegroundWindow(_form.Handle);
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

    public static void Close()
    {
        var form = _form;
        if (form is null || form.IsDisposed) return;
        form.BeginInvoke(() => form.Close());
    }
}
