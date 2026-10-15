using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Shell.Services;

namespace AVAUIDesigner.Shell.Dialogs;

/// <summary>File › New wizard: class name, namespace, kind, MVVM and target folder.</summary>
public class NewDocumentDialog : DialogWindow
{
    public NewDocumentDialog(NewDocumentKind kind, bool mvvm, string defaultNamespace, string? defaultDirectory)
        : base(kind == NewDocumentKind.Window ? "New Window" : "New UserControl", 480)
    {
        var name = new TextBox { Text = kind == NewDocumentKind.Window ? "NewWindow" : "NewControl" };
        var ns = new TextBox { Text = defaultNamespace };
        var kindBox = new ComboBox { ItemsSource = new[] { "Window", "UserControl" }, SelectedIndex = kind == NewDocumentKind.Window ? 0 : 1, HorizontalAlignment = HorizontalAlignment.Stretch };
        var mvvmBox = new CheckBox { Content = "MVVM (generate a ViewModel and bindings)", IsChecked = mvvm };
        var folder = new TextBox { Text = defaultDirectory ?? string.Empty, Watermark = "(unsaved until File › Save)" };
        var browse = new Button { Content = "…" };
        browse.Click += async (_, _) =>
        {
            var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Folder", AllowMultiple = false });
            if (result.Count > 0 && result[0].TryGetLocalPath() is { } p) folder.Text = p;
        };
        var error = new TextBlock { Foreground = new SolidColorBrush(Color.Parse("#E0453A")), IsVisible = false, TextWrapping = TextWrapping.Wrap };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"), RowSpacing = 8, ColumnSpacing = 8 };
        void Row(int r, string label, Control c, Control? extra = null)
        {
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(l, r);
            Grid.SetRow(c, r);
            Grid.SetColumn(c, 1);
            if (extra is null) Grid.SetColumnSpan(c, 2);
            grid.Children.Add(l);
            grid.Children.Add(c);
            if (extra is not null)
            {
                Grid.SetRow(extra, r);
                Grid.SetColumn(extra, 2);
                grid.Children.Add(extra);
            }
        }
        Row(0, "Class name", name);
        Row(1, "Namespace", ns);
        Row(2, "Type", kindBox);
        Row(3, "Folder", folder, browse);
        Grid.SetRow(mvvmBox, 4);
        Grid.SetColumn(mvvmBox, 1);
        Grid.SetColumnSpan(mvvmBox, 2);
        grid.Children.Add(mvvmBox);
        Grid.SetRow(error, 5);
        Grid.SetColumnSpan(error, 3);
        grid.Children.Add(error);
        SetBody(grid);

        AddButton("Create", () =>
        {
            var cls = name.Text?.Trim() ?? string.Empty;
            var nspace = ns.Text?.Trim() ?? string.Empty;
            string? problem = null;
            if (!CodeIdentifiers.IsValidIdentifier(cls)) problem = "The class name must be a valid C# identifier.";
            else if (nspace.Length == 0 || nspace.Split('.').Any(p => !CodeIdentifiers.IsValidIdentifier(p))) problem = "The namespace is not valid.";
            if (problem is not null)
            {
                error.Text = problem;
                error.IsVisible = true;
                return Cancelled.Instance;
            }
            var dir = string.IsNullOrWhiteSpace(folder.Text) ? null : folder.Text.Trim();
            return new NewDocumentRequest(cls, nspace, kindBox.SelectedIndex == 0 ? NewDocumentKind.Window : NewDocumentKind.UserControl,
                mvvmBox.IsChecked == true, dir);
        }, isDefault: true);
        AddButton("Cancel", () => null, isCancel: true);
        Opened += (_, _) =>
        {
            name.Focus();
            name.SelectAll();
        };
    }
}
