using System.Reactive.Linq;
using System.Reactive.Subjects;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Serialization;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

/// <summary>
/// The XAML source pane. Designer changes replace the text (diff-aware in the view); user edits are
/// re-parsed after a 400 ms pause and, when valid, pushed back into the design document.
/// </summary>
public class XamlSourceViewModel : ReactiveObject, IDisposable
{
    public static readonly TimeSpan ParseDelay = TimeSpan.FromMilliseconds(400);

    private readonly XamlParser _parser;
    private readonly ControlDescriptorRegistry _registry;
    private readonly Subject<string> _edits = new();
    private readonly IDisposable _subscription;
    private string _xamlText = string.Empty;
    private bool _hasParseError;
    private string _parseErrorMessage = string.Empty;
    private int _errorLine;
    private int _errorColumn;
    private bool _isSplitViewVisible = true;
    private IReadOnlyList<XamlParseWarning> _warnings = [];

    public XamlSourceViewModel(XamlParser parser, ControlDescriptorRegistry registry)
    {
        _parser = parser;
        _registry = registry;
        _subscription = _edits.Throttle(ParseDelay, RxSchedulers.TaskpoolScheduler)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(ParseUserText);
    }

    /// <summary>Called with a successful parse of user-edited text.</summary>
    public Action<XamlParseResult>? ApplyParsed { get; set; }

    /// <summary>The current source text (two-way with the editor).</summary>
    public string XamlText
    {
        get => _xamlText;
        set
        {
            if (_xamlText == value) return;
            this.RaiseAndSetIfChanged(ref _xamlText, value);
            _edits.OnNext(value);
        }
    }

    public bool HasParseError
    {
        get => _hasParseError;
        private set => this.RaiseAndSetIfChanged(ref _hasParseError, value);
    }

    public string ParseErrorMessage
    {
        get => _parseErrorMessage;
        private set => this.RaiseAndSetIfChanged(ref _parseErrorMessage, value);
    }

    public int ErrorLine
    {
        get => _errorLine;
        private set => this.RaiseAndSetIfChanged(ref _errorLine, value);
    }

    public int ErrorColumn
    {
        get => _errorColumn;
        private set => this.RaiseAndSetIfChanged(ref _errorColumn, value);
    }

    /// <summary>Unknown attributes etc. from the last parse (shown as squiggles).</summary>
    public IReadOnlyList<XamlParseWarning> Warnings
    {
        get => _warnings;
        private set => this.RaiseAndSetIfChanged(ref _warnings, value);
    }

    /// <summary>Show/hide the panel.</summary>
    public bool IsSplitViewVisible
    {
        get => _isSplitViewVisible;
        set => this.RaiseAndSetIfChanged(ref _isSplitViewVisible, value);
    }

    /// <summary>Element → (offset, length) in <see cref="XamlText"/> when the text came from the serializer.</summary>
    public Dictionary<DesignElement, (int Start, int Length)> ElementSpans { get; private set; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>Element → 1-based (line, column) when the text came from a parse.</summary>
    public Dictionary<DesignElement, (int Line, int Column)> ElementLocations { get; private set; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>Raised when the designer replaced the text (the view applies a minimal diff).</summary>
    public event EventHandler<string>? TextReplacedFromDesigner;

    /// <summary>Raised to select a range in the editor.</summary>
    public event EventHandler<(int Start, int Length)>? NavigateRequested;

    /// <summary>Replaces the text after a designer change (no re-parse).</summary>
    public void SetTextFromDesigner(string text, Dictionary<DesignElement, (int Start, int Length)> spans)
    {
        ElementSpans = spans;
        ElementLocations = new(ReferenceEqualityComparer.Instance);
        HasParseError = false;
        ParseErrorMessage = string.Empty;
        if (_xamlText == text) return;
        _xamlText = text;
        this.RaisePropertyChanged(nameof(XamlText));
        TextReplacedFromDesigner?.Invoke(this, text);
    }

    /// <summary>Loads raw file text (may be invalid) together with the result of parsing it.</summary>
    public void LoadText(string text, XamlParseResult? result, XamlParseError? error)
    {
        _xamlText = text;
        this.RaisePropertyChanged(nameof(XamlText));
        TextReplacedFromDesigner?.Invoke(this, text);
        UpdateDiagnostics(result, error);
    }

    /// <summary>Parses the current text right away (e.g. before saving).</summary>
    public void FlushPendingEdits()
    {
        ParseUserText(_xamlText);
    }

    private void ParseUserText(string text)
    {
        if (text != _xamlText) return; // superseded
        var ok = _parser.TryParseDetailed(text, _registry, out var result, out var error);
        UpdateDiagnostics(ok ? result : null, error);
        if (ok && result is not null) ApplyParsed?.Invoke(result);
    }

    private void UpdateDiagnostics(XamlParseResult? result, XamlParseError? error)
    {
        if (result is not null)
        {
            HasParseError = false;
            ParseErrorMessage = string.Empty;
            ErrorLine = ErrorColumn = 0;
            Warnings = result.Warnings;
            ElementLocations = result.ElementLocations;
            ElementSpans = new(ReferenceEqualityComparer.Instance);
        }
        else if (error is not null)
        {
            HasParseError = true;
            ParseErrorMessage = error.ToString();
            ErrorLine = error.Line;
            ErrorColumn = error.Column;
        }
    }

    /// <summary>Selects the start tag of <paramref name="element"/> in the editor.</summary>
    public bool NavigateToElement(DesignElement element)
    {
        if (ElementSpans.TryGetValue(element, out var span))
        {
            NavigateRequested?.Invoke(this, (span.Start, Math.Min(span.Length, element.ElementType.Length + 1)));
            return true;
        }
        if (ElementLocations.TryGetValue(element, out var loc) && OffsetOf(loc.Line, loc.Column) is { } offset)
        {
            NavigateRequested?.Invoke(this, (Math.Max(0, offset - 1), element.ElementType.Length + 1));
            return true;
        }
        return false;
    }

    /// <summary>Selects an attribute (e.g. an event) of <paramref name="element"/> in the editor.</summary>
    public bool NavigateToAttribute(DesignElement element, string attribute)
    {
        int start;
        if (ElementSpans.TryGetValue(element, out var span)) start = span.Start;
        else if (ElementLocations.TryGetValue(element, out var loc) && OffsetOf(loc.Line, loc.Column) is { } o) start = o;
        else return false;
        var tagEnd = _xamlText.IndexOf('>', start);
        if (tagEnd < 0) tagEnd = _xamlText.Length;
        var idx = _xamlText.IndexOf(attribute + "=", start, tagEnd - start, StringComparison.Ordinal);
        if (idx < 0) return NavigateToElement(element);
        var valueEnd = _xamlText.IndexOf('"', _xamlText.IndexOf('"', idx) + 1);
        NavigateRequested?.Invoke(this, (idx, (valueEnd < 0 ? idx + attribute.Length : valueEnd + 1) - idx));
        return true;
    }

    /// <summary>Offset of a 1-based line/column in the text.</summary>
    public int? OffsetOf(int line, int column)
    {
        var offset = 0;
        for (var l = 1; l < line; l++)
        {
            var nl = _xamlText.IndexOf('\n', offset);
            if (nl < 0) return null;
            offset = nl + 1;
        }
        return Math.Min(_xamlText.Length, offset + Math.Max(0, column - 1));
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _edits.Dispose();
    }
}
