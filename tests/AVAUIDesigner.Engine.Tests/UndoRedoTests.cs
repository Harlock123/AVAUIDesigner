using System.ComponentModel;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Engine.UndoRedo.Commands;

namespace AVAUIDesigner.Engine.Tests;

public class UndoRedoTests
{
    private static (DesignDocument Doc, UndoRedoStack Stack) Setup(string? xaml = null)
    {
        var doc = Samples.Parse(xaml ?? Samples.MainWindow);
        return (doc, new UndoRedoStack { Document = doc });
    }

    private static string X(DesignDocument doc) => Samples.Serialize(doc);

    /// <summary>Executes through the stack and verifies undo/redo restore the exact before/after XAML.</summary>
    private static void AssertUndoRedo(DesignDocument doc, UndoRedoStack stack, IDesignCommand command, Action<DesignDocument>? verifyAfter = null)
    {
        var before = X(doc);
        stack.Execute(command);
        var after = X(doc);
        Assert.NotEqual(before, after);
        verifyAfter?.Invoke(doc);

        stack.Undo();
        Assert.Equal(before, X(doc));
        stack.Redo();
        Assert.Equal(after, X(doc));
        verifyAfter?.Invoke(doc);
        stack.Undo();
        Assert.Equal(before, X(doc));
    }

    private static DesignElement Btn(DesignDocument d) => d.NamedElements["btnOk"];
    private static DesignElement Grid(DesignDocument d) => d.RootElement.Children[0];

    // ---------- Stack behavior ----------

    [Fact]
    public void NewStack_HasNothingToUndo()
    {
        var (_, stack) = Setup();
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal(string.Empty, stack.UndoDescription);
        Assert.Equal(string.Empty, stack.RedoDescription);
        stack.Undo(); // no-op
        stack.Redo(); // no-op
    }

    [Fact]
    public void Execute_WithoutDocument_Throws()
    {
        var stack = new UndoRedoStack();
        Assert.Throws<InvalidOperationException>(() => stack.Execute(new DelegateCommand("x", _ => { }, _ => { })));
    }

    [Fact]
    public void CanUndoCanRedo_AndDescriptions_TrackHistory()
    {
        var (doc, stack) = Setup();
        stack.Execute(new SetPropertyCommand(Btn(doc), "Content", "Go"));
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal("Set btnOk.Content", stack.UndoDescription);

        stack.Execute(new ResizeElementCommand(Btn(doc), new Dictionary<string, string?> { ["Width"] = "100" }));
        Assert.Equal("Resize btnOk", stack.UndoDescription);
        Assert.Equal(2, stack.UndoCount);

        stack.Undo();
        Assert.True(stack.CanUndo);
        Assert.True(stack.CanRedo);
        Assert.Equal("Set btnOk.Content", stack.UndoDescription);
        Assert.Equal("Resize btnOk", stack.RedoDescription);

        stack.Undo();
        Assert.False(stack.CanUndo);
        Assert.Equal("Set btnOk.Content", stack.RedoDescription);
    }

    [Fact]
    public void Execute_ClearsRedo()
    {
        var (doc, stack) = Setup();
        stack.Execute(new SetPropertyCommand(Btn(doc), "Content", "A"));
        stack.Undo();
        Assert.True(stack.CanRedo);
        stack.Execute(new SetPropertyCommand(Btn(doc), "Content", "B"));
        Assert.False(stack.CanRedo);
        stack.Redo(); // nothing to redo
        Assert.Equal("B", Btn(doc).GetValue("Content"));
    }

