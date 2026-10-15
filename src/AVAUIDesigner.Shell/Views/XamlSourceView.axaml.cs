using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using AVAUIDesigner.Shell.Controls;
using AVAUIDesigner.Shell.Design;
using AVAUIDesigner.Shell.ViewModels;
using ReactiveUI;

namespace AVAUIDesigner.Shell.Views;

/// <summary>
/// XAML source (AvaloniaEdit) kept in sync with the designer, plus read-only previews of the
/// generated code-behind and ViewModel.
/// </summary>
public partial class XamlSourceView : UserControl
{
    private readonly EditorDecorations _decorations = new();
    private readonly DispatcherTimer _foldTimer;
    private FoldingManager? _folding;
    private readonly XmlFoldingStrategy _foldingStrategy = new();
    private MainWindowViewModel? _vm;
    private bool _updating;
    private CompletionWindow? _completion;
    private readonly List<IDisposable> _subscriptions = [];

    public XamlSourceView()
    {
        InitializeComponent();
        XamlEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        ActualThemeVariantChanged += (_, _) => ApplyHighlightingTheme();
        ApplyHighlightingTheme();
        CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        ViewModelEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        XamlEditor.Options.ConvertTabsToSpaces = true;
        XamlEditor.Options.IndentationSize = 4;
        _folding = FoldingManager.Install(XamlEditor.TextArea);
        XamlEditor.TextArea.TextView.BackgroundRenderers.Add(_decorations);

        _foldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _foldTimer.Tick += (_, _) =>
        {
            _foldTimer.Stop();
            UpdateFoldings();
        };

        XamlEditor.TextChanged += OnEditorTextChanged;
        XamlEditor.TextArea.TextEntered += OnTextEntered;
        XamlEditor.TextArea.Caret.PositionChanged += (_, _) => UpdateTagMatch();
        XamlEditor.AddHandler(KeyDownEvent, OnEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        XamlEditor.PointerMoved += OnEditorPointerMoved;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
        if (_vm is not null)
        {
            _vm.XamlSource.TextReplacedFromDesigner -= OnTextReplaced;
            _vm.XamlSource.NavigateRequested -= OnNavigate;
            _vm.CodeNavigateRequested -= OnCodeNavigate;
        }
        _vm = DataContext as MainWindowViewModel;
        if (_vm is null) return;
        _vm.XamlSource.TextReplacedFromDesigner += OnTextReplaced;
        _vm.XamlSource.NavigateRequested += OnNavigate;
        _vm.CodeNavigateRequested += OnCodeNavigate;
        _subscriptions.Add(_vm.WhenAnyValue(x => x.CodeBehindText).Subscribe(t => ReplaceText(CodeEditor, t)));
        _subscriptions.Add(_vm.WhenAnyValue(x => x.ViewModelText).Subscribe(t => ReplaceText(ViewModelEditor, t)));
        _subscriptions.Add(_vm.XamlSource.WhenAnyValue(x => x.HasParseError, x => x.Warnings, x => x.ErrorLine)
            .Subscribe(_ => UpdateDiagnostics()));
        OnTextReplaced(this, _vm.XamlSource.XamlText);
    }

    /// <summary>The built-in XML/C# colors are tuned for light backgrounds; brighten them in dark mode.</summary>
    private void ApplyHighlightingTheme()
    {
        var dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var xml = new Dictionary<string, (string Light, string Dark)>
        {
            ["Comment"] = ("#008000", "#6A9955"),
            ["CData"] = ("#808080", "#9E9E9E"),
            ["DocType"] = ("#0000FF", "#569CD6"),
            ["XmlDeclaration"] = ("#0000FF", "#569CD6"),
            ["XmlTag"] = ("#A31515", "#569CD6"),
            ["AttributeName"] = ("#FF0000", "#9CDCFE"),
            ["AttributeValue"] = ("#0000FF", "#CE9178"),
            ["Entity"] = ("#008080", "#D7BA7D"),
            ["BrokenEntity"] = ("#FF0000", "#F44747"),
        };
        Recolor(HighlightingManager.Instance.GetDefinition("XML"), xml, dark);
        var cs = new Dictionary<string, (string Light, string Dark)>
        {
            ["Comment"] = ("#008000", "#6A9955"),
            ["String"] = ("#A31515", "#CE9178"),
            ["Keywords"] = ("#0000FF", "#569CD6"),
            ["ReferenceTypeKeywords"] = ("#0000FF", "#569CD6"),
            ["ValueTypeKeywords"] = ("#0000FF", "#569CD6"),
            ["MethodCall"] = ("#191970", "#DCDCAA"),
            ["Preprocessor"] = ("#808080", "#9B9B9B"),
            ["NumberLiteral"] = ("#000000", "#B5CEA8"),
        };
        Recolor(HighlightingManager.Instance.GetDefinition("C#"), cs, dark);
        foreach (var editor in new[] { XamlEditor, CodeEditor, ViewModelEditor })
        {
            editor.TextArea.TextView.LinkTextForegroundBrush =
                new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(dark ? "#4FC1FF" : "#0000FF"));
            var h = editor.SyntaxHighlighting;
            editor.SyntaxHighlighting = null;
            editor.SyntaxHighlighting = h;
        }
    }

