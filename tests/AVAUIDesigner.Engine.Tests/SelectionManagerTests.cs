using System.ComponentModel;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Selection;

namespace AVAUIDesigner.Engine.Tests;

public class SelectionManagerTests
{
    private readonly DesignDocument _doc = Samples.Parse(Samples.MainWindow);
    private readonly SelectionManager _sel;
    private readonly List<SelectionChangedEventArgs> _events = [];

    public SelectionManagerTests()
    {
        _sel = new SelectionManager { Document = _doc };
        _sel.SelectionChanged += (_, e) => _events.Add(e);
    }

    private DesignElement E(string name) => _doc.NamedElements[name];
    private DesignElement Grid => _doc.RootElement.Children[0];

    [Fact]
    public void Initially_Empty()
    {
        Assert.False(_sel.HasSelection);
        Assert.False(_sel.IsMultiSelection);
        Assert.Null(_sel.PrimarySelection);
        Assert.Empty(_sel.SelectedElements);
    }

    [Fact]
    public void SetSelection_ReplacesSelectionAndFlags()
    {
        _sel.SetSelection(E("btnOk"));
        Assert.True(E("btnOk").IsSelected);
        Assert.Same(E("btnOk"), _sel.PrimarySelection);
        Assert.True(_sel.HasSelection);
        Assert.False(_sel.IsMultiSelection);
        Assert.Equal([E("btnOk")], _events[^1].Added);

        _sel.SetSelection(E("txtName"));
        Assert.False(E("btnOk").IsSelected);
        Assert.True(E("txtName").IsSelected);
        Assert.Equal([E("txtName")], _sel.SelectedElements);
        Assert.Equal([E("btnOk")], _events[^1].Removed);
        Assert.Equal([E("txtName")], _events[^1].Added);
    }

    [Fact]
    public void SetSelection_SameSelection_RaisesNoEvent()
    {
        _sel.SetSelection(E("btnOk"));
        var count = _events.Count;
        _sel.SetSelection(E("btnOk"));
        Assert.Equal(count, _events.Count);
    }

    [Fact]
    public void SetSelection_Multiple_RemovesDuplicates_LastIsPrimary()
    {
        _sel.SetSelection([E("btnOk"), E("txtName"), E("btnOk")]);
        Assert.Equal(2, _sel.SelectedElements.Count);
        Assert.True(_sel.IsMultiSelection);
        Assert.Same(E("txtName"), _sel.PrimarySelection);
    }

    [Fact]
    public void AddToSelection_AppendsAndBecomesPrimary()
    {
        _sel.SetSelection(E("btnOk"));
        _sel.AddToSelection(E("txtName"));
        Assert.Equal([E("btnOk"), E("txtName")], _sel.SelectedElements);
        Assert.Same(E("txtName"), _sel.PrimarySelection);
        Assert.True(_sel.IsMultiSelection);
        Assert.Equal([E("txtName")], _events[^1].Added);
        Assert.True(E("btnOk").IsSelected && E("txtName").IsSelected);
    }

    [Fact]
    public void AddToSelection_ExistingElement_BecomesPrimaryWithoutDuplicating()
    {
        _sel.SetSelection([E("btnOk"), E("txtName")]);
        _sel.AddToSelection(E("btnOk"));
        Assert.Equal([E("txtName"), E("btnOk")], _sel.SelectedElements);
        Assert.Same(E("btnOk"), _sel.PrimarySelection);
        Assert.Empty(_events[^1].Added);
        Assert.Empty(_events[^1].Removed);
    }

    [Fact]
    public void RemoveFromSelection_ClearsFlag()
    {
        _sel.SetSelection([E("btnOk"), E("txtName")]);
        _sel.RemoveFromSelection(E("btnOk"));
        Assert.False(E("btnOk").IsSelected);
        Assert.Equal([E("txtName")], _sel.SelectedElements);
        Assert.Equal([E("btnOk")], _events[^1].Removed);

        var count = _events.Count;
        _sel.RemoveFromSelection(E("lblTitle")); // not selected: no event
        Assert.Equal(count, _events.Count);
    }

