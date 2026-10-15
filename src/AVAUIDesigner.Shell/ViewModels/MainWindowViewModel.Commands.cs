using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Properties;
using AVAUIDesigner.Engine.Serialization;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Engine.UndoRedo.Commands;
using AVAUIDesigner.Shell.Design;
using AVAUIDesigner.Shell.Services;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

public partial class MainWindowViewModel
{
    // ---- File
    public ReactiveCommand<Unit, Unit> NewWindowCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> NewUserControlCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> NewMvvmWindowCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> NewMvvmUserControlCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> OpenProjectCommand { get; private set; } = null!;
    public ReactiveCommand<string, Unit> OpenRecentCommand { get; private set; } = null!;
    public ReactiveCommand<ProjectFileItem, Unit> OpenProjectFileCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SaveCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CloseCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExitCommand { get; private set; } = null!;

    // ---- Edit
    public ReactiveCommand<Unit, Unit> UndoCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> RedoCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CutCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CopyCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> PasteCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DuplicateCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> RenameCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SetTabIndexCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DeselectCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ToggleLockCommand { get; private set; } = null!;

    // ---- Format / arrange
    public ReactiveCommand<string, Unit> WrapInCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> UnwrapCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> GroupInGridCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> BringToFrontCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SendToBackCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> BringForwardCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SendBackwardCommand { get; private set; } = null!;
    public ReactiveCommand<AlignKind, Unit> AlignCommand { get; private set; } = null!;
    public ReactiveCommand<string, Unit> DistributeCommand { get; private set; } = null!;

