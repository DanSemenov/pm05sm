using System.Drawing.Imaging;

namespace pm05sm;

/// <summary>Creates real WinForms evidence images used in practice documentation.</summary>
public static class EvidenceCapture
{
    public static void Create()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "evidence");
        Directory.CreateDirectory(folder);
        Capture(new LoginForm(), Path.Combine(folder, "login.png"));
        Capture(new RegisterForm(), Path.Combine(folder, "registration.png"));
        Capture(new MainForm(new AppUser(1, "admin", "Администратор системы", "admin")), Path.Combine(folder, "dashboard.png"));
        CaptureView("requests", "Заявки", Path.Combine(folder, "requests.png"));
        CaptureView("clients", "Клиенты", Path.Combine(folder, "clients.png"));
        CaptureView("parts", "Запчасти", Path.Combine(folder, "parts.png"));
        CaptureView("masters", "Мастера", Path.Combine(folder, "masters.png"));
        using var editor = new EntityEditForm("requests", 1);
        Capture(editor, Path.Combine(folder, "request-edit.png"));
    }

    private static void CaptureView(string section, string title, string file)
    {
        using var form = new Form { Text = "RepairDesk — " + title, Size = new Size(1180, 720), StartPosition = FormStartPosition.CenterScreen };
        form.Controls.Add(new CrudView(section, true));
        Capture(form, file);
    }

    private static void Capture(Form form, string file)
    {
        form.Show();
        Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(file, ImageFormat.Png);
        form.Close();
        form.Dispose();
    }
}