    [Fact]
    public void Execute_Undo_Redo_SetIsDirty()
    {
        var (doc, stack) = Setup();
        Assert.False(doc.IsDirty);
        stack.Execute(new SetPropertyCommand(Btn(doc), "Content", "A"));
        Assert.True(doc.IsDirty);
        doc.IsDirty = false;
        stack.Undo();
        Assert.True(doc.IsDirty);
        doc.IsDirty = false;
        stack.Redo();
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void Execute_RaisesPropertyChangedAndChanged()
    {
        var (doc, stack) = Setup();
        var props = new List<string?>();
        ((INotifyPropertyChanged)stack).PropertyChanged += (_, e) => props.Add(e.PropertyName);
        IDesignCommand? changed = null;
        stack.CommandApplied += (_, c) => changed = c;

        var cmd = new SetPropertyCommand(Btn(doc), "Content", "A");
        stack.Execute(cmd);
        Assert.Contains(nameof(UndoRedoStack.CanUndo), props);
        Assert.Contains(nameof(UndoRedoStack.CanRedo), props);
        Assert.Contains(nameof(UndoRedoStack.UndoDescription), props);
        Assert.Same(cmd, changed);
    }

    [Fact]
    public void ClearAndDocumentChange_EmptyTheStacks()
    {
        var (doc, stack) = Setup();
        stack.Execute(new SetPropertyCommand(Btn(doc), "Content", "A"));
        stack.Execute(new SetPropertyCommand(Btn(doc), "Content", "B"));
        stack.Undo();
        stack.Clear();
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);

        stack.Execute(new SetPropertyCommand(Btn(doc), "Content", "C"));
        stack.Document = Samples.Parse(Samples.MainWindow);
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void ManyCommands_UndoAll_RestoresOriginal()
    {
        var (doc, stack) = Setup();
        var original = X(doc);
        var states = new List<string> { original };
        stack.Execute(new SetPropertyCommand(Btn(doc), "Content", "Go")); states.Add(X(doc));
        stack.Execute(new SetNameCommand(Btn(doc), "btnGo")); states.Add(X(doc));
        stack.Execute(new RemoveElementCommand(doc.NamedElements["lblTitle"])); states.Add(X(doc));
        stack.Execute(new AddElementCommand(Grid(doc), new DesignElement("Button", Samples.Registry.GetByElementName("Button")) { Name = "b2" }, 0)); states.Add(X(doc));

        for (var i = states.Count - 1; i > 0; i--)
        {
            Assert.Equal(states[i], X(doc));
            stack.Undo();
        }
        Assert.Equal(original, X(doc));
        for (var i = 1; i < states.Count; i++)
        {
            stack.Redo();
            Assert.Equal(states[i], X(doc));
        }
    }

    // ---------- Commands ----------

    [Fact]
    public void AddElementCommand_AddsChildAtIndex()
    {
        var (doc, stack) = Setup();
        var e = Samples.Registry.GetByElementName("CheckBox")!.DefaultElementFactory();
        e.Name = "chk";
        var cmd = new AddElementCommand(Grid(doc), e, 1);
        Assert.Equal("Add chk", cmd.Description);
        AssertUndoRedo(doc, stack, cmd, d =>
        {
            Assert.Same(e, Grid(d).Children[1]);
            Assert.Same(Grid(d), e.Parent);
        });
        Assert.Null(e.Parent);
        Assert.Equal(3, Grid(doc).Children.Count);
    }

    [Fact]
    public void AddElementCommand_IntoNewPropertyElement_RemovesItOnUndo()
    {
        var (doc, stack) = Setup();
        var menu = new DesignElement("ContextMenu", Samples.Registry.GetByElementName("ContextMenu"));
        AssertUndoRedo(doc, stack, new AddElementCommand(Btn(doc), menu, -1, "ContextMenu"), d =>
        {
            var pe = Btn(d).GetPropertyElement("ContextMenu");
            Assert.NotNull(pe);
            Assert.Same(pe, menu.OwnerProperty);
            Assert.Contains("<Button.ContextMenu>", X(d));
        });
        Assert.Null(Btn(doc).GetPropertyElement("ContextMenu"));
    }

    [Fact]
    public void AddElementCommand_IntoExistingPropertyElement_KeepsItOnUndo()
    {
        var (doc, stack) = Setup();
        var row = new DesignElement("RowDefinition");
        row.SetValue("Height", "2*");
        AssertUndoRedo(doc, stack, new AddElementCommand(Grid(doc), row, 1, "RowDefinitions"),
            d => Assert.Equal(["Auto", "2*", "*", "Auto"], Grid(d).GetPropertyElement("RowDefinitions")!.ElementValues.Select(r => r.GetValue("Height"))));
        Assert.Equal(3, Grid(doc).GetPropertyElement("RowDefinitions")!.ElementValues.Count);
    }

    [Fact]
    public void RemoveElementCommand_RestoresAtSamePosition()
    {
        var (doc, stack) = Setup();
        var txt = doc.NamedElements["txtName"];
        var cmd = new RemoveElementCommand(txt);
        Assert.Equal("Delete txtName", cmd.Description);
        AssertUndoRedo(doc, stack, cmd, d => Assert.False(d.NamedElements.ContainsKey("txtName")));
        Assert.Same(txt, Grid(doc).Children[1]);
        Assert.Same(Grid(doc), txt.Parent);
    }

    [Fact]
    public void RemoveElementCommand_InsidePropertyElement_RestoresIntoPropertyElement()
    {
        var (doc, stack) = Setup();
        var rows = Grid(doc).GetPropertyElement("RowDefinitions")!;
        var middle = rows.ElementValues[1];
        AssertUndoRedo(doc, stack, new RemoveElementCommand(middle), d =>
        {
            Assert.Equal(2, rows.ElementValues.Count);
            Assert.Null(middle.Parent);
            Assert.Null(middle.OwnerProperty);
        });
        Assert.Same(middle, rows.ElementValues[1]);
        Assert.Same(rows, middle.OwnerProperty);
        Assert.Same(Grid(doc), middle.Parent);
        Assert.DoesNotContain(Grid(doc).Children, c => c.ElementType == "RowDefinition");
    }

    [Fact]
    public void RemoveElementCommand_LastValueOfPropertyElement_RoundTrips()
    {
        var (doc, stack) = Setup(Samples.MvvmMainWindow);
        var dc = doc.RootElement.GetPropertyElement("DataContext")!;
        AssertUndoRedo(doc, stack, new RemoveElementCommand(dc.ElementValues[0]));
        Assert.Single(dc.ElementValues);
    }

    [Fact]
    public void RemoveElementCommand_OnRoot_Throws()
    {
        var (doc, stack) = Setup();
        Assert.Throws<InvalidOperationException>(() => stack.Execute(new RemoveElementCommand(doc.RootElement)));
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void MoveElementCommand_ChangesAttachedAndMargin()
    {
        var (doc, stack) = Setup();
        var cmd = new MoveElementCommand(Btn(doc), new Dictionary<string, string?> { ["Grid.Row"] = "1", ["Margin"] = "4", ["Grid.Column"] = "1" });
        Assert.Equal("Move btnOk", cmd.Description);
        Assert.Equal("2", cmd.OldValues["Grid.Row"]);
        Assert.Null(cmd.OldValues["Grid.Column"]);
        Assert.False(cmd.IsNoOp);
        AssertUndoRedo(doc, stack, cmd, d =>
        {
            Assert.Equal("1", Btn(d).GetValue("Grid.Row"));
            Assert.Equal("1", Btn(d).GetValue("Grid.Column"));
        });
        Assert.Null(Btn(doc).GetValue("Grid.Column"));
    }

    [Fact]
    public void MoveElementCommand_OnCanvas_SetsLeftTop()
    {
        var (doc, stack) = Setup("""
            <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Canvas><Button x:Name="b" Canvas.Left="10" Canvas.Top="20" /></Canvas>
            </Window>
            """);
        var b = doc.NamedElements["b"];
        AssertUndoRedo(doc, stack, new MoveElementCommand(b, new Dictionary<string, string?> { ["Canvas.Left"] = "50", ["Canvas.Top"] = "60" }),
            _ => Assert.Equal((50d, 60d), (b.Left, b.Top)));
        Assert.Equal((10d, 20d), (b.Left, b.Top));
    }

    [Fact]
    public void SetPropertyCommand_NoOpDetection()
    {
        var (doc, _) = Setup();
        Assert.True(new SetPropertyCommand(Btn(doc), "Content", "OK").IsNoOp);
        Assert.False(new SetPropertyCommand(Btn(doc), "Content", "Ok").IsNoOp);
    }

    [Fact]
    public void SetPropertyCommand_RemovingValue_RestoresIt()
    {
        var (doc, stack) = Setup();
        AssertUndoRedo(doc, stack, new SetPropertyCommand(Btn(doc), "Margin", null), d => Assert.Null(Btn(d).GetValue("Margin")));
        Assert.Equal("16", Btn(doc).GetValue("Margin"));
    }

    [Fact]
    public void SetPropertyCommand_AddingNewValue_RemovesItOnUndo()
    {
        var (doc, stack) = Setup();
        AssertUndoRedo(doc, stack, new SetPropertyCommand(Btn(doc), "Background", "Red"));
        Assert.DoesNotContain(Btn(doc).Properties, p => p.Name == "Background");
    }

    [Fact]
    public void SetNameCommand_RenamesAndRestores()
    {
        var (doc, stack) = Setup();
        var b = Btn(doc);
        var cmd = new SetNameCommand(b, "btnAccept");
        AssertUndoRedo(doc, stack, cmd, d => Assert.Same(b, d.NamedElements["btnAccept"]));
        Assert.Equal("btnOk", b.Name);
        Assert.Equal("Rename btnOk to btnAccept", cmd.Description);
    }

    [Fact]
    public void SetEventHandlerCommand_SetsAndClears()
    {
        var (doc, stack) = Setup();
        AssertUndoRedo(doc, stack, new SetEventHandlerCommand(Btn(doc), "Click", "Accept_Click"),
            d => Assert.Equal("Accept_Click", Btn(d).GetEventHandler("Click")));
        AssertUndoRedo(doc, stack, new SetEventHandlerCommand(Btn(doc), "Click", null),
            d => Assert.Null(Btn(d).GetEventHandler("Click")));
        Assert.Equal("BtnOk_Click", Btn(doc).GetEventHandler("Click"));
    }

    [Fact]
    public void ReparentCommand_ToDifferentParent()
    {
        var (doc, stack) = Setup("""
            <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <Canvas x:Name="canvas"><Button x:Name="b" Canvas.Left="10" Canvas.Top="20" /></Canvas>
                    <Grid x:Name="grid"><TextBox /></Grid>
                </StackPanel>
            </Window>
            """);
        var b = doc.NamedElements["b"];
        var grid = doc.NamedElements["grid"];
        var cmd = new ReparentCommand(b, grid, 0,
            new Dictionary<string, string?> { ["Canvas.Left"] = null, ["Canvas.Top"] = null, ["Grid.Row"] = "1" });
        Assert.Equal("Move b into grid", cmd.Description);
        AssertUndoRedo(doc, stack, cmd, _ =>
        {
            Assert.Same(grid, b.Parent);
            Assert.Same(b, grid.Children[0]);
            Assert.DoesNotContain(b.AttachedProperties, a => a.OwnerType == "Canvas");
            Assert.Equal("1", b.GetValue("Grid.Row"));
        });
        Assert.Same(doc.NamedElements["canvas"], b.Parent);
        Assert.Equal("10", b.GetValue("Canvas.Left"));
        Assert.Null(b.GetValue("Grid.Row"));
    }

    private const string ThreeButtons = """
        <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <StackPanel><Button x:Name="a" /><Button x:Name="b" /><Button x:Name="c" /></StackPanel>
        </Window>
        """;

    private static string Order(DesignDocument d) => string.Concat(d.RootElement.Children[0].Children.Select(c => c.Name));

    [Theory]
    [InlineData("a", 2, "bac")]  // move down: insert before c
    [InlineData("a", 3, "bca")]  // move down to end
    [InlineData("a", -1, "bca")] // append
    [InlineData("c", 0, "cab")]  // move up to start
    [InlineData("c", 1, "acb")]  // move up one
    [InlineData("b", 0, "bac")]
    public void ReparentCommand_WithinSameParent(string name, int index, string expected)
    {
        var (doc, stack) = Setup(ThreeButtons);
        var panel = doc.RootElement.Children[0];
        var cmd = new ReparentCommand(doc.NamedElements[name], panel, index);
        stack.Execute(cmd);
        Assert.Equal(expected, Order(doc));
        Assert.Equal($"Reorder {name}", cmd.Description);
        stack.Undo();
        Assert.Equal("abc", Order(doc));
        stack.Redo();
        Assert.Equal(expected, Order(doc));
    }

    [Fact]
    public void ReparentCommand_IntoOwnDescendant_Throws()
    {
        var (doc, _) = Setup();
        Assert.Throws<InvalidOperationException>(() => new ReparentCommand(Grid(doc), Btn(doc)));
        Assert.Throws<InvalidOperationException>(() => new ReparentCommand(Grid(doc), Grid(doc)));
    }

    [Fact]
    public void ReparentCommand_OutOfPropertyElement_RestoresIntoPropertyElement()
    {
        var (doc, stack) = Setup("""
            <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel x:Name="panel">
                    <Button x:Name="host"><ToolTip.Tip><TextBlock x:Name="tip" Text="Hi" /></ToolTip.Tip></Button>
                </StackPanel>
            </Window>
            """);
        var tip = doc.NamedElements["tip"];
        var panel = doc.NamedElements["panel"];
        AssertUndoRedo(doc, stack, new ReparentCommand(tip, panel), _ =>
        {
            Assert.Same(panel, tip.Parent);
            Assert.Null(tip.OwnerProperty);
        });
        Assert.Same(doc.NamedElements["host"], tip.Parent);
        Assert.Equal("ToolTip.Tip", tip.OwnerProperty!.Name);
    }

    [Fact]
    public void CompositeCommand_ExecutesInOrderAndUndoesInReverse()
    {
        var (doc, stack) = Setup();
        var log = new List<string>();
        IDesignCommand Rec(string n) => new DelegateCommand(n, _ => log.Add("do " + n), _ => log.Add("undo " + n));
        var composite = new CompositeCommand("Batch", [Rec("1"), Rec("2"), Rec("3")]);
        stack.Execute(composite);
        Assert.Equal("Batch", stack.UndoDescription);
        Assert.Equal(["do 1", "do 2", "do 3"], log);
        log.Clear();
        stack.Undo();
        Assert.Equal(["undo 3", "undo 2", "undo 1"], log);
        Assert.Equal(3, composite.Commands.Count);
    }

    [Fact]
    public void CompositeCommand_OfDependentSteps_RestoresExactState()
    {
        var (doc, stack) = Setup();
        var grid = Grid(doc);
        var border = new DesignElement("Border", Samples.Registry.GetByElementName("Border")) { Name = "wrap" };
        // "Wrap in Border": add border, move button into it, move attached props.
        var composite = new CompositeCommand("Wrap btnOk in Border",
        [
            new AddElementCommand(grid, border, 2),
            new SetPropertyCommand(border, "Grid.Row", "2"),
            new ReparentCommand(Btn(doc), border, -1, new Dictionary<string, string?> { ["Grid.Row"] = null }),
        ]);
        AssertUndoRedo(doc, stack, composite, d =>
        {
            Assert.Same(border, Btn(d).Parent);
            Assert.Equal("2", border.GetValue("Grid.Row"));
            Assert.Null(Btn(d).GetValue("Grid.Row"));
        });
        Assert.Same(grid, Btn(doc).Parent);
        Assert.Equal(2, Btn(doc).IndexInParent);
    }

    [Fact]
    public void ReplaceRootCommand_SwapsTreeAndMetadata()
    {
        var (doc, stack) = Setup();
        var oldRoot = doc.RootElement;
        var replacement = Samples.Parse(Samples.MvvmMainWindow);
        replacement.XClass = "Other.Views.Main";
        var expectedAfter = X(replacement);

        var cmd = new ReplaceRootCommand(replacement);
        Assert.Equal("Edit XAML", cmd.Description);
        AssertUndoRedo(doc, stack, cmd, d =>
        {
            Assert.Equal(expectedAfter, X(d));
            Assert.Equal("Other.Views.Main", d.XClass);
            Assert.Equal("clr-namespace:MyApp.ViewModels", d.GetNamespaceUri("vm"));
            Assert.Null(d.GetNamespaceUri("d"));
            Assert.Same(cmd.NewRoot, d.RootElement);
        });
        Assert.Same(oldRoot, doc.RootElement);
        Assert.Equal("MyApp.Views.MainWindow", doc.XClass);
        Assert.Null(doc.GetNamespaceUri("vm"));
    }

    [Fact]
    public void ReplacePropertyCommand_ElementFormToAttributeForm()
    {
        var (doc, stack) = Setup();
        var grid = Grid(doc);
        var cmd = new ReplacePropertyCommand(grid, "RowDefinitions", new DesignProperty("RowDefinitions", "Auto,*,Auto"));
        Assert.Equal("Edit Grid.RowDefinitions", cmd.Description);
        AssertUndoRedo(doc, stack, cmd, d =>
        {
            Assert.Contains("<Grid RowDefinitions=\"Auto,*,Auto\">", X(d));
            Assert.DoesNotContain("<Grid.RowDefinitions>", X(d));
        });
        Assert.Equal(3, grid.GetPropertyElement("RowDefinitions")!.ElementValues.Count);
    }

    [Fact]
    public void ReplacePropertyCommand_NewElementForm_ReparentsValues()
    {
        var (doc, stack) = Setup();
        var grid = Grid(doc);
        var prop = new DesignProperty("ColumnDefinitions", null) { IsAttributeForm = false };
        var col = new DesignElement("ColumnDefinition");
        col.SetValue("Width", "2*");
        prop.ElementValues.Add(col);
        AssertUndoRedo(doc, stack, new ReplacePropertyCommand(grid, "ColumnDefinitions", prop), d =>
        {
            Assert.Same(grid, col.Parent);
            Assert.Same(prop, col.OwnerProperty);
            Assert.Contains("<ColumnDefinition Width=\"2*\" />", X(d));
        });
        Assert.Null(grid.GetPropertyElement("ColumnDefinitions"));
    }

    [Fact]
    public void ReplacePropertyCommand_WithNull_RemovesProperty()
    {
        var (doc, stack) = Setup();
        AssertUndoRedo(doc, stack, new ReplacePropertyCommand(Grid(doc), "RowDefinitions", null),
            d => Assert.Null(Grid(d).GetPropertyElement("RowDefinitions")));
        // Undo restores the property at its original position in the property list.
        Assert.Equal("RowDefinitions", Grid(doc).Properties[0].Name);
    }
}
