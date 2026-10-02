using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace WpfFoundation.Dialogs;

/// <summary>
/// The one window every dialog is shown in. Its content is the dialog ViewModel, whose DataTemplate supplies
/// the body; the window adds the title, the footer buttons, sizing and theme. It closes when the ViewModel
/// completes, and Escape or closing the window cancels the ViewModel.
/// </summary>
internal sealed partial class DialogWindow : Window
{
    private readonly DialogViewModel dialog;
    private bool closing;

    public DialogWindow(DialogViewModel dialog)
    {
        this.dialog = dialog;
        InitializeComponent();
        DataContext = dialog;
        if (dialog.IsResizable)
        {
            ResizeMode = ResizeMode.CanResize;
            SizeToContent = SizeToContent.Manual;
            Width = 840;
            Height = 600;
            MinWidth = 600;
            MinHeight = 400;
        }
        else
        {
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = 320;
            MaxWidth = 640;
            MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        }

        dialog.PropertyChanged += OnDialogPropertyChanged;
        Loaded += (_, _) => MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        // Cancel buttons handle Escape themselves; this covers dialogs without one.
        if (e.Key == Key.Escape && !e.Handled && !dialog.Buttons.Any(button => button.IsCancel))
        {
            e.Handled = true;
            dialog.Cancel();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        closing = true;
        base.OnClosing(e);
        if (!e.Cancel)
        {
            dialog.Cancel();
        }
        else
        {
            closing = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        dialog.PropertyChanged -= OnDialogPropertyChanged;
        base.OnClosed(e);
    }

    private void OnDialogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DialogViewModel.IsCompleted) && dialog.IsCompleted && !closing)
        {
            Close();
        }
    }
}