    // ---- View
    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> Zoom100Command { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ZoomFitCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ViewXamlForSelectionCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> FocusPropertiesCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ToggleSourceCommand { get; private set; } = null!;

    // ---- Build / Tools
    public ReactiveCommand<Unit, Unit> ApplyMvvmBindingsCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ConvertToMvvmCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> PreferencesCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> AboutCommand { get; private set; } = null!;

    private void CreateCommands()
    {
        var hasDoc = this.WhenAnyValue(x => x.CurrentDocument).Select(d => d is not null);
        var hasSelection = SelectionManager.WhenAnyValue(x => x.HasSelection);
        var hasEditable = SelectionManager.WhenAnyValue(x => x.SelectedElements).Select(_ => EditableSelection().Count > 0);
        var single = SelectionManager.WhenAnyValue(x => x.SelectedElements).Select(_ => SelectionManager.SelectedElements.Count == 1);
        var multi = SelectionManager.WhenAnyValue(x => x.SelectedElements).Select(_ => EditableSelection().Count >= 2);
        var three = SelectionManager.WhenAnyValue(x => x.SelectedElements).Select(_ => EditableSelection().Count >= 3);

        NewWindowCommand = Cmd(() => NewDocumentAsync(NewDocumentKind.Window, false));
        NewUserControlCommand = Cmd(() => NewDocumentAsync(NewDocumentKind.UserControl, false));
        NewMvvmWindowCommand = Cmd(() => NewDocumentAsync(NewDocumentKind.Window, true));
        NewMvvmUserControlCommand = Cmd(() => NewDocumentAsync(NewDocumentKind.UserControl, true));
        OpenFileCommand = Cmd(OpenFileAsync);
        OpenProjectCommand = Cmd(async () =>
        {
            if (Dialogs is null) return;
            var path = await Dialogs.OpenFileAsync("Open Project", [FileFilter.CsProj, FileFilter.All]);
            if (path is not null) await OpenProjectAsync(path);
        });
        OpenRecentCommand = ReactiveCommand.CreateFromTask<string>(async path =>
        {
            if (!await ConfirmDiscardAsync()) return;
            if (File.Exists(path)) await LoadFileAsync(path);
            else
            {
                Settings.Ui.RecentFiles.Remove(path);
                RecentFiles.Remove(path);
                StatusMessage = $"{path} no longer exists.";
            }
        });
        Report(OpenRecentCommand);
        OpenProjectFileCommand = ReactiveCommand.CreateFromTask<ProjectFileItem>(async item =>
        {
            if (await ConfirmDiscardAsync()) await LoadFileAsync(item.FullPath);
        });
        Report(OpenProjectFileCommand);
        SaveCommand = Cmd(async () => { await SaveAsync(); }, hasDoc);
        SaveAsCommand = Cmd(async () => { await SaveAsAsync(); }, hasDoc);
        CloseCommand = Cmd(CloseAsync, hasDoc);
        ExitCommand = Cmd(() => ExitRequested?.Invoke(this, EventArgs.Empty));

        UndoCommand = Cmd(() => UndoRedo.Undo(), UndoRedo.WhenAnyValue(x => x.CanUndo));
        RedoCommand = Cmd(() => UndoRedo.Redo(), UndoRedo.WhenAnyValue(x => x.CanRedo));
        CopyCommand = Cmd(CopyAsync, hasEditable);
        CutCommand = Cmd(async () =>
        {
            await CopyAsync();
            DeleteSelection("Cut");
        }, hasEditable);
        PasteCommand = Cmd(PasteAsync, hasDoc);
        DeleteCommand = Cmd(() => DeleteSelection("Delete"), hasEditable);
        SelectAllCommand = Cmd(() => SelectionManager.SelectAll(), hasDoc);
        DuplicateCommand = Cmd(Duplicate, hasEditable);
        RenameCommand = Cmd(RenameAsync, single);
        SetTabIndexCommand = Cmd(SetTabIndexAsync, hasEditable);
        DeselectCommand = Cmd(() => SelectionManager.ClearSelection());
        ToggleLockCommand = Cmd(() =>
        {
            var lockAll = SelectionManager.SelectedElements.Any(e => !e.IsLocked);
            foreach (var e in SelectionManager.SelectedElements) e.IsLocked = lockAll;
            DesignInvalidated?.Invoke(this, EventArgs.Empty);
        }, hasSelection);

        WrapInCommand = ReactiveCommand.Create<string>(WrapIn, hasEditable);
        Report(WrapInCommand);
        UnwrapCommand = Cmd(Unwrap, single);
        GroupInGridCommand = Cmd(() => WrapIn("Grid"), hasEditable);
        BringToFrontCommand = Cmd(() => Reorder(ZOrder.Front), hasEditable);
        SendToBackCommand = Cmd(() => Reorder(ZOrder.Back), hasEditable);
        BringForwardCommand = Cmd(() => Reorder(ZOrder.Forward), hasEditable);
        SendBackwardCommand = Cmd(() => Reorder(ZOrder.Backward), hasEditable);
        AlignCommand = ReactiveCommand.Create<AlignKind>(Align, multi);
        Report(AlignCommand);
        DistributeCommand = ReactiveCommand.Create<string>(d => Distribute(d == "Vertical"), three);
        Report(DistributeCommand);

        ZoomInCommand = Cmd(() => ZoomFactor = Math.Min(8, ZoomFactor * 1.25));
        ZoomOutCommand = Cmd(() => ZoomFactor = Math.Max(0.1, ZoomFactor / 1.25));
        Zoom100Command = Cmd(() => ZoomFactor = 1);
        ZoomFitCommand = Cmd(() => Surface?.ZoomToFit());
        ViewXamlForSelectionCommand = Cmd(() =>
        {
            if (SelectionManager.PrimarySelection is not { } e) return;
            XamlSource.IsSplitViewVisible = true;
            BottomTab = 0;
            XamlSource.NavigateToElement(e);
        }, hasSelection);
        FocusPropertiesCommand = Cmd(() =>
        {
            ShowProperties = true;
            FocusPropertiesRequested?.Invoke(this, EventArgs.Empty);
        });
        ToggleSourceCommand = Cmd(() => XamlSource.IsSplitViewVisible = !XamlSource.IsSplitViewVisible);

        ApplyMvvmBindingsCommand = Cmd(ApplyMvvmBindings, hasDoc);
        ConvertToMvvmCommand = Cmd(ConvertToMvvm, hasDoc);
        PreferencesCommand = Cmd(async () =>
        {
            if (Dialogs is null) return;
            if (await Dialogs.ShowPreferencesAsync(Settings.Clone()) is { } edited) ApplySettings(edited);
        });
        AboutCommand = Cmd(() => ShowMessage("About AVAUIDesigner",
            "AVAUIDesigner 1.0\nA visual XAML designer for Avalonia UI.\nBuilt with .NET 10 and Avalonia 11."));
    }

    private ReactiveCommand<Unit, Unit> Cmd(Action action, IObservable<bool>? canExecute = null)
    {
        var cmd = ReactiveCommand.Create(action, canExecute);
        Report(cmd);
        return cmd;
    }

    private ReactiveCommand<Unit, Unit> Cmd(Func<Task> action, IObservable<bool>? canExecute = null)
    {
        var cmd = ReactiveCommand.CreateFromTask(action, canExecute);
        Report(cmd);
        return cmd;
    }

    private void Report<TIn, TOut>(ReactiveCommand<TIn, TOut> cmd) =>
        cmd.ThrownExceptions.Subscribe(ex => StatusMessage = $"Error: {ex.Message}");

    // ------------------------------------------------------------------ file

    private async Task OpenFileAsync()
    {
        if (Dialogs is null) return;
        if (!await ConfirmDiscardAsync()) return;
        var path = await Dialogs.OpenFileAsync("Open", [FileFilter.Axaml, FileFilter.All]);
        if (path is not null) await LoadFileAsync(path);
    }

    // ------------------------------------------------------------------ selection helpers

    /// <summary>Selected elements that can be edited structurally: not the root, top-most only.</summary>
    private List<DesignElement> EditableSelection()
    {
        var doc = CurrentDocument;
        if (doc is null) return [];
        var set = new HashSet<DesignElement>(SelectionManager.SelectedElements, ReferenceEqualityComparer.Instance);
        return SelectionManager.SelectedElements
            .Where(e => !ReferenceEquals(e, doc.RootElement) && e.Parent is not null)
            .Where(e => !Ancestors(e).Any(set.Contains))
            .ToList();
    }

    private static IEnumerable<DesignElement> Ancestors(DesignElement e)
    {
        for (var p = e.Parent; p is not null; p = p.Parent) yield return p;
    }

    /// <summary>Selects the next (or previous) element in tree order (Tab / Shift+Tab).</summary>
    public void CycleSelection(bool forward)
    {
        if (CurrentDocument is not { } doc) return;
        var all = doc.RootElement.SelfAndDescendants().ToList();
        if (all.Count == 0) return;
        var index = SelectionManager.PrimarySelection is { } p ? all.IndexOf(p) : -1;
        index = forward ? (index + 1) % all.Count : (index <= 0 ? all.Count - 1 : index - 1);
        SelectionManager.SetSelection(all[index]);
    }

    // ------------------------------------------------------------------ add / delete / clipboard

    /// <summary>The container that receives new or pasted elements.</summary>
    private DesignElement? InsertionTarget(string elementType)
    {
        var doc = CurrentDocument;
        if (doc is null) return null;
        var start = SelectionManager.PrimarySelection ?? doc.RootElement;
        for (var e = start; e is not null; e = e.Parent)
        {
            if (e.OwnerProperty is not null) continue;
            if (e.Descriptor is { } d && d.AcceptsChild(elementType) && (d.MaxChildren is null || e.Children.Count < d.MaxChildren))
                return e;
        }
        // The root's layout panel.
        for (var e = doc.RootElement; e is not null; e = e.Children.FirstOrDefault())
            if (e.Descriptor is { } d && d.AcceptsChild(elementType) && (d.MaxChildren is null || e.Children.Count < d.MaxChildren))
                return e;
        return null;
    }

    /// <summary>Toolbox double-click: adds the control at a default position.</summary>
    public void AddControl(ToolboxItem item)
    {
        if (CurrentDocument is not { } doc) return;
        var d = item.Descriptor;
        var element = CreateElement(d);

        if (d.AttachToPropertyOnDrop is { } propName)
        {
            var host = SelectionManager.PrimarySelection;
            while (host is not null && !d.CanAttachTo(host)) host = host.Parent;
            if (host is null)
            {
                StatusMessage = $"Select a control that supports {propName} first.";
                return;
            }
            var prop = new DesignProperty(propName, null) { IsAttributeForm = false };
            prop.ElementValues.Add(element);
            Execute(new ReplacePropertyCommand(host, propName, prop, $"Add {d.ElementName} to {host.DisplayName}"));
            return;
        }

        var target = InsertionTarget(d.ElementName);
        if (target is null)
        {
            StatusMessage = $"There is no container that can hold a {d.ElementName} here.";
            return;
        }
        var offset = 16 + 16 * (target.Children.Count % 10);
        switch (PlacementService.ModeOf(target))
        {
            case ContainerLayoutMode.Canvas:
                element.SetValue("Canvas.Left", offset.ToString());
                element.SetValue("Canvas.Top", offset.ToString());
                break;
            case ContainerLayoutMode.Grid:
                element.SetValue("Margin", $"{offset},{offset},0,0");
                element.SetValue("HorizontalAlignment", "Left");
                element.SetValue("VerticalAlignment", "Top");
                break;
        }
        Execute(new AddElementCommand(target, element));
        SelectionManager.SetSelection(element);
    }

    private void DeleteSelection(string verb)
    {
        var items = EditableSelection();
        if (items.Count == 0) return;
        var commands = items.Select(e => (IDesignCommand)new RemoveElementCommand(e)).ToList();
        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand($"{verb} {commands.Count} elements", commands));
        SelectionManager.ClearSelection();
    }

