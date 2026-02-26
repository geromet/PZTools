using System.Reactive;
using System.Reactive.Linq;
using DataInput;
using DataInput.Errors;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using UI.UndoRedo;

namespace UI.ViewModels;

/// <summary>
/// Root ViewModel. Implements IScreen so it owns the ReactiveUI Router,
/// meaning the center panel is fully navigation-driven.
///
/// Persistent side panels (distribution list, error list) are owned here and
/// injected with the shared UndoRedoStack and IScreen reference so they can
/// participate in routing and undo without knowing about MainViewModel.
///
/// Future views (mod comparison, batch editor, etc.) are added by registering
/// a new IViewFor&lt;TViewModel&gt; with Splat and pushing onto Router.Navigate.
/// </summary>
public sealed class MainViewModel : ViewModelBase, IScreen
{
    // ── IScreen ───────────────────────────────────────────────────────────────
    public RoutingState Router { get; } = new();

    // ── Shared services ───────────────────────────────────────────────────────
    public UndoRedoStack UndoRedo { get; } = new();

    // ── Persistent side-panel ViewModels ─────────────────────────────────────
    public DistributionListViewModel DistributionList { get; }
    public ErrorListViewModel        ErrorList        { get; }

    // ── Status ────────────────────────────────────────────────────────────────
    [Reactive] public string StatusText { get; private set; } = "Open a folder to begin.";
    [Reactive] public bool   IsLoading  { get; private set; }

    private string? _lastFolder;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Accepts the selected folder path as parameter and runs the parse on the
    /// thread pool. The UI calls this after receiving the path from the
    /// StorageProvider (which must stay in code-behind since it needs a Window ref).
    /// </summary>
    public ReactiveCommand<string, ParseResult?> ParseCommand { get; }

    /// <summary>Reload the last folder without re-opening the picker.</summary>
    public ReactiveCommand<Unit, ParseResult?>  ReloadCommand { get; }

    // ── Keyboard shortcuts exposed as commands ────────────────────────────────
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    public MainViewModel()
    {
        DistributionList = new DistributionListViewModel(this, UndoRedo);
        ErrorList        = new ErrorListViewModel();

        // ── Parse command ─────────────────────────────────────────────────────
        ParseCommand = ReactiveCommand.CreateFromTask<string, ParseResult?>(
            async folder =>
            {
                _lastFolder = folder;
                return await Task.Run(() =>
                    DistributionParser.CreateDefault().Parse(folder));
            },
            canExecute: this.WhenAnyValue(x => x.IsLoading).Select(b => !b));

        // IsLoading tracks the command's execution state
        ParseCommand.IsExecuting
            .Subscribe(executing => IsLoading = executing)
            .DisposeWith(Disposables);

        // On success: populate side-panel VMs and navigate to empty state
        ParseCommand
            .Where(r => r is not null)
            .Subscribe(result =>
            {
                DistributionList.Load(result!.Distributions);
                ErrorList.Load(result.Errors);
                Router.Navigate.Execute(new EmptyStateViewModel(this));

                var fatals   = result.Errors.Count(e => e.IsFatal);
                var warnings = result.Errors.Count(e => !e.IsFatal);
                StatusText = $"{result.Distributions.Count} distributions  ·  " +
                             $"{fatals} errors  ·  {warnings} warnings";
            })
            .DisposeWith(Disposables);

        // On failure: show error in status bar
        ParseCommand.ThrownExceptions
            .Subscribe(ex => StatusText = $"Parse failed: {ex.Message}")
            .DisposeWith(Disposables);

        // ── Reload command ────────────────────────────────────────────────────
        ReloadCommand = ReactiveCommand.CreateFromTask<Unit, ParseResult?>(
            async _ => _lastFolder is null ? null
                : await Task.Run(() => DistributionParser.CreateDefault().Parse(_lastFolder)),
            canExecute: this.WhenAnyValue(x => x.IsLoading, x => x._lastFolder,
                (loading, folder) => !loading && folder is not null));

        // Subscribe reload to the same result handler by sharing ParseCommand's logic
        ReloadCommand
            .Where(r => r is not null)
            .Subscribe(result =>
            {
                DistributionList.Load(result!.Distributions);
                ErrorList.Load(result.Errors);
                Router.Navigate.Execute(new EmptyStateViewModel(this));
            })
            .DisposeWith(Disposables);

        ReloadCommand.ThrownExceptions
            .Subscribe(ex => StatusText = $"Reload failed: {ex.Message}")
            .DisposeWith(Disposables);

        // ── Undo / Redo commands (keyboard shortcut proxies) ──────────────────
        UndoCommand = UndoRedo.UndoCommand;
        RedoCommand = UndoRedo.RedoCommand;

        // Navigate to empty state on launch
        Router.Navigate.Execute(new EmptyStateViewModel(this));
    }
}