    [Fact]
    public void ToggleSelection_AddsThenRemoves()
    {
        _sel.ToggleSelection(E("btnOk"));
        Assert.True(_sel.IsSelected(E("btnOk")));
        _sel.ToggleSelection(E("txtName"));
        Assert.True(_sel.IsMultiSelection);
        _sel.ToggleSelection(E("btnOk"));
        Assert.False(_sel.IsSelected(E("btnOk")));
        Assert.False(E("btnOk").IsSelected);
        Assert.Equal([E("txtName")], _sel.SelectedElements);
    }

    [Fact]
    public void SelectAll_ExcludesRootAndPropertyElementContents()
    {
        _sel.SelectAll();
        Assert.DoesNotContain(_doc.RootElement, _sel.SelectedElements);
        Assert.False(_doc.RootElement.IsSelected);
        Assert.Equal([Grid, E("lblTitle"), E("txtName"), E("btnOk")], _sel.SelectedElements);
        Assert.DoesNotContain(_sel.SelectedElements, e => e.ElementType == "RowDefinition");
        Assert.Same(E("btnOk"), _sel.PrimarySelection);
    }

    [Fact]
    public void SelectAll_WithoutDocument_DoesNothing()
    {
        var sel = new SelectionManager();
        sel.SelectAll();
        Assert.False(sel.HasSelection);
    }

    [Fact]
    public void ClearSelection_ClearsFlagsAndRaisesOnce()
    {
        _sel.SetSelection([E("btnOk"), E("txtName")]);
        var count = _events.Count;
        _sel.ClearSelection();
        Assert.False(_sel.HasSelection);
        Assert.False(E("btnOk").IsSelected);
        Assert.False(E("txtName").IsSelected);
        Assert.Equal(count + 1, _events.Count);
        Assert.Equal(2, _events[^1].Removed.Count);
        _sel.ClearSelection();
        Assert.Equal(count + 1, _events.Count);
    }

    [Fact]
    public void Prune_DropsDetachedElements()
    {
        _sel.SetSelection([E("btnOk"), E("txtName")]);
        var btn = E("btnOk");
        btn.Detach();
        _sel.Prune();
        Assert.Equal([E("txtName")], _sel.SelectedElements);
        Assert.False(btn.IsSelected);
        Assert.Equal([btn], _events[^1].Removed);

        var count = _events.Count;
        _sel.Prune(); // nothing to prune
        Assert.Equal(count, _events.Count);
    }

    [Fact]
    public void Prune_KeepsElementsInsidePropertyElements()
    {
        var row = Grid.GetPropertyElement("RowDefinitions")!.ElementValues[0];
        _sel.SetSelection(row);
        _sel.Prune();
        Assert.Same(row, _sel.PrimarySelection);
    }

    [Fact]
    public void Prune_AfterRootReplaced_DropsEverything()
    {
        _sel.SetSelection([E("btnOk"), E("txtName")]);
        _doc.RootElement = Samples.Parse(Samples.MvvmMainWindow).RootElement;
        _sel.Prune();
        Assert.False(_sel.HasSelection);
    }

    [Fact]
    public void ChangingDocument_ClearsSelection()
    {
        _sel.SetSelection(E("btnOk"));
        _sel.Document = Samples.Parse(Samples.MainWindow);
        Assert.False(_sel.HasSelection);
        Assert.False(E("btnOk").IsSelected);
    }

    [Fact]
    public void Changes_RaisePropertyChanged()
    {
        var props = new List<string?>();
        ((INotifyPropertyChanged)_sel).PropertyChanged += (_, e) => props.Add(e.PropertyName);
        _sel.SetSelection(E("btnOk"));
        Assert.Contains(nameof(SelectionManager.SelectedElements), props);
        Assert.Contains(nameof(SelectionManager.PrimarySelection), props);
        Assert.Contains(nameof(SelectionManager.HasSelection), props);
        Assert.Contains(nameof(SelectionManager.IsMultiSelection), props);
    }
}
