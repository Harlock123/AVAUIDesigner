# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers the designer's features, shortcuts,
plugins and supported controls; this file covers how the code is put together and the rules that keep it working.

## What this is

A visual drag-and-drop designer for Avalonia `.axaml` files, in C# on .NET 10 and Avalonia 11.3. It edits a design
document on a live surface, keeps a XAML source view in sync, and generates code-behind and MVVM view models. It
runs on Windows, macOS and Linux. `AvaloniaDesigner_Spec.md` is the original specification; the README's "Design
notes" list where the code deliberately departs from it.

## Commands

```bash
dotnet build AVAUIDesigner.sln                    # no global.json: any .NET 10 SDK
dotnet test AVAUIDesigner.sln                     # Engine and Shell tests; the Shell tests render headlessly
dotnet run --project src/AVAUIDesigner.App        # start the designer
dotnet run --project src/AVAUIDesigner.App -- --mcp   # the MCP server on stdio, no window (--attach: the open window;
                                                      # --http [ADDRESS:]PORT: over the network, with a token)
./scripts/update-screenshots.sh                   # re-render docs/screenshots/*.png (sets AVAUIDESIGNER_SCREENSHOTS)
AVAUIDESIGNER_BUILD_TEST=1 dotnet test tests/AVAUIDesigner.Shell.Tests --filter GeneratedProjectBuildTests
```

The last command is opt-in and slow: it saves generated output into two real Avalonia projects and runs
`dotnet build` on them. `Directory.Build.props` sets the target framework, nullable reference types and implicit
usings for every project. Package versions are pinned centrally in `Directory.Packages.props`, so
`<PackageReference>` items carry no `Version`.

## Layout

