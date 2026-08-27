using System.Drawing;
using System.Windows.Forms;

public static class ImageViewer
{
    private static Form? _form;

    public static void Show(string fileName)
    {
        var thread = new Thread(() =>
        {
            using var image = Image.FromFile(fileName);

            _form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.Black,
                StartPosition = FormStartPosition.CenterScreen,
                Width = 1000,
                Height = 700,
                TopMost = true
            };

            var pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = image
            };

            _form.Controls.Add(pictureBox);

            System.Windows.Forms.Application.Run(_form);

            _form = null;
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public static void Close()
    {
        var form = _form;

        if (form is null || form.IsDisposed)
            return;

        form.BeginInvoke(() => form.Close());
    }
}