    private static void Recolor(IHighlightingDefinition? definition, Dictionary<string, (string Light, string Dark)> colors, bool dark)
    {
        if (definition is null) return;
        foreach (var named in definition.NamedHighlightingColors)
        {
            if (!colors.TryGetValue(named.Name, out var c)) continue;
            named.Foreground = new SimpleHighlightingBrush(Avalonia.Media.Color.Parse(dark ? c.Dark : c.Light));
        }
    }

    // ------------------------------------------------------------------ sync

    private void OnTextReplaced(object? sender, string text)
    {
        _updating = true;
        try
        {
            ReplaceText(XamlEditor, text);
        }
        finally
        {
            _updating = false;
        }
        _foldTimer.Stop();
        _foldTimer.Start();
        UpdateDiagnostics();
    }

    /// <summary>Applies only the changed middle part, so the caret and scroll position survive.</summary>
    private static void ReplaceText(TextEditor editor, string text)
    {
        var doc = editor.Document;
        var old = doc.Text;
        if (old == text) return;
        var prefix = 0;
        var max = Math.Min(old.Length, text.Length);
        while (prefix < max && old[prefix] == text[prefix]) prefix++;
        var suffix = 0;
        while (suffix < max - prefix && old[old.Length - 1 - suffix] == text[text.Length - 1 - suffix]) suffix++;
        doc.Replace(prefix, old.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _foldTimer.Stop();
        _foldTimer.Start();
        if (_updating || _vm is null) return;
        _vm.XamlSource.XamlText = XamlEditor.Text;
    }

    private void UpdateFoldings()
    {
        if (_folding is null) return;
        try
        {
            _foldingStrategy.UpdateFoldings(_folding, XamlEditor.Document);
        }
        catch (Exception)
        {
            // Folding is cosmetic; ignore failures on malformed text.
        }
    }

    private void UpdateDiagnostics()
    {
        _decorations.Markers.Clear();
        if (_vm?.XamlSource is { } src)
        {
            if (src.HasParseError && src.ErrorLine > 0 && src.OffsetOf(src.ErrorLine, src.ErrorColumn) is { } off)
                _decorations.Markers.Add(new EditorDecorations.Marker(Math.Max(0, off - 1), 3, true, src.ParseErrorMessage));
            if (!src.HasParseError)
                foreach (var w in src.Warnings)
                    if (src.OffsetOf(w.Line, w.Column) is { } o)
                        _decorations.Markers.Add(new EditorDecorations.Marker(o, w.Length, false, w.Message));
        }
        XamlEditor.TextArea.TextView.InvalidateLayer(_decorations.Layer);
    }

    private void UpdateTagMatch()
    {
        _decorations.TagHighlights.Clear();
        if (XmlScanner.MatchingTag(XamlEditor.Text, XamlEditor.CaretOffset) is { } m)
        {
            _decorations.TagHighlights.Add((m.A, m.ALength));
            _decorations.TagHighlights.Add((m.B, m.BLength));
        }
        XamlEditor.TextArea.TextView.InvalidateLayer(_decorations.Layer);
    }

    private void OnEditorPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = XamlEditor.GetPositionFromPoint(e.GetPosition(XamlEditor));
        if (pos is null)
        {
            ToolTip.SetTip(XamlEditor, null);
            return;
        }
        var offset = XamlEditor.Document.GetOffset(pos.Value.Location);
        ToolTip.SetTip(XamlEditor, _decorations.MessageAt(offset));
    }

    // ------------------------------------------------------------------ navigation

    private void OnNavigate(object? sender, (int Start, int Length) span)
    {
        var len = XamlEditor.Document.TextLength;
        var start = Math.Clamp(span.Start, 0, len);
        XamlEditor.Select(start, Math.Clamp(span.Length, 0, len - start));
        var loc = XamlEditor.Document.GetLocation(start);
        XamlEditor.ScrollTo(loc.Line, loc.Column);
        XamlEditor.Focus();
    }