- `src/AVAUIDesigner.Engine/`: the UI-independent core (it uses only Avalonia's basic types).
  - `Document/`: the model, `DesignDocument`, `DesignElement` and `DesignProperty`.
  - `Serialization/`: `XamlParser` and `XamlSerializer`, with `XamlNamespaceMap`.
  - `Properties/XamlValueConverter.cs` turns XAML strings into values and back.
  - `UndoRedo/`: `UndoRedoStack` and one `IDesignCommand` per kind of change under `Commands/`.
  - `CodeGeneration/`: code-behind and view-model generators. Scriban templates live in `Templates/*.sbntxt`
    (embedded resources); the merge into existing code uses Roslyn.
  - `Descriptors/`: `ControlDescriptor`, `PropertyDescriptor`, `EventDescriptor` and the registry.
  - `Selection/`, `Project/`, `Settings/DesignerSettings.cs` and `Plugins/` (the plugin contracts and loader).
- `src/AVAUIDesigner.Controls/`: one `*Descriptor.cs` per standard control under `Descriptors/`, with shared
  property, event and attached-property sets in `Common/`.
- `src/AVAUIDesigner.Shell/`: the Avalonia UI.
  - `MainWindow.axaml` and `ViewModels/` (`MainWindowViewModel` is split, with its commands in a partial file).
  - `Controls/DesignCanvas.cs` renders the surface and handles hit testing, adorners, snapping and drag and drop.
  - `Controls/PropertyEditors/` holds the code-built property editors.
  - `Design/PlacementService.cs` turns a drop point into attached properties or an insertion index.
  - `Views/` (toolbox, property grid, outline, AvaloniaEdit source view), `Dialogs/` and `Services/`.
- `src/AVAUIDesigner.Data/`: data structures for screens, with no UI.
  - `EntityModel.cs`: `EntityModel` and `FieldModel` (what every source is read into), `Words`, `Identifiers`
    and `TypeMap` (SQL types to C#).
  - `SourceReaders.cs`: entities from C# (Roslyn: classes, records, data-annotation attributes, enums) and from
    SQL scripts' `CREATE TABLE`.
  - `Databases.cs`: `SqlDialect` (quoting, parameters, how each engine returns a new key) and `SchemaReader`,
    which reads a database's catalogue only, never rows.
  - `Forms.cs`, `Crud.cs`, `Assessments.cs`: the XAML and C# generators. `CodeMerge.cs` adds members to an
    existing class.
- `src/AVAUIDesigner.Mcp/`: the MCP server (ModelContextProtocol.Core, and .AspNetCore for the network door).
  - `DesignTools*.cs`: the tools, as `[McpServerTool]` methods.
  - `DesignServer.cs`: the instructions every agent is told, the argument checks, and the recording of each
    session's changes.
  - `Recipes.cs`: the saved recipes.
  - `Hosting.cs`:
    - Handlers touch the design only through `IDesignHost.RunAsync` and an `IDesignSession`.
    - `DesignHost` runs each call on the UI thread, waiting while a dialog is open.
    - `HeadlessSession` is the session for `--mcp`.
    - `AgentEditCommand` makes each change one undo step.
  - `Elements.cs`: finding elements by name or path, describing, naming and inserting them.
  - `ConnectionStore.cs`: named database connections.
  - `LiveListener`, `LiveBridge`, `NetworkListener`, `McpCommandLine` and `Workspace` are the same files as AVA
    Modeler's, AVASvgMaker's and the others', apart from the names: fix them together.
- `src/AVAUIDesigner.Chat/`: the AI Chat's conversation and clients (Claude through the Anthropic SDK, or any
  OpenAI-compatible server), keys, usage, saved chats and the in-process bridge to the server. It is a copy of
  AVA Modeler's `AVAModeler.Chat` (by way of AVASvgMaker), so a fix there may be wanted here.
- `src/AVAUIDesigner.Shell/Agents/`:
  - `ChatPanel.cs`: the panel. Its brushes follow the theme.
  - `LiveAgents.cs`: `WindowSession` (the open design as an agent sees it) and the doors.
  - `DesignLibrary.cs`: drawing a design as a picture, in a hidden stage panel.
  - `AgentSettings.cs`: `agents.json` beside `settings.json`.
  - `MainWindow.Agents.cs` holds the AI menu, the chat dock, Connect an Agent and Data Connections.
  - `MainWindowViewModel.Agents.cs` holds `PendingFiles`: code an agent made, written on the next save, by the
    agent or with Ctrl+S.
- `src/AVAUIDesigner.App/`: `Program.cs` sets up dependency injection and starts the app. `AgentMain.cs` runs
  `--mcp` on Avalonia's headless platform with Skia.
- `tests/AVAUIDesigner.Engine.Tests/`: xUnit 2 tests of parsing, round-tripping, undo/redo and generation.
- `tests/AVAUIDesigner.Shell.Tests/`: xUnit 2 with `Avalonia.Headless.XUnit` and real Skia rendering; it drives the
  main window with simulated mouse and keyboard input. `ReadmeScreenshots.cs` makes the README images.

## Rules that keep it working

- **XAML strings are the source of truth.** Property values are stored as the XAML text, so round-tripping is
  lossless. Keep unknown values intact through the parser and serializer.
- **Every edit is an `IDesignCommand`.** Changes to the document go through the undo/redo stack, never straight
  into the model, or undo breaks.
- **Engine stays UI-free.** Put document logic in the Engine, where the Engine tests reach it without a window.
- **New controls register themselves.** A class in `AVAUIDesigner.Controls` marked `[ControlDescriptor]` is
  picked up by `ControlDescriptorRegistry.RegisterAssembly`; there is no list to edit.
- **Code-behind merges never lose user code.** Handler bodies the user wrote are kept, and unused handlers move to
  an "Orphaned Handlers" region. Keep tests for this green in `CodeBehindGeneratorTests`.
- **Settings and files.** Settings live in `settings.json` under the app-data folder (`AVAUIDesigner`), and
  plugins load from its `Plugins` subfolder. Crash-recovery snapshots go to the temp folder (`AVAUIDesigner`).
  Tests pass their own paths in a temp folder and never touch the user's settings.
- **Agent tools.** Every change an agent makes goes through `DesignTools.Edit`:
  - The work is done on a copy of the design, which replaces it as one `AgentEditCommand`, so a call that
    throws changes nothing.
  - A batch is several such steps folded into one with `UndoRedoStack.Squash`.
  - A new tool needs a `[Description]` on it and on every parameter (snake_case names), and a place in
    `AgentTests.ToolNames`.
  - Every recipe is run call by call by `AgentTests`, and built for real by the opt-in
    `GeneratedProjectBuildTests.AgentScreens_ProjectBuilds`.
  - `read_file`, generated files and every other path stay inside the working folder (`Workspace`).
  - Saving doesn't write over a file this session didn't write unless `overwrite` is given (Ctrl+S asks).
- **Generated code builds.** CRUD, form and assessment code must compile in an app with compiled bindings on.
  Their method names (`Load`, `Save`, …) match what the designer's view-model merge looks for, so it adds
  nothing twice. After changing a generator, run the opt-in build test. If the SQL changes, run it against
  the engines (see the README's Screens from data).
- **No real model, keys, database server or user settings in tests.** `SettingsIsolation` points
  `AgentSettings.FolderOverride` at a temporary folder before any test. Chat tests answer from
  `FakeModelServer`, the database is a SQLite file, and test windows get key stores, connection stores and pipes
  of their own.
- **Agents never read rows.** `SchemaReader` runs catalogue queries only. Keep it that way: the README promises
  it, and clinics rely on it.

## Making a change

- Add or update tests with every change. `dotnet test` must pass.
- If the change shows in the UI, run `./scripts/update-screenshots.sh` and look at the images.
- If generated code changes, run the opt-in generated-project build test above.
- Update `README.md` (features, shortcuts, supported controls) to match.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request on Ubuntu. It then publishes
self-contained single-file builds of `AVAUIDesigner.App` for win-x64/x86/arm64, linux-x64/arm64/arm and
osx-x64/arm64, kept as workflow artifacts. Pushes to `main` replace the assets of the `latest` pre-release, and a
`v*` tag makes a proper release.
