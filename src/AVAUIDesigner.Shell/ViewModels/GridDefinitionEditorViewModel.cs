using System.Collections.ObjectModel;
using System.Reactive;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Properties;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Engine.UndoRedo.Commands;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

/// <summary>One row/column definition in the Grid editor.</summary>
public class GridDefinitionItemViewModel : ReactiveObject
{
    private readonly Action _changed;
    private string _value;
    private bool _hasError;

    public GridDefinitionItemViewModel(int index, string value, Action changed, Action<GridDefinitionItemViewModel> delete)
    {
        Index = index;
        _value = value;
        _changed = changed;
        DeleteCommand = ReactiveCommand.Create(() => delete(this));
    }

    public int Index { get; }
    public string Label => $"{Index}";

    /// <summary>"*", "2*", "Auto" or a pixel value.</summary>
    public string Value
    {
        get => _value;
        set
        {
            var v = (value ?? string.Empty).Trim();
            if (v == _value) return;
            if (!GridDefinitions.IsValid(v))
            {
                HasError = true;
                return;
            }
            HasError = false;
            _value = v;
            this.RaisePropertyChanged();
            _changed();
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set => this.RaiseAndSetIfChanged(ref _hasError, value);
    }

    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
}

/// <summary>Reads and writes Grid.RowDefinitions / ColumnDefinitions in either attribute or element form.</summary>
public static class GridDefinitions
{
    public static List<string> Read(DesignElement grid, bool rows)
    {
        var name = rows ? "RowDefinitions" : "ColumnDefinitions";
        var sizeAttr = rows ? "Height" : "Width";
        if (grid.GetValue(name) is { } compact)
            return compact.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (grid.GetPropertyElement(name) is { } pe)
            return pe.ElementValues.Select(d => d.GetValue(sizeAttr) ?? "*").ToList();
        return [];
    }

    /// <summary>Builds the element-form property (null when there are no definitions).</summary>
    public static DesignProperty? Build(bool rows, IEnumerable<string> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return null;
        var prop = new DesignProperty(rows ? "RowDefinitions" : "ColumnDefinitions", null) { IsAttributeForm = false };
        foreach (var v in list)
        {
            var def = new DesignElement(rows ? "RowDefinition" : "ColumnDefinition");
            if (v != "*") def.SetValue(rows ? "Height" : "Width", v);
            prop.ElementValues.Add(def);
        }
        return prop;
    }

    public static bool IsValid(string v) =>
        v.Length > 0 && XamlValueConverter.TryDeserialize(v, typeof(Avalonia.Controls.GridLength), out _);
}

/// <summary>The Grid definition editor section shown in the property grid when a Grid is selected.</summary>
public class GridDefinitionEditorViewModel : ReactiveObject
{
    private readonly DesignElement _grid;
    private readonly Action<IDesignCommand> _execute;

    public GridDefinitionEditorViewModel(DesignElement grid, Action<IDesignCommand> execute)
    {
        _grid = grid;
        _execute = execute;
        AddRowCommand = ReactiveCommand.Create(() => Add(rows: true));
        AddColumnCommand = ReactiveCommand.Create(() => Add(rows: false));
        Reload();
    }

    public ObservableCollection<GridDefinitionItemViewModel> Rows { get; } = [];
    public ObservableCollection<GridDefinitionItemViewModel> Columns { get; } = [];

    public ReactiveCommand<Unit, Unit> AddRowCommand { get; }
    public ReactiveCommand<Unit, Unit> AddColumnCommand { get; }

    /// <summary>Row heights for the thumbnail.</summary>
    public IReadOnlyList<string> RowValues => Rows.Select(r => r.Value).ToList();

    public IReadOnlyList<string> ColumnValues => Columns.Select(c => c.Value).ToList();

    public void Reload()
    {
        Fill(Rows, GridDefinitions.Read(_grid, rows: true), rows: true);
        Fill(Columns, GridDefinitions.Read(_grid, rows: false), rows: false);
        this.RaisePropertyChanged(nameof(RowValues));
        this.RaisePropertyChanged(nameof(ColumnValues));
    }

    private void Fill(ObservableCollection<GridDefinitionItemViewModel> target, List<string> values, bool rows)
    {
        target.Clear();
        for (var i = 0; i < values.Count; i++)
            target.Add(new GridDefinitionItemViewModel(i, values[i], () => Commit(rows), item => Delete(item, rows)));
    }

    private void Add(bool rows)
    {
        var values = (rows ? Rows : Columns).Select(r => r.Value).ToList();
        // A grid without definitions has one implicit "*" row/column: adding makes it two.
        if (values.Count == 0) values.Add("*");
        values.Add("*");
        Commit(rows, values, rows ? "Add row" : "Add column");
    }

    private void Delete(GridDefinitionItemViewModel item, bool rows)
    {
        var values = (rows ? Rows : Columns).Where(r => r != item).Select(r => r.Value).ToList();
        Commit(rows, values, rows ? "Delete row" : "Delete column");
    }

    private void Commit(bool rows) => Commit(rows, (rows ? Rows : Columns).Select(r => r.Value).ToList(),
        rows ? "Edit row definitions" : "Edit column definitions");

    private void Commit(bool rows, List<string> values, string description)
    {
        var name = rows ? "RowDefinitions" : "ColumnDefinitions";
        _execute(new ReplacePropertyCommand(_grid, name, GridDefinitions.Build(rows, values), description));
    }
}
