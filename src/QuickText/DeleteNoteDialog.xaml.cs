using System.Windows;

namespace QuickText;

/// <summary>Makes permanent deletion explicit while keeping cancellation the safe default.</summary>
public partial class DeleteNoteDialog : Window
{
    /// <summary>Names the threatened file so confirmation applies to the note the user expects.</summary>
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
