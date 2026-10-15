using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;

namespace AVAUIDesigner.Shell.Dialogs;

/// <summary>Base for the small modal dialogs: content area + right-aligned button row.</summary>
public class DialogWindow : Window
{
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly DockPanel _root = new() { Margin = new Thickness(16) };

    public DialogWindow(string title, double width = 420)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DockPanel.SetDock(_buttons, Dock.Bottom);
        _buttons.Margin = new Thickness(0, 16, 0, 0);
        _root.Children.Add(_buttons);
        Content = _root;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(null);
        };
    }

    protected void SetBody(Control body) => _root.Children.Add(body);

    protected Button AddButton(string text, Func<object?> result, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button { Content = text, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = isDefault, IsCancel = isCancel };
        if (isDefault) button.Classes.Add("accent");
        button.Click += (_, _) =>
        {
            var r = result();
            if (r is not Cancelled) Close(r);
        };
        _buttons.Children.Add(button);
        return button;
    }

    /// <summary>Returned from a button callback to keep the dialog open (e.g. validation failed).</summary>
    protected sealed class Cancelled
    {
        public static readonly Cancelled Instance = new();
    }

    protected static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
}

/// <summary>Message with arbitrary buttons; returns the clicked button's text.</summary>
public class MessageDialog : DialogWindow
{
    public MessageDialog(string title, string message, params string[] buttons) : base(title)
    {
        SetBody(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 });
        for (var i = 0; i < buttons.Length; i++)
        {
            var text = buttons[i];
            AddButton(text, () => text, isDefault: i == 0, isCancel: text is "Cancel" or "No" && i == buttons.Length - 1);
        }
    }
}

/// <summary>Single-line prompt with validation.</summary>
public class PromptDialog : DialogWindow
{
    private readonly TextBox _input;
    private readonly TextBlock _error = new() { Foreground = new SolidColorBrush(Color.Parse("#E0453A")), TextWrapping = TextWrapping.Wrap, IsVisible = false };

    public PromptDialog(string title, string message, string initial, Func<string, string?>? validate) : base(title)
    {
        _input = new TextBox { Text = initial };
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(Label(message));
        body.Children.Add(_input);
        body.Children.Add(_error);
        SetBody(body);
        AddButton("OK", () =>
        {
            var text = _input.Text ?? string.Empty;
            if (validate?.Invoke(text) is { } error)
            {
                _error.Text = error;
                _error.IsVisible = true;
                return Cancelled.Instance;
            }
            return text;
        }, isDefault: true);
        AddButton("Cancel", () => null, isCancel: true);
        Opened += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };
    }
}

/// <summary>Multi-line text editor (XAML-highlighted when requested).</summary>
public class TextEditDialog : DialogWindow
{
    public TextEditDialog(string title, string message, string text, bool isXaml) : base(title, 640)
    {
        SizeToContent = SizeToContent.Manual;
        Height = 460;
        CanResize = true;
        var editor = new TextEditor
        {
            Text = text,
            ShowLineNumbers = isXaml,
            FontFamily = new FontFamily("Cascadia Code,Consolas,Menlo,DejaVu Sans Mono,Noto Sans Mono,Liberation Mono,JetBrains Mono,Ubuntu Mono,monospace"),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        if (isXaml) editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        var body = new DockPanel();
        var label = Label(message);
        DockPanel.SetDock(label, Dock.Top);
        body.Children.Add(label);
        body.Children.Add(editor);
        SetBody(body);
        AddButton("OK", () => editor.Text);
        AddButton("Cancel", () => null, isCancel: true);
        Opened += (_, _) => editor.Focus();
    }
}