    private void OnCodeNavigate(object? sender, string search)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var editor = _vm?.BottomTab == 2 ? ViewModelEditor : CodeEditor;
            var text = editor.Text;
            var idx = text.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return;
            var nameStart = search.StartsWith("void ", StringComparison.Ordinal) ? idx + 5 : idx;
            var nameLength = search.TrimEnd('(').Length - (nameStart - idx);
            editor.Select(nameStart, Math.Max(0, nameLength));
            var loc = editor.Document.GetLocation(idx);
            editor.ScrollTo(loc.Line, loc.Column);
        }, DispatcherPriority.Background);
    }

    // ------------------------------------------------------------------ editing aids

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowAttributeCompletion();
            e.Handled = true;
        }
        else if (e.Key == Key.F12)
        {
            if (XmlScanner.WordAt(XamlEditor.Text, XamlEditor.CaretOffset) is { } word) GoToDefinition(word);
            e.Handled = true;
        }
    }

    private void GoToDefinition(string word)
    {
        if (_vm?.CurrentDocument is not { } doc) return;
        string? search = null;
        if (doc.NamedElements.ContainsKey(word)) search = _vm.CodeBehindText.Contains($" {word};") ? $" {word};" : word;
        else if (doc.AllElements.Any(e => e.EventHandlers.Any(h => h.HandlerName == word))) search = $"void {word}(";
        if (search is null) return;
        _vm.BottomTab = 1;
        OnCodeNavigate(this, search);
    }

    private void OnTextEntered(object? sender, Avalonia.Input.TextInputEventArgs e)
    {
        if (e.Text is not { Length: 1 } typed) return;
        var text = XamlEditor.Text;
        var caret = XamlEditor.CaretOffset;

        switch (typed[0])
        {
            case '>':
            {
                // Auto-close: <Button ...>|  →  <Button ...>|</Button>
                if (caret >= 2 && text[caret - 2] == '/') return;
                var lt = text.LastIndexOf('<', Math.Max(0, caret - 1));
                if (lt < 0 || lt + 1 >= text.Length || text[lt + 1] is '/' or '!' or '?') return;
                var nameEnd = lt + 1;
                while (nameEnd < caret && XmlScanner.IsNameChar(text[nameEnd])) nameEnd++;
                var name = text[(lt + 1)..nameEnd];
                if (name.Length == 0) return;
                XamlEditor.Document.Insert(caret, $"</{name}>");
                XamlEditor.CaretOffset = caret;
                break;
            }
            case '/':
            {
                // "</" completes the innermost open element.
                if (caret < 2 || text[caret - 2] != '<') return;
                var open = XmlScanner.OpenElements(text, caret - 2);
                if (open.Count == 0) return;
                XamlEditor.Document.Insert(caret, open[^1] + ">");
                break;
            }
            case '<':
                ShowElementCompletion();
                break;
            case ' ':
                if (XmlScanner.ElementAtCaretInTag(text, caret) is not null) ShowAttributeCompletion();
                break;
        }
    }

    private void ShowElementCompletion()
    {
        if (_vm is null) return;
        var items = _vm.Registry.GetAll().Where(d => !d.IsRootOnly)
            .Select(d => new XamlCompletionData(d.ElementName, d.Description, isAttribute: false)).ToList();
        ShowCompletion(items, XamlEditor.CaretOffset);
    }

    /// <summary>Ctrl+Space: attributes valid for the element at the caret.</summary>
    private void ShowAttributeCompletion()
    {
        if (_vm is null) return;
        var text = XamlEditor.Text;
        var caret = XamlEditor.CaretOffset;
        var elementName = XmlScanner.ElementAtCaretInTag(text, caret);
        if (elementName is null) return;
        var registry = _vm.Registry;
        var descriptor = registry.GetByElementName(elementName);

        var items = new List<XamlCompletionData>();
        if (descriptor is not null)
        {
            items.AddRange(descriptor.Properties.Select(p => new XamlCompletionData(p.Name, $"{p.Name} ({p.PropertyType.Name}) — {p.Description}", true, 1)));
            items.AddRange(descriptor.Events.Select(ev => new XamlCompletionData(ev.Name, $"Event: {ev.Description}", true)));
        }
        // Attached properties from the parent element and globals.
        var open = XmlScanner.OpenElements(text, text.LastIndexOf('<', Math.Max(0, caret - 1)));
        if (open.Count > 0 && registry.GetByElementName(open[^1]) is { } parent)
            items.AddRange(parent.ChildAttachedProperties.Select(p => new XamlCompletionData(p.FullName, p.Description, true, 2)));
        items.AddRange(registry.GlobalAttachedProperties.Select(p => new XamlCompletionData(p.FullName, p.Description, true)));
        items.Add(new XamlCompletionData("x:Name", "The element's name (creates a field in code-behind).", true, 3));
        if (items.Count == 0) return;

        // Replace a partially typed attribute name.
        var start = caret;
        while (start > 0 && XmlScanner.IsNameChar(text[start - 1])) start--;
        ShowCompletion(items.GroupBy(i => i.Text).Select(g => g.First()).OrderBy(i => i.Text).ToList(), start);
    }

    private void ShowCompletion(List<XamlCompletionData> items, int startOffset)
    {
        _completion?.Close();
        _completion = new CompletionWindow(XamlEditor.TextArea) { StartOffset = startOffset };
        foreach (var item in items) _completion.CompletionList.CompletionData.Add(item);
        _completion.Closed += (_, _) => _completion = null;
        _completion.Show();
    }

    /// <summary>Gives keyboard focus to the XAML editor.</summary>
    public void FocusEditor() => XamlEditor.Focus();
}
