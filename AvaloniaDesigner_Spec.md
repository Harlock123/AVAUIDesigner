# AvaloniaDesigner — Complete Technical Specification
### A Visual XAML Designer for Avalonia UI, built with .NET 10 + Avalonia

**Document Version:** 1.0  
**Target Audience:** Claude Code (AI implementation agent)  
**Implementation Language:** C# 13 / .NET 10  
**UI Framework:** Avalonia UI 11.x  

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [High-Level Architecture](#2-high-level-architecture)
3. [Solution & Project Structure](#3-solution--project-structure)
4. [Core Subsystems](#4-core-subsystems)
   - 4.1 Design Surface
   - 4.2 Toolbox Panel
   - 4.3 Property Grid Panel
   - 4.4 Document Outline / Visual Tree Panel
   - 4.5 XAML Source View
   - 4.6 XAML Serialization Engine
   - 4.7 Code-Behind Generation Engine
   - 4.8 Undo / Redo Stack
   - 4.9 Snap & Layout Guides
   - 4.10 Selection & Adorner System
5. [Data Model](#5-data-model)
6. [Control Descriptor Catalog](#6-control-descriptor-catalog)
7. [Property System](#7-property-system)
8. [Event Wiring System](#8-event-wiring-system)
9. [MVVM Code Generation](#9-mvvm-code-generation)
10. [File & Project Integration](#10-file--project-integration)
11. [Main Window Layout](#11-main-window-layout)
12. [Keyboard Shortcuts & Context Menus](#12-keyboard-shortcuts--context-menus)
13. [Extensibility / Plugin Model](#13-extensibility--plugin-model)
14. [Settings & Preferences](#14-settings--preferences)
15. [Non-Functional Requirements](#15-non-functional-requirements)
16. [Implementation Phases & Milestones](#16-implementation-phases--milestones)
17. [Key API Contracts](#17-key-api-contracts)
18. [Appendix A — Generated File Samples](#appendix-a--generated-file-samples)

---

## 1. Project Overview

**AvaloniaDesigner** is a standalone desktop application that provides a drag-and-drop visual WYSIWYG designer for Avalonia UI XAML (`.axaml`) files, closely mirroring the experience of the Windows Forms designer embedded in Visual Studio 2019/2022. The application itself is built with .NET 10 and Avalonia 11.x so it runs cross-platform (Windows, macOS, Linux).

### Goals

| Goal | Description |
|------|-------------|
| **Visual design** | Drag controls from a toolbox onto a live-rendered canvas |
| **Property editing** | Side panel shows all Avalonia properties for the selected control, editable inline |
| **Event wiring** | Events tab in the property panel allows naming handler methods that are auto-generated in code-behind |
| **XAML sync** | The live AXAML source view stays in sync with the canvas at all times |
| **Code-behind generation** | Emits a `.axaml.cs` partial class with InitializeComponent, field declarations, and event handler stubs |
| **MVVM scaffolding** | Optional generation of a ViewModel skeleton with bound properties and commands |
| **Project awareness** | Opens and saves files inside a real `.csproj` Avalonia project on disk |
| **Undo/Redo** | Full unlimited undo/redo for every design action |

### Non-Goals (out of scope v1)

- Integrated compiler / build runner
- Live preview with actual data bindings resolved at runtime
- MAUI / WPF / WinUI targeting
- A full IDE (no file tree, no debugger)

---

## 2. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        MainWindow (Avalonia)                         │
│                                                                      │
│  ┌───────────┐  ┌──────────────────────────┐  ┌──────────────────┐  │
│  │  Toolbox  │  │     Design Surface       │  │  Property Panel  │  │
│  │  Panel    │  │  (DesignCanvas)          │  │  (PropGrid)      │  │
│  │           │  │                          │  │  ┌────────────┐  │  │
│  │ [Button]  │  │  ┌────────────────────┐  │  │  │ Properties │  │  │
│  │ [TextBox] │  │  │  DesignAdornerLayer│  │  │  │ tab        │  │  │
│  │ [Grid]    │  │  │  (handles/grids)   │  │  │  ├────────────┤  │  │
│  │ ...       │  │  └────────────────────┘  │  │  │ Events tab │  │  │
│  └───────────┘  │  ┌────────────────────┐  │  │  └────────────┘  │  │
│                 │  │  RenderedPreview   │  │  └──────────────────┘  │
│                 │  │  (Avalonia Controls)│  │                        │
│                 │  └────────────────────┘  │  ┌──────────────────┐  │
│                 └──────────────────────────┘  │  Document        │  │
│                                               │  Outline         │  │
│  ┌────────────────────────────────────────────┤  (Visual Tree)   │  │
│  │  XAML Source View  (AvaloniaEdit / Code) │  └──────────────────┘  │
│  └────────────────────────────────────────────┘                      │
└─────────────────────────────────────────────────────────────────────┘

        ┌──────────────────────────────────────────┐
        │           Core Engine Layer               │
        │                                           │
        │  DesignDocument (in-memory DOM)           │
        │  XamlSerializer  ←→  XamlParser           │
        │  CodeBehindGenerator                      │
        │  ViewModelGenerator                       │
        │  UndoRedoStack                            │
        │  SelectionManager                         │
        │  ControlDescriptorRegistry                │
        └──────────────────────────────────────────┘
```

### Layered Responsibilities

| Layer | Responsibility |
|-------|----------------|
| **UI Layer** | All Avalonia panels, windows, dialogs, adorners |
| **ViewModel Layer** | ReactiveUI ViewModels binding UI to engine |
| **Engine Layer** | Design document DOM, serialization, code generation, undo/redo |
| **Plugin Layer** | Control descriptor plugins, property editor plugins |

---

## 3. Solution & Project Structure

```
AvaloniaDesigner/
├── AvaloniaDesigner.sln
│
├── src/
│   ├── AvaloniaDesigner.App/              # Entry point, app lifecycle
│   │   ├── Program.cs
│   │   ├── App.axaml
│   │   └── App.axaml.cs
│   │
│   ├── AvaloniaDesigner.Shell/            # Main window + panels (UI)
│   │   ├── MainWindow.axaml
│   │   ├── MainWindow.axaml.cs
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── ToolboxViewModel.cs
│   │   │   ├── PropertyGridViewModel.cs
│   │   │   ├── DocumentOutlineViewModel.cs
│   │   │   └── XamlSourceViewModel.cs
│   │   ├── Views/
│   │   │   ├── DesignSurfaceView.axaml
│   │   │   ├── ToolboxView.axaml
│   │   │   ├── PropertyGridView.axaml
│   │   │   ├── DocumentOutlineView.axaml
│   │   │   └── XamlSourceView.axaml
│   │   └── Controls/
│   │       ├── DesignCanvas.cs            # Custom Panel = design surface
│   │       ├── AdornerLayer.cs
│   │       ├── SelectionHandle.cs
│   │       ├── SnapLine.cs
│   │       └── PropertyEditors/           # per-type inline editors
│   │           ├── ColorPropertyEditor.axaml
│   │           ├── FontPropertyEditor.axaml
│   │           ├── EnumPropertyEditor.axaml
│   │           ├── ThicknessPropertyEditor.axaml
│   │           ├── BoolPropertyEditor.axaml
│   │           └── StringPropertyEditor.axaml
│   │
│   ├── AvaloniaDesigner.Engine/           # Pure logic, no Avalonia UI dependency
│   │   ├── Document/
│   │   │   ├── DesignDocument.cs
│   │   │   ├── DesignElement.cs
│   │   │   └── DesignProperty.cs
│   │   ├── Serialization/
│   │   │   ├── XamlSerializer.cs
│   │   │   ├── XamlParser.cs
│   │   │   └── XamlNamespaceMap.cs
│   │   ├── CodeGeneration/
│   │   │   ├── CodeBehindGenerator.cs
│   │   │   ├── ViewModelGenerator.cs
│   │   │   └── Templates/               # Scriban/T4 templates
│   │   │       ├── CodeBehind.sbntxt
│   │   │       └── ViewModel.sbntxt
│   │   ├── UndoRedo/
│   │   │   ├── UndoRedoStack.cs
│   │   │   └── Commands/
│   │   │       ├── AddElementCommand.cs
│   │   │       ├── RemoveElementCommand.cs
│   │   │       ├── MoveElementCommand.cs
│   │   │       ├── ResizeElementCommand.cs
│   │   │       └── SetPropertyCommand.cs
│   │   ├── Selection/
│   │   │   └── SelectionManager.cs
│   │   └── Descriptors/
│   │       ├── ControlDescriptor.cs
│   │       ├── PropertyDescriptor.cs
│   │       ├── EventDescriptor.cs
│   │       └── ControlDescriptorRegistry.cs
│   │
│   └── AvaloniaDesigner.Controls/        # Descriptor definitions for Avalonia controls
│       ├── StandardControlDescriptors.cs
│       └── Descriptors/
│           ├── ButtonDescriptor.cs
│           ├── TextBoxDescriptor.cs
│           ├── ... (one per control)
│
└── tests/
    ├── AvaloniaDesigner.Engine.Tests/
    └── AvaloniaDesigner.Shell.Tests/
```

### NuGet Dependencies

| Package | Purpose |
|---------|---------|
| `Avalonia` 11.x | UI framework |
| `Avalonia.Themes.Fluent` | Default theme |
| `AvaloniaEdit` | XAML source editor with syntax highlighting |
| `ReactiveUI.Avalonia` | MVVM reactive framework |
| `DynamicData` | Reactive collection pipelines |
| `Scriban` | Code generation templates |
| `System.Xml.Linq` | XAML manipulation |
| `Microsoft.Extensions.DependencyInjection` | DI container |
| `FluentAvalonia.UI` | Additional styled controls (optional) |

---

## 4. Core Subsystems

---

### 4.1 Design Surface

#### Overview
The **DesignCanvas** is a custom Avalonia `Panel` subclass that is the heart of the designer. It renders a live representation of the form/window being designed, overlaid with an adorner layer that provides selection handles, resize grips, and snap guides.

#### Architecture

```
DesignCanvas (Panel)
├── BackgroundGrid (Canvas with crosshatch pattern)
├── PreviewHost (ContentPresenter)
│   └── [Dynamically created Avalonia controls from DesignDocument]
└── AdornerLayer (Canvas, top z-order, pointer events transparent by default)
    ├── SelectionRect (rubber-band selection)
    ├── SelectionHandles[] (per selected element)
    ├── SnapLines[]
    └── DropTarget indicators
```

#### Behaviors

**Drag from Toolbox:**
1. User begins drag on a ToolboxItem.
2. `DragDrop.DoDragDrop` is called with `ControlDescriptor` as payload.
3. DesignCanvas handles `DragOver` — shows ghost preview at cursor position with snap feedback.
4. On `Drop` — creates a new `DesignElement`, assigns default Width/Height from descriptor, records `AddElementCommand` to undo stack, re-renders.

**Click to Select:**
1. Pointer press on a rendered child control hits the AdornerLayer via hit-testing override.
2. `SelectionManager.SetSelection(element)` is called.
3. AdornerLayer draws resize handles (8-point) around the selected control's bounds.
4. PropertyGridViewModel is notified, showing that element's properties.

**Move via Drag:**
1. Pointer pressed on a selected element, not on a resize handle.
2. On pointer move: compute delta, update element's `Canvas.Left`/`Canvas.Top` (or `Grid.Row`/`Grid.Column` if parent is Grid).
3. Snap lines are computed against other elements and the parent edges.
4. On pointer release: commit `MoveElementCommand` to undo stack.

**Resize via Handles:**
1. Pointer pressed on one of the 8 resize handles.
2. On pointer move: update Width/Height/position based on which handle.
3. Enforce MinWidth/MinHeight from descriptor.
4. On pointer release: commit `ResizeElementCommand`.

**Multi-Select:**
- Hold `Ctrl` while clicking to add/remove from selection.
- Rubber-band selection: drag on empty canvas area to select all elements whose bounds intersect the rubber-band rect.
- When multiple elements are selected, the property grid shows only shared common properties.

**Parent Container Awareness:**
- If the user drops a control onto a `Grid`, a column/row picker overlay appears allowing the user to assign `Grid.Row` and `Grid.Column`.
- If the user drops onto a `StackPanel` or `DockPanel`, position is determined by insertion index (shown as a blue insertion line).
- If the user drops onto a `Canvas`, absolute positioning (Left/Top) is used.

#### DesignCanvas Public API

```csharp
public class DesignCanvas : Panel
{
    public static readonly StyledProperty<DesignDocument> DocumentProperty;
    public static readonly StyledProperty<SelectionManager> SelectionManagerProperty;
    
    // Raised when user action would change the document
    public event EventHandler<DesignActionEventArgs> ActionRequested;
    
    // Force re-render from current document state
    public void InvalidateDesign();
    
    // Hit-test a point to a DesignElement
    public DesignElement? HitTest(Point position);
    
    // Scroll/zoom
    public double ZoomFactor { get; set; }
    public void ZoomToFit();
    public void ZoomTo(double factor);
}
```

---

### 4.2 Toolbox Panel

#### Overview
A categorized list of Avalonia controls that the user drags onto the design surface. Mirrors the VS toolbox exactly.

#### Structure

```
ToolboxView
└── ScrollViewer
    └── ItemsControl (bound to ToolboxViewModel.Categories)
        └── [Category] (Expander)
            └── [ToolboxItem] (draggable)
                ├── Icon (24×24 SVG or PNG)
                └── Label (control name)
```

#### ToolboxViewModel

```csharp
public class ToolboxViewModel : ReactiveObject
{
    public ObservableCollection<ToolboxCategory> Categories { get; }
    public string SearchText { get; set; }            // filters items
    public ObservableCollection<ToolboxItem> FilteredItems { get; }
    public ToolboxItem? SelectedItem { get; set; }
    
    // Double-click adds control at default position on canvas
    public ReactiveCommand<ToolboxItem, Unit> AddControlCommand { get; }
}

public class ToolboxCategory
{
    public string Name { get; set; }
    public bool IsExpanded { get; set; }
    public ObservableCollection<ToolboxItem> Items { get; }
}

public class ToolboxItem
{
    public ControlDescriptor Descriptor { get; set; }
    public string DisplayName { get; set; }
    public IImage Icon { get; set; }
}
```

#### Categories (Default)

| Category | Controls |
|----------|----------|
| **Common Controls** | Button, TextBox, TextBlock, CheckBox, RadioButton, ComboBox, ListBox, Image, ProgressBar, Slider |
| **Containers** | Grid, StackPanel, DockPanel, WrapPanel, Canvas, Border, ScrollViewer, Expander, TabControl, SplitView |
| **Data** | DataGrid, TreeView, ListBox (data-bound), ItemsControl |
| **Menus & Toolbars** | Menu, ContextMenu, ToolTip, MenuItem |
| **Dialogs & Overlays** | Popup, Flyout, ContentDialog (if FluentAvalonia) |
| **Input** | NumericUpDown, DatePicker, TimePicker, AutoCompleteBox, ToggleButton, ToggleSwitch |
| **Layout** | Separator, GridSplitter, Viewbox, UniformGrid |
| **Media & Display** | Shape (Rectangle, Ellipse, Line, Path), Canvas, DrawingPresenter |

#### Drag Initiation

```csharp
// In ToolboxItemView.axaml.cs code-behind or via attached behavior:
private void OnPointerPressed(object sender, PointerPressedEventArgs e)
{
    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
        var data = new DataObject();
        data.Set("ControlDescriptor", this.DataContext as ToolboxItem);
        DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }
}
```

---

### 4.3 Property Grid Panel

#### Overview
Displays all designable properties and events of the currently selected `DesignElement`. Mirrors the VS Properties window. Supports alphabetical and categorized sorting, search/filter, and inline editing via type-specific editors.

#### Layout

```
PropertyGridView
├── Toolbar
│   ├── [Sort Alphabetically] toggle button
│   ├── [Sort by Category] toggle button
│   └── [Search] TextBox
├── TabControl
│   ├── Tab: "Properties"  (property list)
│   └── Tab: "Events"      (event handler list)
└── StatusBar
    └── Description text for selected property
```

#### Properties Tab

```
PropertyGridView (Properties tab)
└── ItemsControl (bound to PropertyGridViewModel.VisibleProperties)
    └── [PropertyRow]
        ├── Name column (label, non-editable, bold if non-default value)
        └── Value column (appropriate PropertyEditor control)
```

Each `PropertyRow` uses a `DataTemplateSelector` to pick the right editor:

| Property Type | Editor Control |
|---------------|---------------|
| `string` | `StringPropertyEditor` (TextBox) |
| `bool` | `BoolPropertyEditor` (CheckBox) |
| `enum` | `EnumPropertyEditor` (ComboBox) |
| `Color` / `IBrush` | `ColorPropertyEditor` (color picker swatch + popup) |
| `Thickness` | `ThicknessPropertyEditor` (4 linked NumericUpDown) |
| `CornerRadius` | `CornerRadiusPropertyEditor` (4 NumericUpDown) |
| `double` / `int` | `NumericPropertyEditor` (NumericUpDown) |
| `FontFamily` | `FontPropertyEditor` (dropdown with preview) |
| `FontSize` | `NumericPropertyEditor` |
| `FontStyle`, `FontWeight` | `EnumPropertyEditor` |
| `GridLength` | `GridLengthPropertyEditor` (NumericUpDown + Auto/Star selector) |
| `HorizontalAlignment` / `VerticalAlignment` | `EnumPropertyEditor` (ComboBox with icon glyphs) |
| `IDataTemplate` | `DataTemplateEditor` (opens XAML snippet editor dialog) |
| `object` (complex) | Read-only display + `[...]` expand button |

#### PropertyGridViewModel

```csharp
public class PropertyGridViewModel : ReactiveObject
{
    public DesignElement? SelectedElement { get; set; }
    
    public ObservableCollection<PropertyRowViewModel> AllProperties { get; }
    public ObservableCollection<PropertyRowViewModel> VisibleProperties { get; }  // filtered/sorted
    
    public string SearchText { get; set; }
    public PropertySortMode SortMode { get; set; }  // Alphabetical | Categorized
    
    // Called when user commits a change in any editor
    public void SetProperty(string propertyName, object? newValue);
    
    // Reset to default
    public ReactiveCommand<PropertyRowViewModel, Unit> ResetPropertyCommand { get; }
}

public class PropertyRowViewModel : ReactiveObject
{
    public string Name { get; }
    public string Category { get; }
    public string Description { get; }
    public object? Value { get; set; }
    public object? DefaultValue { get; }
    public bool IsModified => !Equals(Value, DefaultValue);
    public Type PropertyType { get; }
    public bool IsReadOnly { get; }
}
```

#### Events Tab

```
EventsTab
└── ItemsControl (bound to PropertyGridViewModel.Events)
    └── [EventRow]
        ├── Name column (event name, e.g. "Click", "TextChanged")
        └── Handler column (TextBox — user types handler method name)
```

When the user types a handler name and presses Enter (or Tab):
1. The event is recorded on the `DesignElement`.
2. The code-behind generator includes an empty handler stub.
3. If a handler name already exists in the code file, no duplicate is emitted.

Double-clicking an existing handler name navigates to the XAML source view and scrolls to that event attribute.

---

### 4.4 Document Outline / Visual Tree Panel

#### Overview
Displays the full hierarchical tree of all `DesignElement` nodes in the current document. Identical to the VS Document Outline window.

```
DocumentOutlineView
└── TreeView (bound to DocumentOutlineViewModel.RootElement)
    └── [TreeNode: Window / UserControl]
        └── [TreeNode: Grid]
            ├── [TreeNode: Button]
            ├── [TreeNode: TextBox "txtName"]
            └── [TreeNode: StackPanel]
                └── [TreeNode: Label]
```

#### Behaviors
- Clicking a node selects the corresponding element on the design surface (and vice versa — selection is two-way).
- Drag-and-drop within the tree to re-parent elements.
- Right-click context menu: Cut, Copy, Paste, Delete, Bring to Front, Send to Back, Wrap In (Grid/StackPanel/Border...).
- Element icon is the same icon as in the toolbox.
- Element label = `x:Name` attribute if set, else the type name (e.g. `Button`).

#### DocumentOutlineViewModel

```csharp
public class DocumentOutlineViewModel : ReactiveObject
{
    public DesignElementNodeViewModel? RootNode { get; }
    public SelectionManager SelectionManager { get; }
    
    public ReactiveCommand<DesignElementNodeViewModel, Unit> SelectNodeCommand { get; }
    public ReactiveCommand<(DesignElementNodeViewModel source, DesignElementNodeViewModel target), Unit> ReparentCommand { get; }
}

public class DesignElementNodeViewModel : ReactiveObject
{
    public DesignElement Element { get; }
    public string DisplayName { get; }           // x:Name or type name
    public IImage Icon { get; }
    public bool IsSelected { get; set; }
    public bool IsExpanded { get; set; }
    public ObservableCollection<DesignElementNodeViewModel> Children { get; }
}
```

---

### 4.5 XAML Source View

#### Overview
A split pane below the design surface showing the raw `.axaml` source, kept in sync with the visual designer. Uses **AvaloniaEdit** (a port of AvaloniaTextEditor) for syntax highlighting, code folding, and IntelliSense-like XML completion.

#### Sync Strategy
- **Designer → Source:** On every committed design action (end of drag, property change, etc.) the `XamlSerializer` regenerates the XAML string and the source view is updated with a diff-aware update to preserve cursor position where possible.
- **Source → Designer:** On every keystroke in the source view (after a 400ms debounce), `XamlParser` attempts a re-parse. If successful, the `DesignDocument` is updated and the design surface re-renders. If parse fails, the source view shows an error indicator and the design surface is frozen at the last good state.

#### XamlSourceViewModel

```csharp
public class XamlSourceViewModel : ReactiveObject
{
    public string XamlText { get; set; }         // Two-way bound to editor
    public bool HasParseError { get; }
    public string ParseErrorMessage { get; }
    public int ErrorLine { get; }
    public int ErrorColumn { get; }
    
    public bool IsSplitViewVisible { get; set; }  // show/hide the panel
}
```

#### Editor Features
- XML/XAML syntax highlighting (Avalonia namespace-aware).
- Brace/tag matching and auto-close.
- Code folding for element subtrees.
- `Ctrl+Space` attribute completion: attributes valid for the element at cursor, sourced from `ControlDescriptorRegistry`.
- `F12` / Go-to-definition on `x:Name` values jumps to the code-behind declaration.
- Error squiggles for unknown attributes or malformed XML.

---

### 4.6 XAML Serialization Engine

#### Overview
Responsible for:
- **Parsing** an existing `.axaml` file into a `DesignDocument` (in-memory DOM).
- **Serializing** a `DesignDocument` back to a well-formatted `.axaml` string.

#### XamlParser

```csharp
public class XamlParser
{
    // Parses axaml string to DesignDocument. Throws XamlParseException on failure.
    public DesignDocument Parse(string axamlText, ControlDescriptorRegistry registry);
    
    // Attempts parse; returns false + errorInfo on failure (no exception)
    public bool TryParse(string axamlText, ControlDescriptorRegistry registry,
                         out DesignDocument? document, out XamlParseError? error);
}
```

The parser uses `System.Xml.Linq` (`XDocument.Parse`) to read the XML, then walks the element tree:
- Maps XML element names to `ControlDescriptor` entries.
- For unknown element types, creates a `DesignElement` with `IsUnknown = true` (rendered as a placeholder grey box on the canvas).
- Reads all XML attributes as `DesignProperty` entries.
- Handles attached properties (e.g. `Grid.Row="1"`) as special `AttachedDesignProperty` entries.
- Handles content properties (children, inner text) recursively.
- Reads `x:Name` as the element's `Name` field.

#### XamlSerializer

```csharp
public class XamlSerializer
{
    // Serializes full document to axaml string
    public string Serialize(DesignDocument document);
    
    // Serializes a single element subtree (used for clipboard copy)
    public string SerializeElement(DesignElement element);
}
```

Serialization rules:
- Output is valid XML, indented with 4 spaces.
- Namespace declarations are on the root element only (or the element that first uses the namespace).
- Attributes are sorted: `x:Name` first, then layout properties (Width, Height, Margin), then content properties, then style/appearance, then attached properties alphabetically.
- Default-value properties are **omitted** from output (cleaner XAML).
- Child elements appear as XML child nodes, not as attribute values.
- Attached properties use the `OwnerType.PropertyName="value"` notation.

#### Namespace Map

The serializer maintains a `XamlNamespaceMap` that knows the prefix → namespace URI mapping:

```xml
xmlns="https://github.com/avaloniaui"
xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
xmlns:vm="clr-namespace:MyApp.ViewModels"
```

---

### 4.7 Code-Behind Generation Engine

#### Overview
Generates (or updates) the `.axaml.cs` partial class file that accompanies each designed form. The generated file is **idempotent** — running generation again does not lose hand-written code inside handler methods.

#### CodeBehindGenerator

```csharp
public class CodeBehindGenerator
{
    // Generates or merges code-behind for the given document.
    // existingSource: current .axaml.cs contents, or null if new file.
    // Returns the updated .axaml.cs source string.
    public string Generate(DesignDocument document, string? existingSource,
                            CodeGenOptions options);
}

public class CodeGenOptions
{
    public string Namespace { get; set; }        // e.g. "MyApp.Views"
    public string ClassName { get; set; }        // e.g. "MainWindow"
    public string BaseClass { get; set; }        // e.g. "Window", "UserControl"
    public bool GenerateNullableAnnotations { get; set; } = true;
    public bool GenerateFieldDeclarations { get; set; } = true;
    public CodeBehindStyle Style { get; set; }   // CodeBehind | MVVM
}

public enum CodeBehindStyle { CodeBehind, MVVM }
```

#### Generated Code Structure (CodeBehind style)

```csharp
// Auto-generated by AvaloniaDesigner. Manual edits inside event handlers are preserved.
// Do not manually edit the field declarations or InitializeComponent region.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace {Namespace};

public partial class {ClassName} : {BaseClass}
{
    #region Designer-generated fields
    private Button btnOk;
    private TextBox txtName;
    // ... one field per named element
    #endregion

    public {ClassName}()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        btnOk.Click += BtnOk_Click;
        txtName.TextChanged += TxtName_TextChanged;
        // ... one line per event wiring
    }

    #region Event Handlers

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: implement
    }

    private void TxtName_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // TODO: implement
    }

    #endregion
}
```

#### Merge Strategy

When `existingSource` is not null:
1. Parse the existing file using `Microsoft.CodeAnalysis` (Roslyn) `CSharpSyntaxTree`.
2. Extract the body of each existing event handler method.
3. Regenerate the entire file scaffolding (fields, WireEvents, stubs).
4. Re-insert the body of each existing handler method into the new stubs.
5. Handler methods that no longer correspond to a wired event are moved into a `#region Orphaned Handlers` block with a comment — they are NOT deleted.

#### Template Engine

Use **Scriban** templates stored in the `Templates/` folder. Example `CodeBehind.sbntxt`:

```
// Auto-generated by AvaloniaDesigner. 
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
{{ for ns in extra_usings }}
using {{ ns }};
{{ end }}

namespace {{ namespace }};

public partial class {{ class_name }} : {{ base_class }}
{
    #region Designer-generated fields
{{ for field in fields }}
    private {{ field.type }} {{ field.name }};
{{ end }}
    #endregion

    public {{ class_name }}()
    {
        InitializeComponent();
        WireEvents();
    }
    ...
```

---

### 4.8 Undo / Redo Stack

#### Overview
Every user action on the design surface is recorded as an `IDesignCommand` object. The stack supports unlimited undo/redo within a session. The stack is cleared on file open.

#### IDesignCommand

```csharp
public interface IDesignCommand
{
    string Description { get; }    // shown in Edit > Undo "{Description}"
    void Execute(DesignDocument document);
    void Undo(DesignDocument document);
}
```

#### Concrete Commands

| Command | Description | Undo |
|---------|-------------|------|
| `AddElementCommand` | Adds a new DesignElement to parent | Remove element |
| `RemoveElementCommand` | Removes element | Re-add at same position |
| `MoveElementCommand` | Changes Left/Top or Grid.Row/Column | Restore old position |
| `ResizeElementCommand` | Changes Width/Height | Restore old size |
| `SetPropertyCommand` | Changes one property value | Restore old value |
| `ReparentCommand` | Moves element to different parent | Restore old parent |
| `SetNameCommand` | Changes x:Name | Restore old name |
| `CompositeCommand` | Wraps multiple commands as one | Undo all in reverse |

#### UndoRedoStack

```csharp
public class UndoRedoStack : ReactiveObject
{
    public bool CanUndo { get; }
    public bool CanRedo { get; }
    public string UndoDescription { get; }   // e.g. "Move Button"
    public string RedoDescription { get; }
    
    public void Execute(IDesignCommand command);   // execute + push to undo stack
    public void Undo();
    public void Redo();
    public void Clear();
}
```

The `UndoRedoStack.Execute` method:
1. Calls `command.Execute(document)`.
2. Pushes the command onto the undo stack.
3. Clears the redo stack.
4. Raises `PropertyChanged` for `CanUndo`/`CanRedo`.

---

### 4.9 Snap & Layout Guides

When moving or resizing a control:

**Snap to Grid:** If the DesignCanvas has a snap grid enabled (configurable in settings, default 8px), movement snaps to grid units.

**Smart Snap Lines:** The designer computes alignment snap lines against:
- Other element edges (left, right, top, bottom, horizontal center, vertical center).
- The parent container edges and center lines.
- Standard margin distances (8, 16 pixels shown as blue lines when snapped).

Snap lines are rendered as 1px colored lines in the AdornerLayer and disappear on pointer release.

**Snap Threshold:** An element snaps when its edge is within `snapThreshold` pixels (default 6px) of a snap target.

---

### 4.10 Selection & Adorner System

#### SelectionManager

```csharp
public class SelectionManager : ReactiveObject
{
    public IReadOnlyList<DesignElement> SelectedElements { get; }
    public DesignElement? PrimarySelection { get; }   // last clicked
    public bool HasSelection { get; }
    public bool IsMultiSelection { get; }
    
    public void SetSelection(DesignElement element);
    public void AddToSelection(DesignElement element);
    public void RemoveFromSelection(DesignElement element);
    public void SelectAll();
    public void ClearSelection();
    
    public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
}
```

#### AdornerLayer

The `AdornerLayer` is a transparent `Canvas` overlaid on the `DesignCanvas`. It draws:

- **Selection Rectangle**: A 1px dashed blue border around the selected element's bounds.
- **Resize Handles**: 8 filled squares (6×6px, white with blue border) at corners and edge midpoints.
- **Container Label**: Small grey tab at top-left showing the element type name.
- **Snap Lines**: Thin colored lines (blue = alignment, pink = spacing).
- **Rubber-Band Rectangle**: Semi-transparent blue fill on drag-select.
- **Drop Insertion Line**: A 2px blue horizontal/vertical line showing where a dragged item will be inserted in ordered containers.

Handle hit areas are 12×12px (larger than visual for easier grabbing).

---

## 5. Data Model

### DesignDocument

```csharp
public class DesignDocument : ReactiveObject
{
    public string FilePath { get; set; }           // full path to .axaml file
    public string XClass { get; set; }             // x:Class value, e.g. "MyApp.Views.MainWindow"
    public string RootElementType { get; set; }    // "Window", "UserControl", etc.
    public DesignElement RootElement { get; set; }
    public bool IsDirty { get; set; }              // unsaved changes exist
    public string? AssemblyName { get; set; }      // from project context
    public List<XamlNamespaceDeclaration> Namespaces { get; }
    
    // Flat lookup by x:Name
    public IReadOnlyDictionary<string, DesignElement> NamedElements { get; }
    
    // All elements in tree order
    public IEnumerable<DesignElement> AllElements { get; }
}
```

### DesignElement

```csharp
public class DesignElement : ReactiveObject
{
    public string ElementType { get; set; }         // XAML element name, e.g. "Button"
    public string? ClrType { get; set; }            // fully qualified CLR type, if known
    public string? Name { get; set; }               // x:Name value
    public ControlDescriptor? Descriptor { get; }   // null if unknown type
    
    // Properties defined on this element in XAML
    public ObservableCollection<DesignProperty> Properties { get; }
    
    // Attached properties (Grid.Row, etc.)
    public ObservableCollection<DesignAttachedProperty> AttachedProperties { get; }
    
    // Event handlers
    public ObservableCollection<DesignEventHandler> EventHandlers { get; }
    
    // Tree relationships
    public DesignElement? Parent { get; internal set; }
    public ObservableCollection<DesignElement> Children { get; }
    
    // Layout (convenience: read/write from Properties)
    public double Width { get; set; }
    public double Height { get; set; }
    public double Left { get; set; }   // Canvas.Left
    public double Top { get; set; }    // Canvas.Top
    public Thickness Margin { get; set; }
    
    // Designer state
    public bool IsLocked { get; set; }      // not moveable/resizable
    public bool IsVisible { get; set; }     // hidden on canvas (still in XAML)
    public bool IsSelected { get; set; }
    
    // Get/set a property by name
    public object? GetProperty(string name);
    public void SetProperty(string name, object? value);
    public void RemoveProperty(string name);   // resets to default (omit from XAML)
    
    // Add/remove children
    public void AddChild(DesignElement child, int index = -1);
    public void RemoveChild(DesignElement child);
}
```

### DesignProperty

```csharp
public class DesignProperty
{
    public string Name { get; set; }          // attribute name
    public string? StringValue { get; set; }  // raw XAML attribute string
    public object? TypedValue { get; set; }   // parsed typed value
    public bool IsAttributeForm { get; set; } // true = attr, false = element form
}
```

### DesignEventHandler

```csharp
public class DesignEventHandler
{
    public string EventName { get; set; }      // e.g. "Click"
    public string HandlerName { get; set; }    // e.g. "BtnOk_Click"
}
```

---

## 6. Control Descriptor Catalog

### ControlDescriptor

A `ControlDescriptor` describes everything the designer needs to know about a control type. It is the bridge between the abstract design document and the concrete Avalonia type.

```csharp
public class ControlDescriptor
{
    public string ElementName { get; }            // XAML element name (e.g. "Button")
    public string FullTypeName { get; }           // CLR type (e.g. "Avalonia.Controls.Button")
    public string Category { get; }               // Toolbox category
    public string Description { get; }
    public IImage Icon { get; }
    
    // Default size when dropped on canvas
    public Size DefaultSize { get; }
    
    // Minimum designable size
    public Size MinimumSize { get; }
    
    // Properties exposed in the property grid
    public IReadOnlyList<PropertyDescriptor> Properties { get; }
    
    // Events exposed in the events tab
    public IReadOnlyList<EventDescriptor> Events { get; }
    
    // Can this element have child elements?
    public bool IsContainer { get; }
    
    // Layout mode this container uses
    public ContainerLayoutMode LayoutMode { get; }   // Canvas | Stack | Grid | Dock | Wrap
    
    // Which property holds children (e.g. "Children", "Items", "Content")
    public string? ContentProperty { get; }
    
    // Factory: creates a live Avalonia control instance for preview rendering
    public Func<DesignElement, Control> ControlFactory { get; }
    
    // Factory: creates a default DesignElement for this type
    public Func<DesignElement> DefaultElementFactory { get; }
}
```

### PropertyDescriptor

```csharp
public class PropertyDescriptor
{
    public string Name { get; }                // e.g. "Content", "Width"
    public string Category { get; }            // e.g. "Layout", "Appearance"
    public string Description { get; }
    public Type PropertyType { get; }
    public object? DefaultValue { get; }
    public bool IsReadOnly { get; }
    public bool IsBrowsable { get; }           // show in property grid
    public bool IsDesignTimeOnly { get; }      // d: prefix — not in runtime XAML
    public Type? CustomEditorType { get; }     // override the default editor
    public string? AttachedPropertyOwner { get; } // e.g. "Grid" for Grid.Row
}
```

### EventDescriptor

```csharp
public class EventDescriptor
{
    public string Name { get; }                // e.g. "Click"
    public string HandlerSignatureTypeName { get; }  // e.g. "RoutedEventHandler"
    public string Description { get; }
    public string DefaultHandlerName { get; }  // suggested stub name
}
```

### ControlDescriptorRegistry

```csharp
public class ControlDescriptorRegistry
{
    public void Register(ControlDescriptor descriptor);
    public void RegisterAssembly(Assembly assembly);   // scans for [ControlDescriptor] attributes
    
    public ControlDescriptor? GetByElementName(string name);
    public ControlDescriptor? GetByType(Type type);
    public IReadOnlyList<ControlDescriptor> GetAll();
    public IReadOnlyList<ControlDescriptor> GetByCategory(string category);
}
```

---

### Standard Control Descriptors (Required Implementations)

Each of the following must have a concrete `ControlDescriptor` implementation in `AvaloniaDesigner.Controls`:

#### Containers
- `Window` — root only; properties: Title, Width, Height, WindowStartupLocation, Icon, CanResize
- `UserControl` — root only; properties: Width, Height
- `Grid` — LayoutMode=Grid; expose row/column definition editors in property grid (see §6.1)
- `StackPanel` — LayoutMode=Stack; properties: Orientation, Spacing
- `DockPanel` — LayoutMode=Dock; properties: LastChildFill
- `WrapPanel` — LayoutMode=Wrap; properties: Orientation, ItemWidth, ItemHeight
- `Canvas` — LayoutMode=Canvas; properties: Background
- `Border` — single child container; properties: Background, BorderBrush, BorderThickness, CornerRadius, Padding
- `ScrollViewer` — single child container; properties: HorizontalScrollBarVisibility, VerticalScrollBarVisibility
- `Expander` — properties: Header, IsExpanded, ExpandDirection
- `TabControl` — children are `TabItem` elements; properties: TabStripPlacement
- `TabItem` — properties: Header, IsSelected
- `SplitView` — properties: PanePlacement, IsPaneOpen, OpenPaneLength, DisplayMode

#### Common Controls
- `Button` — DefaultSize: 75×23; properties: Content, IsDefault, IsCancel, Command, CommandParameter; events: Click
- `TextBox` — DefaultSize: 120×23; properties: Text, Watermark, IsReadOnly, MaxLength, AcceptsReturn, TextWrapping; events: TextChanged, KeyDown
- `TextBlock` — DefaultSize: 120×20; properties: Text, FontSize, FontWeight, FontStyle, Foreground, TextWrapping, TextTrimming, TextAlignment
- `CheckBox` — DefaultSize: 120×20; properties: Content, IsChecked, IsThreeState; events: Checked, Unchecked, IsCheckedChanged
- `RadioButton` — DefaultSize: 120×20; properties: Content, IsChecked, GroupName; events: IsCheckedChanged
- `ComboBox` — DefaultSize: 120×23; properties: Items (collection editor), SelectedIndex, SelectedItem, IsEditable, PlaceholderText; events: SelectionChanged
- `ListBox` — DefaultSize: 120×96; properties: Items, SelectedItem, SelectionMode; events: SelectionChanged
- `Image` — DefaultSize: 100×100; properties: Source, Stretch, StretchDirection
- `ProgressBar` — DefaultSize: 200×20; properties: Value, Minimum, Maximum, IsIndeterminate, Orientation
- `Slider` — DefaultSize: 200×20; properties: Value, Minimum, Maximum, TickFrequency, TickPlacement, Orientation; events: ValueChanged
- `Label` — DefaultSize: 120×20; properties: Content, Target (for focus)
- `ToggleButton` — DefaultSize: 75×23; properties: Content, IsChecked; events: Checked, Unchecked
- `ToggleSwitch` — DefaultSize: 120×30; properties: IsChecked, OnContent, OffContent; events: IsCheckedChanged

#### Input Controls
- `NumericUpDown` — DefaultSize: 120×23; properties: Value, Minimum, Maximum, Increment, FormatString, DecimalPlaces; events: ValueChanged
- `DatePicker` — DefaultSize: 200×30; properties: SelectedDate, DayVisible, MonthVisible, YearVisible; events: SelectedDateChanged
- `TimePicker` — DefaultSize: 200×30; properties: SelectedTime, ClockIdentifier; events: SelectedTimeChanged
- `AutoCompleteBox` — DefaultSize: 200×23; properties: Text, ItemsSource, FilterMode, Watermark; events: TextChanged, SelectionChanged
- `CalendarDatePicker` — DefaultSize: 200×30; properties: SelectedDate, Watermark; events: SelectedDateChanged

#### Data Controls
- `DataGrid` — DefaultSize: 300×200; column collection editor in property grid; properties: ItemsSource, AutoGenerateColumns, CanUserSortColumns, GridLinesVisibility, SelectionMode; events: SelectionChanged, CellEditEnding
- `TreeView` — DefaultSize: 200×300; properties: ItemsSource, SelectionMode; events: SelectionChanged
- `ItemsControl` — DefaultSize: 200×200; properties: ItemsSource, ItemTemplate

#### Menu & Navigation
- `Menu` — properties: Items (menu item collection)
- `MenuItem` — properties: Header, Icon, InputGesture, Command, Items (sub-menu)
- `ContextMenu` — properties: Items
- `ToolTip` — attached property on any control; properties: Content, Placement, ShowDelay
- `Separator` — no significant properties

#### Layout Helpers
- `GridSplitter` — properties: ResizeDirection, ShowsPreview
- `Viewbox` — single child; properties: Stretch, StretchDirection
- `UniformGrid` — properties: Rows, Columns

#### Shapes (under `Avalonia.Controls.Shapes`)
- `Rectangle` — properties: Fill, Stroke, StrokeThickness, RadiusX, RadiusY
- `Ellipse` — properties: Fill, Stroke, StrokeThickness
- `Line` — properties: X1, Y1, X2, Y2, Stroke, StrokeThickness
- `Path` — properties: Data (path geometry string), Fill, Stroke, StrokeThickness
- `Polygon` — properties: Points, Fill, Stroke, StrokeThickness
- `Polyline` — properties: Points, Stroke, StrokeThickness

---

#### 6.1 Grid Row/Column Definition Editor

When a `Grid` element is selected, the property grid must include a special **Grid Definition Editor** section:

- A visual mini-grid thumbnail showing current rows/columns.
- "Add Row" and "Add Column" buttons.
- A table listing each row definition (Height: *, Auto, or pixel value) with a delete button.
- A table listing each column definition (Width: *, Auto, or pixel value) with a delete button.
- Clicking on a child element inside the grid exposes `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, `Grid.ColumnSpan` as attached properties in the property grid.

---

## 7. Property System

### Value Conversion

The property system must be able to convert between:
- The **string form** used in XAML attributes (`"Red"`, `"8,8,8,8"`, `"True"`, `"0.5*"`)
- The **typed runtime value** used in property editors and in the live preview control.

Implement a `XamlValueConverter` class that handles all standard Avalonia type conversions:

```csharp
public static class XamlValueConverter
{
    public static string Serialize(object? value, Type targetType);
    public static object? Deserialize(string text, Type targetType);
}
```

Supported conversions:

| Type | Example XAML String |
|------|---------------------|
| `Color` | `"#FF0000"`, `"Red"` |
| `SolidColorBrush` | `"Red"`, `"#80FF0000"` |
| `Thickness` | `"8"`, `"8,4"`, `"8,4,8,4"` |
| `CornerRadius` | `"4"`, `"4,8,4,8"` |
| `GridLength` | `"*"`, `"2*"`, `"Auto"`, `"100"` |
| `FontFamily` | `"Arial"`, `"avares://..."` |
| `HorizontalAlignment` | `"Left"`, `"Center"`, `"Right"`, `"Stretch"` |
| `VerticalAlignment` | `"Top"`, `"Center"`, `"Bottom"`, `"Stretch"` |
| `Orientation` | `"Horizontal"`, `"Vertical"` |
| `bool` | `"True"`, `"False"` |
| `double` | `"1.5"`, `"NaN"`, `"Infinity"` |
| `int` | `"42"` |
| `string` | passthrough |
| Enums | enum member name |
| `Point` | `"10,20"` |
| `Size` | `"100,50"` |
| `Rect` | `"0,0,100,50"` |

---

## 8. Event Wiring System

### XAML Side

When the user assigns a handler name to an event in the Events tab, the serializer emits the event as an XAML attribute:

```xml
<Button x:Name="btnOk" Content="OK" Click="BtnOk_Click" />
```

### Code-Behind Side

The code generator reads all `DesignEventHandler` entries from all `DesignElement` nodes and generates:

1. **Field declaration** (if element has an `x:Name`):
   ```csharp
   private Button btnOk;
   ```

2. **Wire-up in WireEvents()**:
   ```csharp
   btnOk.Click += BtnOk_Click;
   ```

3. **Handler stub** (only if not already present in existing source):
   ```csharp
   private void BtnOk_Click(object? sender, RoutedEventArgs e)
   {
       // TODO: implement
   }
   ```

### Handler Naming Conventions

Auto-suggested handler name = `{ElementName}_{EventName}` (e.g., `btnOk_Click`).
If element has no `x:Name`, use `{TypeName}{AutoIndex}_{EventName}` (e.g., `Button1_Click`).

The user can override the suggested name in the Events tab. The property grid shows an error icon if the name is not a valid C# identifier.

---

## 9. MVVM Code Generation

When the user chooses **MVVM mode** (via File > New > MVVM Window or a per-file setting), the code generator additionally produces a `ViewModel` skeleton.

### Generated ViewModel

```csharp
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace {Namespace}.ViewModels;

public partial class {ClassName}ViewModel : ObservableObject
{
    // Auto-generated from TextBox controls with two-way Binding
    [ObservableProperty]
    private string _txtName = string.Empty;

    // Auto-generated from Button controls with Command binding
    [RelayCommand]
    private void BtnOk()
    {
        // TODO: implement
    }
}
```

### Generated XAML (MVVM style)

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:{Namespace}.ViewModels"
        x:Class="{Namespace}.{ClassName}"
        x:DataType="vm:{ClassName}ViewModel">
    <Window.DataContext>
        <vm:{ClassName}ViewModel />
    </Window.DataContext>
    <Grid>
        <TextBox Text="{Binding TxtName}" />
        <Button Content="OK" Command="{Binding BtnOkCommand}" />
    </Grid>
</Window>
```

### MVVM Binding Detection Rules

The code generator uses heuristics from the `ControlDescriptor`:
- `TextBox.Text` → `{Binding {Name}Text, Mode=TwoWay}`
- `Button.Command` → `{Binding {Name}Command}`
- `CheckBox.IsChecked` → `{Binding Is{Name}Checked}`
- `ComboBox.SelectedItem` → `{Binding Selected{Name}Item}`
- `Slider.Value` → `{Binding {Name}Value}`
- `ProgressBar.Value` → `{Binding {Name}Value}`

These are applied when MVVM mode is active.

---

## 10. File & Project Integration

### ProjectContext

```csharp
public class ProjectContext
{
    public string? ProjectFilePath { get; }      // path to .csproj, or null if standalone
    public string? RootNamespace { get; }
    public string? AssemblyName { get; }
    public string? TargetFramework { get; }
    public List<string> AxamlFiles { get; }      // all .axaml files in project
    public List<string> CsFiles { get; }         // all .cs files in project
    
    public static Task<ProjectContext> LoadFromCsprojAsync(string csprojPath);
    public static ProjectContext Standalone();
}
```

### File Operations

| Operation | Behavior |
|-----------|----------|
| **File > New > Window** | Creates `NewWindow.axaml` + `NewWindow.axaml.cs` with boilerplate |
| **File > New > UserControl** | Creates `NewControl.axaml` + `NewControl.axaml.cs` |
| **File > Open** | Opens a `.axaml` file; auto-detects sibling `.axaml.cs` |
| **File > Open Project** | Opens a `.csproj`; populates file navigator |
| **File > Save** | Writes `.axaml` from `XamlSerializer`, merges into existing `.axaml.cs` via `CodeBehindGenerator` |
| **File > Save As** | Prompts for new path, saves both files |
| **File > Close** | Prompts for save if dirty |

### Auto-Detection of x:Class Namespace

When opening a `.csproj` project, the designer reads the `<RootNamespace>` element and the relative path of the `.axaml` file to construct the default `x:Class` value:

```
RootNamespace = "MyApp"
file path = Views/MainWindow.axaml
→ x:Class = "MyApp.Views.MainWindow"
```

### File Watcher

If an open `.axaml` file is changed on disk by an external editor (e.g., VS Code), the designer shows a notification bar:

> *"MainWindow.axaml has been modified externally. [Reload] [Ignore]*"

---

## 11. Main Window Layout

### Docking Layout

The main window uses a docking layout manager (`Dock` panels or a third-party docking library compatible with Avalonia such as `Dock.Avalonia`):

```
┌─────────────────────────────────────────────────────────────┐
│ MenuBar: File  Edit  View  Format  Build  Tools  Help        │
├─────────────────────────────────────────────────────────────┤
│ Toolbar: New | Open | Save | Undo | Redo | Zoom% | Snap [x] │
├──────────┬────────────────────────────┬──────────────────────┤
│ Toolbox  │                            │   Property Grid      │
│ (left)   │   Design Surface           │   (right, 280px)     │
│  200px   │   (center, fills)          │                      │
│          │                            │   ┌────────────────┐ │
│          │                            │   │ Props | Events │ │
│          │                            │   └────────────────┘ │
│          │                            ├──────────────────────┤
│          │                            │ Document Outline      │
│          │                            │ (right-bottom, 280px) │
├──────────┴────────────────────────────┴──────────────────────┤
│ XAML Source View (bottom, collapsible, 30% height)           │
├─────────────────────────────────────────────────────────────┤
│ Status Bar: [element count]  [zoom]  [cursor pos]  [errors]  │
└─────────────────────────────────────────────────────────────┘
```

### Panel Visibility
All panels can be shown/hidden via the **View** menu and dragged to float or dock to alternate sides. Panel layout is persisted to `settings.json`.

### Toolbar

| Button | Action |
|--------|--------|
| New Window | File > New > Window |
| New UserControl | File > New > UserControl |
| Open | File > Open |
| Save | File > Save |
| Undo | Ctrl+Z |
| Redo | Ctrl+Y |
| Zoom selector | 25%, 50%, 75%, 100%, 150%, 200%, Fit |
| Snap to Grid toggle | Enable/disable grid snapping |
| Show Grid toggle | Show/hide the background grid |
| Alignment tools | Align Left, Center, Right, Top, Middle, Bottom (enabled when ≥2 elements selected) |
| Distribution tools | Distribute Horizontally / Vertically |

---

## 12. Keyboard Shortcuts & Context Menus

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Z` | Undo |
| `Ctrl+Y` / `Ctrl+Shift+Z` | Redo |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+N` | New Window |
| `Ctrl+O` | Open File |
| `Ctrl+A` | Select All |
| `Delete` / `Backspace` | Delete selected element(s) |
| `Ctrl+C` | Copy selection |
| `Ctrl+X` | Cut selection |
| `Ctrl+V` | Paste |
| `Ctrl+D` | Duplicate selection |
| `Ctrl+G` | Group selected elements in a Grid |
| Arrow keys | Nudge selected element(s) by 1px |
| `Shift+Arrow` | Nudge by 8px |
| `F2` | Rename (set x:Name) |
| `Escape` | Deselect all / cancel drag |
| `Tab` | Cycle through elements in tree order |
| `Ctrl+]` | Bring to front |
| `Ctrl+[` | Send to back |
| `Ctrl+=` | Zoom in |
| `Ctrl+-` | Zoom out |
| `Ctrl+0` | Zoom to 100% |
| `Ctrl+Shift+0` | Zoom to fit |

### Canvas Context Menu (right-click on canvas)

- Cut / Copy / Paste / Delete
- ─────
- Bring to Front / Send to Back / Bring Forward / Send Backward
- ─────
- Wrap In → Grid / StackPanel / Border / ScrollViewer
- Unwrap (remove container, promote children)
- ─────
- Set Tab Index…
- Set x:Name…
- ─────
- View XAML for Selection
- ─────
- Properties (brings focus to property grid)

---

## 13. Extensibility / Plugin Model

### Custom Control Descriptors

Third-party control packages can provide their own `ControlDescriptor` implementations. The designer supports loading plugins from:

1. **Built-in**: `AvaloniaDesigner.Controls` assembly (standard Avalonia controls).
2. **Plugin assemblies**: `.dll` files placed in `%AppData%/AvaloniaDesigner/Plugins/` (or platform equivalent). Each plugin assembly is scanned for types implementing `IControlDescriptorPlugin`.

```csharp
public interface IControlDescriptorPlugin
{
    string PluginName { get; }
    string PluginVersion { get; }
    IEnumerable<ControlDescriptor> GetDescriptors();
    IEnumerable<ToolboxCategory> GetToolboxCategories();
}
```

### Custom Property Editors

Plugins can register custom property editor controls for specific types:

```csharp
public interface IPropertyEditorPlugin
{
    Type PropertyType { get; }               // type this editor handles
    Type EditorControlType { get; }          // must be a Control subclass
}
```

The `EditorControlType` must expose a `Value` `StyledProperty<object?>` that the property grid binds.

---

## 14. Settings & Preferences

Persisted to `%AppData%/AvaloniaDesigner/settings.json` (or platform equivalent):

```json
{
  "ui": {
    "theme": "FluentLight",
    "panelLayout": { /* serialized dock layout */ },
    "toolboxExpanded": ["Common Controls", "Containers"],
    "recentFiles": ["C:\\Projects\\MyApp\\Views\\MainWindow.axaml"]
  },
  "design": {
    "snapToGrid": true,
    "gridSize": 8,
    "snapThreshold": 6,
    "showGrid": true,
    "defaultZoom": 1.0
  },
  "codeGen": {
    "defaultStyle": "MVVM",
    "mvvmToolkit": "CommunityToolkit",
    "generateNullableAnnotations": true,
    "defaultNamespace": ""
  }
}
```

Settings are exposed via **Tools > Preferences** dialog.

---

## 15. Non-Functional Requirements

| Requirement | Target |
|------------|--------|
| **Platform** | Windows 10+, macOS 13+, Ubuntu 22.04+ (wherever .NET 10 + Avalonia runs) |
| **Performance** | Design surface re-render < 16ms for documents with ≤ 200 elements |
| **Startup time** | < 3 seconds cold start |
| **XAML parse round-trip** | Parse + re-serialize a 500-element document < 200ms |
| **Memory** | < 300 MB resident for a document with 200 elements |
| **Accessibility** | All toolbar and menu actions accessible via keyboard only |
| **Theme** | Supports Fluent Light and Fluent Dark; follows system theme by default |
| **Error resilience** | An unparseable XAML file must not crash the app; show error and allow editing |
| **Crash recovery** | Auto-save a crash recovery copy every 60 seconds to `%Temp%/AvaloniaDesigner/` |

---

## 16. Implementation Phases & Milestones

### Phase 1 — Core Foundation (MVP)
**Deliverable**: Can open a blank canvas, drag a Button/TextBox/Label onto it, move/resize it, see updated XAML, and save `.axaml` + `.axaml.cs`.

- [ ] Project & solution scaffolding (.NET 10, Avalonia 11, ReactiveUI)
- [ ] `DesignDocument` / `DesignElement` data model
- [ ] `XamlSerializer` (write only)
- [ ] `DesignCanvas` (Canvas layout only)
- [ ] `AdornerLayer` with selection handles and move/resize
- [ ] `ToolboxView` with 5 controls: Button, TextBox, Label, CheckBox, Grid
- [ ] Basic `PropertyGridView` (string and numeric editors only)
- [ ] `UndoRedoStack` with Add/Move/Resize/SetProperty commands
- [ ] File Save / Open
- [ ] `CodeBehindGenerator` basic (no merge)

### Phase 2 — Full Control Library + Property Editors
- [ ] All controls from §6 implemented as `ControlDescriptor`
- [ ] All property editors from §4.3 implemented
- [ ] Snap lines and grid snapping
- [ ] `XamlParser` (read existing files)
- [ ] Source View with AvaloniaEdit, two-way sync
- [ ] `DocumentOutlineView` with drag-to-reparent

### Phase 3 — Events, Code-Behind Merge, Project Support
- [ ] Events tab in property grid
- [ ] `CodeBehindGenerator` with Roslyn merge
- [ ] `ProjectContext` — open `.csproj`, navigate files
- [ ] x:Class / namespace auto-detection
- [ ] File watcher for external edits
- [ ] New Window / New UserControl wizards

### Phase 4 — MVVM + Polish + Plugins
- [ ] MVVM code generation mode
- [ ] Plugin loading from filesystem
- [ ] Keyboard shortcut handling (complete set)
- [ ] Alignment and distribution tools
- [ ] Wrap In / Unwrap commands
- [ ] Settings dialog
- [ ] Theme support (light/dark)
- [ ] Crash recovery auto-save
- [ ] Grid Row/Column definition editor

### Phase 5 — Quality & Testing
- [ ] Unit tests for `XamlSerializer` round-trips
- [ ] Unit tests for `CodeBehindGenerator`
- [ ] Unit tests for `UndoRedoStack`
- [ ] Integration tests for drag-drop simulation
- [ ] Performance profiling and optimization
- [ ] Accessibility audit

---

## 17. Key API Contracts

### MainWindowViewModel

```csharp
public class MainWindowViewModel : ReactiveObject
{
    // Sub-ViewModels
    public ToolboxViewModel Toolbox { get; }
    public PropertyGridViewModel PropertyGrid { get; }
    public DocumentOutlineViewModel DocumentOutline { get; }
    public XamlSourceViewModel XamlSource { get; }
    
    // Current state
    public DesignDocument? CurrentDocument { get; private set; }
    public SelectionManager SelectionManager { get; }
    public UndoRedoStack UndoRedo { get; }
    public ProjectContext? ProjectContext { get; private set; }
    
    // File commands
    public ReactiveCommand<Unit, Unit> NewWindowCommand { get; }
    public ReactiveCommand<Unit, Unit> NewUserControlCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenProjectCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    
    // Edit commands
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> CutCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> DuplicateCommand { get; }
    
    // Canvas action handler (called by DesignCanvas)
    public void HandleDesignAction(DesignActionEventArgs args);
}
```

### DI Registration (Program.cs)

```csharp
var services = new ServiceCollection();

// Engine
services.AddSingleton<ControlDescriptorRegistry>();
services.AddSingleton<XamlParser>();
services.AddSingleton<XamlSerializer>();
services.AddSingleton<CodeBehindGenerator>();
services.AddSingleton<ViewModelGenerator>();

// Shell
services.AddTransient<MainWindowViewModel>();
services.AddTransient<ToolboxViewModel>();
services.AddTransient<PropertyGridViewModel>();
services.AddTransient<DocumentOutlineViewModel>();
services.AddTransient<XamlSourceViewModel>();

// Plugin loading
services.AddSingleton<IPluginLoader, FileSystemPluginLoader>();

var provider = services.BuildServiceProvider();
```

---

## Appendix A — Generated File Samples

### Sample: Generated `MainWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        x:Class="MyApp.Views.MainWindow"
        Title="MainWindow"
        Width="800"
        Height="450">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBlock x:Name="lblTitle"
                   Grid.Row="0"
                   Text="Welcome"
                   FontSize="24"
                   HorizontalAlignment="Center"
                   Margin="0,16,0,8" />

        <TextBox x:Name="txtName"
                 Grid.Row="1"
                 Watermark="Enter your name"
                 Margin="16"
                 VerticalAlignment="Center"
                 TextChanged="TxtName_TextChanged" />

        <Button x:Name="btnOk"
                Grid.Row="2"
                Content="OK"
                Width="75"
                HorizontalAlignment="Right"
                Margin="16"
                Click="BtnOk_Click" />
    </Grid>
</Window>
```

### Sample: Generated `MainWindow.axaml.cs`

```csharp
// Auto-generated by AvaloniaDesigner v1.0
// Do not edit the Designer-generated regions manually.
// Event handler bodies are preserved across regenerations.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyApp.Views;

public partial class MainWindow : Window
{
    #region Designer-generated fields
    private TextBlock lblTitle;
    private TextBox txtName;
    private Button btnOk;
    #endregion

    public MainWindow()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        txtName.TextChanged += TxtName_TextChanged;
        btnOk.Click += BtnOk_Click;
    }

    #region Event Handlers

    private void TxtName_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // TODO: implement
    }

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: implement
    }

    #endregion
}
```

### Sample: Generated MVVM `MainWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:MyApp.ViewModels"
        x:Class="MyApp.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="MainWindow"
        Width="800"
        Height="450">
    <Window.DataContext>
        <vm:MainWindowViewModel />
    </Window.DataContext>
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0"
                   Text="Welcome"
                   FontSize="24"
                   HorizontalAlignment="Center"
                   Margin="0,16,0,8" />

        <TextBox Grid.Row="1"
                 Text="{Binding TxtName, Mode=TwoWay}"
                 Watermark="Enter your name"
                 Margin="16"
                 VerticalAlignment="Center" />

        <Button Grid.Row="2"
                Content="OK"
                Width="75"
                HorizontalAlignment="Right"
                Margin="16"
                Command="{Binding BtnOkCommand}" />
    </Grid>
</Window>
```

### Sample: Generated MVVM `MainWindowViewModel.cs`

```csharp
// Auto-generated by AvaloniaDesigner v1.0
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyApp.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _txtName = string.Empty;

    [RelayCommand]
    private void BtnOk()
    {
        // TODO: implement
    }
}
```

---

*End of AvaloniaDesigner Technical Specification v1.0*
