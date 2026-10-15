using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAUIDesigner.Engine.Settings;

namespace AVAUIDesigner.Shell.Dialogs;

/// <summary>Tools › Preferences. Edits a copy of the settings; OK returns it.</summary>
public class PreferencesWindow : DialogWindow
{
    public PreferencesWindow(DesignerSettings s) : base("Preferences", 520)
    {
        var theme = new ComboBox { ItemsSource = new[] { "System", "FluentLight", "FluentDark" }, SelectedItem = s.Ui.Theme, HorizontalAlignment = HorizontalAlignment.Stretch };
        var snap = new CheckBox { Content = "Snap to grid", IsChecked = s.Design.SnapToGrid };
        var showGrid = new CheckBox { Content = "Show grid", IsChecked = s.Design.ShowGrid };
        var smart = new CheckBox { Content = "Smart snap lines (align to other controls)", IsChecked = s.Design.SmartSnapLines };
        var autoName = new CheckBox { Content = "Name new controls automatically (button1, textBox1…)", IsChecked = s.Design.AutoNameNewControls };
        var gridSize = new NumericUpDown { Value = s.Design.GridSize, Minimum = 2, Maximum = 64, Increment = 1, FormatString = "0" };
        var threshold = new NumericUpDown { Value = s.Design.SnapThreshold, Minimum = 1, Maximum = 20, Increment = 1, FormatString = "0" };
        var zoom = new NumericUpDown { Value = (decimal)(s.Design.DefaultZoom * 100), Minimum = 10, Maximum = 400, Increment = 25, FormatString = "0'%'" };

        var style = new ComboBox { ItemsSource = new[] { "CodeBehind", "MVVM" }, SelectedItem = s.CodeGen.DefaultStyleValue.ToString(), HorizontalAlignment = HorizontalAlignment.Stretch };
        var nullable = new CheckBox { Content = "Nullable annotations in generated code", IsChecked = s.CodeGen.GenerateNullableAnnotations };
        var fields = new CheckBox
        {
            Content = "Generate field declarations (only when Avalonia's name generator is disabled)",
            IsChecked = s.CodeGen.GenerateFieldDeclarations,
        };
        var wire = new CheckBox { Content = "Wire events in code-behind (WireEvents) instead of XAML attributes", IsChecked = s.CodeGen.WireEventsInCode };
        var ns = new TextBox { Text = s.CodeGen.DefaultNamespace, Watermark = "MyApp.Views" };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Header("Appearance"));
        panel.Children.Add(Row("Theme", theme));
        panel.Children.Add(Header("Design surface"));
        panel.Children.Add(snap);
        panel.Children.Add(showGrid);
        panel.Children.Add(smart);
        panel.Children.Add(autoName);
        panel.Children.Add(Row("Grid size (px)", gridSize));
        panel.Children.Add(Row("Snap threshold (px)", threshold));
        panel.Children.Add(Row("Default zoom", zoom));
        panel.Children.Add(Header("Code generation"));
        panel.Children.Add(Row("New documents", style));
        panel.Children.Add(Row("Default namespace", ns));
        panel.Children.Add(nullable);
        panel.Children.Add(fields);
        panel.Children.Add(wire);
        panel.Children.Add(new TextBlock
        {
            Text = $"Settings file: {DesignerSettings.DefaultPath}",
            FontSize = 11,
            Opacity = 0.6,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        SetBody(panel);

        AddButton("OK", () =>
        {
            s.Ui.Theme = theme.SelectedItem as string ?? "System";
            s.Design.SnapToGrid = snap.IsChecked == true;
            s.Design.ShowGrid = showGrid.IsChecked == true;
            s.Design.SmartSnapLines = smart.IsChecked == true;
            s.Design.AutoNameNewControls = autoName.IsChecked == true;
            s.Design.GridSize = (int)(gridSize.Value ?? 8);
            s.Design.SnapThreshold = (int)(threshold.Value ?? 6);
            s.Design.DefaultZoom = (double)(zoom.Value ?? 100) / 100;
            s.CodeGen.DefaultStyle = style.SelectedItem as string ?? "CodeBehind";
            s.CodeGen.DefaultNamespace = ns.Text?.Trim() ?? string.Empty;
            s.CodeGen.GenerateNullableAnnotations = nullable.IsChecked == true;
            s.CodeGen.GenerateFieldDeclarations = fields.IsChecked == true;
            s.CodeGen.WireEventsInCode = wire.IsChecked == true;
            return s;
        }, isDefault: true);
        AddButton("Cancel", () => null, isCancel: true);
    }

    private static TextBlock Header(string text) =>
        new() { Text = text, FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) };

    private static Control Row(string label, Control editor)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*") };
        var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(editor, 1);
        grid.Children.Add(l);
        grid.Children.Add(editor);
        return grid;
    }
}