    private async Task CopyAsync()
    {
        var items = EditableSelection();
        if (items.Count == 0) return;
        var text = string.Join("\n", items.Select(Serializer.SerializeElement));
        _clipboardFallback = text;
        if (Clipboard is not null) await Clipboard.SetTextAsync(text);
        StatusMessage = $"Copied {items.Count} element(s)";
    }

    private async Task PasteAsync()
    {
        if (CurrentDocument is not { } doc) return;
        var text = Clipboard is not null ? await Clipboard.GetTextAsync() : null;
        text ??= _clipboardFallback;
        if (string.IsNullOrWhiteSpace(text)) return;
        List<DesignElement> elements;
        try
        {
            elements = Parser.ParseFragment(text, doc, Registry);
        }
        catch (XamlParseException)
        {
            StatusMessage = "The clipboard does not contain XAML elements.";
            return;
        }
        if (elements.Count == 0) return;

        var target = InsertionTarget(elements[0].ElementType);
        if (target is null)
        {
            StatusMessage = $"There is no container that can hold a {elements[0].ElementType} here.";
            return;
        }
        var used = doc.NamedElements.Keys.ToHashSet();
        var commands = new List<IDesignCommand>();
        foreach (var e in elements)
        {
            Uniquify(e, used);
            foreach (var (k, v) in PlacementService.CleanupForNewParent(e, target)) e.SetValue(k, v);
            Offset(e, target, 10);
            commands.Add(new AddElementCommand(target, e));
        }
        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand($"Paste {commands.Count} elements", commands));
        SelectionManager.SetSelection(elements);
    }

    private void Duplicate()
    {
        var items = EditableSelection().Where(e => e.OwnerProperty is null).ToList();
        if (items.Count == 0 || CurrentDocument is not { } doc) return;
        var used = doc.NamedElements.Keys.ToHashSet();
        var commands = new List<IDesignCommand>();
        var copies = new List<DesignElement>();
        foreach (var e in items)
        {
            var copy = e.DeepClone();
            Uniquify(copy, used);
            Offset(copy, e.Parent!, 10);
            commands.Add(new AddElementCommand(e.Parent!, copy, e.IndexInParent + 1 + copies.Count(c => ReferenceEquals(c.Parent, e.Parent))));
            copies.Add(copy);
        }
        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand($"Duplicate {commands.Count} elements", commands));
        SelectionManager.SetSelection(copies);
    }

    private static void Uniquify(DesignElement element, HashSet<string> used)
    {
        foreach (var e in element.SelfAndAllDescendants())
        {
            if (e.Name is not { } name) continue;
            if (used.Add(name)) continue;
            var stem = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (stem.Length == 0) stem = name;
            for (var i = 1; ; i++)
            {
                var candidate = $"{stem}{i}";
                if (!used.Add(candidate)) continue;
                e.Name = candidate;
                break;
            }
        }
    }

    /// <summary>Shifts a pasted/duplicated element so it does not sit exactly on its source.</summary>
    private static void Offset(DesignElement e, DesignElement parent, double by)
    {
        switch (PlacementService.ModeOf(parent))
        {
            case ContainerLayoutMode.Canvas:
                e.Left = (double.IsNaN(e.Left) ? 0 : e.Left) + by;
                e.Top = (double.IsNaN(e.Top) ? 0 : e.Top) + by;
                break;
            case ContainerLayoutMode.Grid when e.GetValue("HorizontalAlignment") == "Left" && e.GetValue("VerticalAlignment") == "Top":
                var m = e.Margin;
                e.Margin = new Thickness(m.Left + by, m.Top + by, m.Right, m.Bottom);
                break;
        }
    }

    // ------------------------------------------------------------------ rename / tab index

    private async Task RenameAsync()
    {
        if (SelectionManager.PrimarySelection is not { } e || Dialogs is null) return;
        var name = await Dialogs.PromptAsync("Set x:Name", $"Name of this {e.ElementType}:", e.Name ?? string.Empty,
            v => PropertyGridViewModel.ValidateNewName(e, v));
        if (name is null) return;
        var v = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (v != e.Name) Execute(new SetNameCommand(e, v));
    }

    private async Task SetTabIndexAsync()
    {
        var items = EditableSelection();
        if (items.Count == 0 || Dialogs is null) return;
        var current = items[0].GetValue("TabIndex") ?? string.Empty;
        var value = await Dialogs.PromptAsync("Set Tab Index", "Keyboard navigation order (empty = default):", current,
            v => string.IsNullOrWhiteSpace(v) || int.TryParse(v, out _) ? null : "Enter a whole number.");
        if (value is null) return;
        var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var commands = items.Select(e => (IDesignCommand)new SetPropertyCommand(e, "TabIndex", v)).ToList();
        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand("Set Tab Index", commands));
    }

    // ------------------------------------------------------------------ z-order

    private enum ZOrder
    {
        Front,
        Back,
        Forward,
        Backward,
    }

    private void Reorder(ZOrder order)
    {
        var commands = new List<IDesignCommand>();
        foreach (var e in EditableSelection().Where(e => e.OwnerProperty is null))
        {
            var parent = e.Parent!;
            var i = e.IndexInParent;
            var target = order switch
            {
                ZOrder.Front => parent.Children.Count,
                ZOrder.Back => 0,
                ZOrder.Forward => Math.Min(parent.Children.Count, i + 2),
                _ => Math.Max(0, i - 1),
            };
            if (target == i || target == i + 1) continue;
            commands.Add(new ReparentCommand(e, parent, target));
        }
        if (commands.Count > 0) Execute(commands.Count == 1 ? commands[0] : new CompositeCommand($"Reorder {commands.Count} elements", commands));
    }

    // ------------------------------------------------------------------ nudge / align / distribute

    /// <summary>Arrow keys: move the selection by (dx, dy) pixels.</summary>
    public void Nudge(double dx, double dy)
    {
        if (Surface is null) return;
        var commands = new List<IDesignCommand>();
        foreach (var e in EditableSelection().Where(e => !e.IsLocked))
        {
            if (Surface.GetElementBounds(e) is not { } b) continue;
            if (Surface.GetPlacementValues(e, b.Translate(new Vector(dx, dy))) is not { } values) continue;
            var cmd = new MoveElementCommand(e, values);
            if (!cmd.IsNoOp) commands.Add(cmd);
        }
        if (commands.Count > 0) Execute(commands.Count == 1 ? commands[0] : new CompositeCommand($"Nudge {commands.Count} elements", commands));
    }

    private void Align(AlignKind kind)
    {
        if (Surface is null || SelectionManager.PrimarySelection is not { } primary || Surface.GetElementBounds(primary) is not { } reference)
            return;
        var commands = new List<IDesignCommand>();
        foreach (var e in EditableSelection())
        {
            if (ReferenceEquals(e, primary) || e.IsLocked || Surface.GetElementBounds(e) is not { } b) continue;
            var r = kind switch
            {
                AlignKind.Left => b.WithX(reference.Left),
                AlignKind.Center => b.WithX(reference.Center.X - b.Width / 2),
                AlignKind.Right => b.WithX(reference.Right - b.Width),
                AlignKind.Top => b.WithY(reference.Top),
                AlignKind.Middle => b.WithY(reference.Center.Y - b.Height / 2),
                _ => b.WithY(reference.Bottom - b.Height),
            };
            if (Surface.GetPlacementValues(e, r) is { } values) commands.Add(new MoveElementCommand(e, values));
        }
        if (commands.Count > 0) Execute(new CompositeCommand($"Align {kind}", commands));
    }

    private void Distribute(bool vertical)
    {
        if (Surface is null) return;
        var items = EditableSelection().Where(e => !e.IsLocked)
            .Select(e => (Element: e, Bounds: Surface.GetElementBounds(e)))
            .Where(x => x.Bounds is not null).Select(x => (x.Element, Bounds: x.Bounds!.Value))
            .OrderBy(x => vertical ? x.Bounds.Top : x.Bounds.Left).ToList();
        if (items.Count < 3) return;
        var start = vertical ? items[0].Bounds.Top : items[0].Bounds.Left;
        var end = vertical ? items[^1].Bounds.Bottom : items[^1].Bounds.Right;
        var total = items.Sum(x => vertical ? x.Bounds.Height : x.Bounds.Width);
        var gap = (end - start - total) / (items.Count - 1);
        var commands = new List<IDesignCommand>();
        var pos = start;
        foreach (var (e, b) in items)
        {
            var r = vertical ? b.WithY(pos) : b.WithX(pos);
            pos += (vertical ? b.Height : b.Width) + gap;
            if (Surface.GetPlacementValues(e, r) is { } values)
            {
                var cmd = new MoveElementCommand(e, values);
                if (!cmd.IsNoOp) commands.Add(cmd);
            }
        }
        if (commands.Count > 0) Execute(new CompositeCommand(vertical ? "Distribute vertically" : "Distribute horizontally", commands));
    }

    // ------------------------------------------------------------------ wrap / unwrap

    /// <summary>Wraps the selected siblings in a new container (Grid, StackPanel, Border, ScrollViewer...).</summary>
    public void WrapIn(string containerType)
    {
        var items = EditableSelection().Where(e => e.OwnerProperty is null).ToList();
        if (items.Count == 0 || Registry.GetByElementName(containerType) is not { } descriptor) return;
        var parent = items[0].Parent!;
        if (items.Any(e => !ReferenceEquals(e.Parent, parent)))
        {
            StatusMessage = "Wrap In needs elements that share the same parent.";
            return;
        }
        items = items.OrderBy(e => e.IndexInParent).ToList();

        var container = CreateElement(descriptor);
        container.Children.Clear();
        foreach (var k in new[] { "Width", "Height" }) container.SetValue(k, null);
        // Single-child containers get an inner Grid when several elements are wrapped.
        var inner = container;
        if (descriptor.MaxChildren == 1 && items.Count > 1 && Registry.GetByElementName("Grid") is { } gridDescriptor)
        {
            inner = new DesignElement("Grid", gridDescriptor);
            container.AddChild(inner);
        }

        var bounds = items.Select(e => Surface?.GetElementBounds(e)).ToList();
        Rect? union = null;
        foreach (var b in bounds)
            if (b is { } r) union = union is { } u ? u.Union(r) : r;

        // Position the container where the elements were.
        foreach (var (k, v) in PlacementService.CleanupForNewParent(container, parent)) container.SetValue(k, v);
        if (union is { } area && PlacementService.IsAbsolute(parent))
        {
            container.SetValue("Width", PlacementService.Fmt(area.Width));
            container.SetValue("Height", PlacementService.Fmt(area.Height));
            if (Surface?.GetPlacementValuesIn(parent, container, area) is { } values)
                foreach (var (k, v) in values) if (k is not ("Width" or "Height")) container.SetValue(k, v);
        }

        var commands = new List<IDesignCommand> { new AddElementCommand(parent, container, items[0].IndexInParent) };
        for (var i = 0; i < items.Count; i++)
        {
            var e = items[i];
            var values = PlacementService.CleanupForNewParent(e, inner);
            if (union is { } area2 && bounds[i] is { } b && PlacementService.ModeOf(inner) == ContainerLayoutMode.Grid && items.Count > 1)
            {
                values["Margin"] = XamlValueConverter.Serialize(new Thickness(Math.Round(b.X - area2.X), Math.Round(b.Y - area2.Y), 0, 0), typeof(Thickness));
                values["HorizontalAlignment"] = "Left";
                values["VerticalAlignment"] = "Top";
                if (e.GetValue("Width") is null) values["Width"] = PlacementService.Fmt(b.Width);
                if (e.GetValue("Height") is null) values["Height"] = PlacementService.Fmt(b.Height);
            }
            else if (items.Count == 1)
            {
                // A single wrapped element fills its new container.
                values["Margin"] = null;
                values["HorizontalAlignment"] = null;
                values["VerticalAlignment"] = null;
                if (PlacementService.IsAbsolute(parent))
                {
                    values["Width"] = null;
                    values["Height"] = null;
                }
            }
            commands.Add(new ReparentCommand(e, inner, -1, values));
        }
        Execute(new CompositeCommand($"Wrap in {containerType}", commands));
        SelectionManager.SetSelection(container);
    }

    /// <summary>Removes the selected container, promoting its children to its parent.</summary>
    private void Unwrap()
    {
        if (SelectionManager.PrimarySelection is not { Parent: { } parent, OwnerProperty: null } container
            || container.Descriptor?.IsContainer != true || container.Children.Count == 0)
            return;
        var index = container.IndexInParent;
        var commands = new List<IDesignCommand>();
        var children = container.Children.ToList();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var values = PlacementService.CleanupForNewParent(child, parent);
            if (PlacementService.IsAbsolute(parent) && Surface?.GetElementBounds(child) is { } b
                && Surface.GetPlacementValuesIn(parent, child, b) is { } placement)
                foreach (var (k, v) in placement) values[k] = v;
            commands.Add(new ReparentCommand(child, parent, index + 1 + i, values));
        }
        commands.Add(new RemoveElementCommand(container));
        Execute(new CompositeCommand($"Unwrap {container.DisplayName}", commands));
        SelectionManager.SetSelection(children);
    }

    // ------------------------------------------------------------------ MVVM

    private void ApplyMvvmBindings()
    {
        if (CurrentDocument is not { } doc) return;
        var commands = MvvmBindingHeuristics.Suggest(doc)
            .Select(s => (IDesignCommand)new SetPropertyCommand(s.Element, s.Property, s.Binding)).ToList();
        if (commands.Count == 0)
        {
            StatusMessage = "No named controls without bindings were found (name controls to bind them).";
            return;
        }
        if (doc.CodeStyle != CodeBehindStyle.MVVM) ConvertToMvvm();
        Execute(new CompositeCommand("Apply MVVM bindings", commands));
        BottomTab = 2;
    }

    private void ConvertToMvvm()
    {
        if (CurrentDocument is not { } doc || doc.CodeStyle == CodeBehindStyle.MVVM && doc.RootElement.GetValue("x:DataType") is not null) return;
        var options = BuildCodeGenOptions(doc);
        var vmNs = options.EffectiveViewModelNamespace;
        var vmClass = _viewModelGenerator.GetViewModelClassName(options);
        var before = doc.Namespaces.ToList();
        var snapshot = doc.RootElement.DeepClone();
        var oldStyle = doc.CodeStyle;
        Execute(new DelegateCommand("Convert to MVVM",
            d => MvvmScaffolder.Apply(d, vmNs, vmClass),
            d =>
            {
                d.Namespaces.Clear();
                d.Namespaces.AddRange(before);
                d.RootElement.SetValue("x:DataType", snapshot.GetValue("x:DataType"));
                var dc = d.RootElement.GetPropertyElement("DataContext");
                if (dc is not null) d.RootElement.Properties.Remove(dc);
                if (snapshot.GetPropertyElement("DataContext") is { } oldDc) d.RootElement.Properties.Add(oldDc.Clone(d.RootElement));
                d.CodeStyle = oldStyle;
            }));
        BottomTab = 2;
    }
}
