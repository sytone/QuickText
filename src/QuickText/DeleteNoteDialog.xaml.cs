using System.Windows;

namespace QuickText;

public partial class DeleteNoteDialog : Window
{
    public DeleteNoteDialog(string fileName)
    {
        InitializeComponent();
        FileNameLabel.Text = fileName;
        DeleteButton.Click += (_, _) => DialogResult = true;
        CancelButton.Click += (_, _) => DialogResult = false;
        CloseButton.Click += (_, _) => DialogResult = false;
        Loaded += (_, _) => CancelButton.Focus();
    }
}
