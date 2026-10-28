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
- `src/AVAUIDesigner.App/`: `Program.cs` sets up dependency injection and starts the app.
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
