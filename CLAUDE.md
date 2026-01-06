# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Todowork is a lightweight Windows WPF desktop application for managing todo items with a transparent, always-on-top overlay window. Built with .NET Framework 4.8 and WPF, it features system tray integration, single-instance enforcement, and optional auto-start on boot.

## Build and Development

### Building the Project

```bash
# Open solution in Visual Studio
# Recommended: Visual Studio 2019/2022

# Restore NuGet packages
nuget restore Todowork.sln

# Build for x86 Release (recommended for distribution)
msbuild Todowork.sln /p:Configuration=Release /p:Platform=x86

# Build for Debug
msbuild Todowork.sln /p:Configuration=Debug /p:Platform=AnyCPU
```

Output locations:
- Debug: `Todowork/bin/Debug/Todowork.exe`
- Release x86: `Todowork/bin/x86/Release/Todowork.exe`

### Release Process

Releases are automated via GitHub Actions. Push a tag starting with `v` (e.g., `v1.0.0`) to trigger a build and release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Architecture

### Application Lifecycle

The application uses a unique architecture centered around `App.xaml.cs` which manages three windows and enforces single-instance behavior:

1. **Single Instance Control**: Uses a named Mutex (`InstanceMutexName`) to ensure only one instance runs. If a second instance is launched, it signals the first instance via `EventWaitHandle` to show the main window, then exits.

2. **Window Management**:
   - `MainWindow`: Hidden by default, shown via tray menu or second instance launch
   - `OverlayWindow`: Transparent, topmost, mouse-passthrough overlay showing pinned todos
   - `SettingsWindow`: Modal dialog for overlay customization

3. **Data Flow**: `App.xaml.cs` acts as the central coordinator, managing window state and synchronizing settings between `MainViewModel` and `OverlayWindow`.

### Data Persistence

Two JSON files stored in `%AppData%\Todowork\`:

- `todo.json`: Todo items (managed by `TodoRepository` and `TodoStore`)
- `ui.json`: UI state including overlay position, visibility, opacity, colors, font size (managed by `App.xaml.cs`)

Both use debounced auto-save (400ms for todos, immediate for UI changes).

### Key Components

**Services Layer** (`Services/`):
- `TodoStore`: Observable collection wrapper with auto-save debouncing
- `TodoRepository`: JSON serialization for todo items
- `TrayService`: System tray icon and menu management
- `AutoStartService`: Windows registry integration for startup

**ViewModels** (`ViewModels/`):
- `MainViewModel`: Main window logic, manages two filtered views (active/completed) from single `TodoStore`
- `OverlayViewModel`: Filtered view of pinned items only
- `BaseNotify`: INotifyPropertyChanged base class
- `RelayCommand`: ICommand implementation

**Models** (`Models/`):
- `TodoItem`: Data model with `IsPinned`, `IsCompleted`, `Text`, timestamps

### UI Architecture

**Three-Window System**:

1. **MainWindow** (`MainWindow.xaml`):
   - Custom chrome with WindowChrome (no standard title bar)
   - Two ListView sections: active todos and completed todos (collapsible)
   - Inline editing on click, context menus for acns
   - Virtualized scrolling for performance

2. **OverlayWindow** (`OverlayWindow.xaml`):
   - `WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`
   - Mouse-passthrough achieved via Win32 interop (see `OverlayWindow.xaml.cs`)
   - Height auto-adjusts based on pinned item count (max 90% of screen)
   - Position controlled by ratio-based sliders (0-1 range mapped to screen coordinates)

3. **SettingsWindow** (`SettingsWindow.xaml`):
   - Modal dialog with sliders for overlay customization
   - Color picker using visual button grid (6 preset colors)
   - Changes apply immediateia `MainViewModel` property setters

**Styling**: Centralized in `Theme.xaml` with consistent color palette, typography, and reusable button styles.

### Important Implementation Details

**Overlay Positioning**: Uses ratio-based positioning (0.0 to 1.0) rather than absolute coordinates to handle different screen resolutions. `App.xaml.cs` converts ratios to actual screen coordinates using `SystemParameters.WorkArea`.

**View Filtering**: `MainViewModel` creates two `CollectionViewSource` instances from the same `TodoStore.Items` collection, using different filter predicates. This avoids data duplication while presenting separate active/completed lists.

**Property Change Propagation**: When overlay settings change in `MainViewModel`, property setters call methods on `App.xaml.cs` (cast from `Application.Current`), which then update `OverlayWindow` properties. This indirect communication pattern avoids tight coupling between ViewModels and Windows.

**Debounced Refresh**: `MainViewModel` uses a `DispatcherTimer` to batch view refreshes when multiple items change rapidly (e.g., during bulk operations), preventing UI stuttering.

## Common Modifications

### Adding New Overlay Settings

1. Add property to `MainViewModel` w setter that calls `App.SetOverlxx()`
2. Add corresponding property to `OverlayWindow.xaml.cs`
3. Add `SetOverlayXxx()` method in `App.xaml.cs` to bridge ViewModel → Window
4. Add UI control in `SettingsWindow.xaml` bound to ViewModel property
5. Add serialization fields to `UiState` class in `App.xaml.cs`
6. Update `LoadUiState()` and `SaveUiState()` methods

### Modifying Main Window UI

The main window uses two separate ListViews with different ItemTemplates for active vs completed todos. Both share the same DataContext (`MainViewModel`) but bind to different views (`ItemsView` vs `CompletedItemsView`).

### Modifying Overlay Window UI

The overlay binds to `OverlayViewModel.PinnedView` which filts for `IsPinned && !IsCompleted`. Visual properties (opacity, color, font size) are dependency properties on `OverlayWindow` itself, not in the ViewModel, to allow direct manipulation from `App.xaml.cs`.
