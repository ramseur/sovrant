using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Session;
using SessionItem = Sovrant.Runtime.Session.SessionListItem;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Phase 133 — conversation folders in the Desktop sidebar: the FOLDERS / UNFILED
/// tree, search across folders, ⋯ menu actions, drag and drop, and the open
/// conversation's folder path for the chat header. Tree logic and rules come from
/// <see cref="SessionFolderTree"/> / <see cref="SessionFolderRules"/>, shared with Web.
/// </summary>
public partial class SidebarViewModel
{
    private SessionSidebarService? _folderService;
    private SessionSidebarService FolderService => _folderService ??= SessionSidebarService.From(App.Services);

    private IReadOnlyList<SessionFolder> _folders = [];
    private IReadOnlyList<SessionItem> _sessionItems = [];
    private HashSet<string> _expanded = new(StringComparer.Ordinal);
    private bool _expandedLoaded;
    private int _hoverExpandGeneration;

    public ObservableCollection<SessionTreeRowViewModel> FolderRows { get; } = [];
    public ObservableCollection<SessionTreeRowViewModel> UnfiledRows { get; } = [];
    public ObservableCollection<SessionTreeRowViewModel> SearchRows { get; } = [];

    /// <summary>Move picker + name prompt, rendered as a window-wide overlay by MainWindow.</summary>
    public SessionFolderDialogViewModel FolderDialog { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _folderError;

    [ObservableProperty]
    private bool _hasFolders;

    [ObservableProperty]
    private string _unfiledHeader = "RECENT";

    [ObservableProperty]
    private string _unfiledEmptyText = "No recent conversations";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTopValidDrop), nameof(IsTopRefusedDrop))]
    private bool _isTopDropTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnfiledValidDrop), nameof(IsUnfiledRefusedDrop))]
    private bool _isUnfiledDropTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTopValidDrop), nameof(IsTopRefusedDrop), nameof(IsUnfiledValidDrop), nameof(IsUnfiledRefusedDrop), nameof(HasTopDropRefusal), nameof(HasUnfiledDropRefusal))]
    private string? _headDropRefusal;

    public bool IsTopValidDrop => IsTopDropTarget && HeadDropRefusal is null;
    public bool IsTopRefusedDrop => IsTopDropTarget && HeadDropRefusal is not null;
    public bool IsUnfiledValidDrop => IsUnfiledDropTarget && HeadDropRefusal is null;
    public bool IsUnfiledRefusedDrop => IsUnfiledDropTarget && HeadDropRefusal is not null;
    public bool HasTopDropRefusal => IsTopDropTarget && HeadDropRefusal is not null;
    public bool HasUnfiledDropRefusal => IsUnfiledDropTarget && HeadDropRefusal is not null;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsBrowsing => !IsSearching;
    public bool HasUnfiled => UnfiledRows.Count > 0;
    public bool HasSearchRows => SearchRows.Count > 0;
    public bool HasFolderError => !string.IsNullOrEmpty(FolderError);

    // ── open conversation (chat header) ─────────────────────────────────────

    [ObservableProperty]
    private string? _currentSessionId;

    [ObservableProperty]
    private string _currentPath = "Unfiled";

    [ObservableProperty]
    private string _currentTitle = SessionFolderTree.UntitledText;

    public ObservableCollection<SessionLabel> CurrentLabels { get; } = [];

    private string? _currentFolderId;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(IsBrowsing));
        RebuildRows();
    }

    partial void OnFolderErrorChanged(string? value) => OnPropertyChanged(nameof(HasFolderError));

    partial void OnCurrentSessionIdChanged(string? value)
    {
        foreach (var row in AllRows())
            row.IsCurrent = row.IsSession && row.Id == value;
        UpdateCurrentHeader();
    }

    // ── loading ─────────────────────────────────────────────────────────────

    private DateTimeOffset _lastExternalRefresh = DateTimeOffset.MinValue;

    /// <summary>
    /// Reloads the folder tree to pick up changes made elsewhere (e.g. on Web, which
    /// shares the same database). Called when the Chat menu opens and when the window
    /// regains focus; throttled so focus flicker doesn't trigger repeated reloads.
    /// </summary>
    public Task RefreshFromOtherSurfacesAsync()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastExternalRefresh < TimeSpan.FromSeconds(2))
            return Task.CompletedTask;
        _lastExternalRefresh = now;
        return LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        var me = App.SovrantUserId;
        try
        {
            if (!_expandedLoaded)
            {
                _expanded = await FolderService.LoadExpandedAsync(me).ConfigureAwait(false);
                _expandedLoaded = true;
            }
            var snapshot = await FolderService.LoadAsync(me).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _folders = snapshot.Folders;
                _sessionItems = snapshot.Sessions;
                var current = _sessionItems.FirstOrDefault(s => s.SessionId == CurrentSessionId);
                if (current?.FolderId is { } folderId)
                    _expanded.UnionWith(SessionFolderTree.AncestorsOf(_folders, folderId));
                RebuildRows();
                UpdateCurrentHeader();
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => FolderError = $"Couldn't load conversations: {ex.Message}");
        }
    }

    private void RebuildRows()
    {
        HasFolders = _folders.Count > 0;
        UnfiledHeader = HasFolders ? "UNFILED" : "RECENT";
        UnfiledEmptyText = _sessionItems.Count == 0 ? "No recent conversations" : "Everything is filed";

        Fill(FolderRows, IsSearching ? [] : SessionFolderTree.FolderRows(_folders, _sessionItems, _expanded));
        Fill(UnfiledRows, IsSearching ? [] : SessionFolderTree.UnfiledRows(_folders, _sessionItems));
        Fill(SearchRows, IsSearching ? SessionFolderTree.SearchRows(_folders, _sessionItems, SearchText) : []);
        OnPropertyChanged(nameof(HasUnfiled));
        OnPropertyChanged(nameof(HasSearchRows));
    }

    private void Fill(ObservableCollection<SessionTreeRowViewModel> target, IReadOnlyList<SessionTreeRow> rows)
    {
        target.Clear();
        foreach (var row in rows)
        {
            var vm = new SessionTreeRowViewModel(row)
            {
                IsCurrent = row.Kind == SessionTreeRowKind.Session && row.Id == CurrentSessionId,
                IsRunning = row.Kind == SessionTreeRowKind.Session && (_activeSessions?.HasSession(row.Id) ?? false),
                CanAddSubfolder = row.Kind == SessionTreeRowKind.Folder
                    && SessionFolderRules.DepthOf(_folders, row.Id) < SessionFolderRules.MaxDepth,
                DeleteHint = row.Kind == SessionTreeRowKind.Folder ? DeleteHintFor(row.FolderId) : null,
            };
            target.Add(vm);
        }
    }

    private IEnumerable<SessionTreeRowViewModel> AllRows() => FolderRows.Concat(UnfiledRows).Concat(SearchRows);

    private void RefreshRunningStates()
    {
        foreach (var row in AllRows())
            row.IsRunning = row.IsSession && (_activeSessions?.HasSession(row.Id) ?? false);
    }

    private void UpdateCurrentHeader()
    {
        var item = _sessionItems.FirstOrDefault(s => s.SessionId == CurrentSessionId);
        _currentFolderId = item?.FolderId;
        var path = SessionFolderTree.PathOf(_folders, _currentFolderId);
        CurrentPath = string.IsNullOrEmpty(path) ? "Unfiled" : path;
        CurrentTitle = item is null ? SessionFolderTree.UntitledText : SessionFolderTree.DisplayTitle(item);
        CurrentLabels.Clear();
        foreach (var label in item?.Labels ?? [])
            CurrentLabels.Add(label);
    }

    private async Task SaveExpandedAsync()
    {
        try { await FolderService.SaveExpandedAsync(App.SovrantUserId, _expanded).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* remembering open folders is best-effort */ }
    }

    /// <summary>Runs a folder/conversation change, shows a refusal inline, then reloads the tree.</summary>
    private async Task RunFolderChangeAsync(Func<Task> change)
    {
        try
        {
            FolderError = null;
            await change().ConfigureAwait(false);
        }
        catch (SessionFolderException ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => FolderError = ex.Message);
        }
        await LoadSessionsAsync().ConfigureAwait(false);
    }

    // ── row commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenRowAsync(SessionTreeRowViewModel row)
    {
        if (row.IsFolder)
            await ToggleFolderAsync(row);
        else
            ResumeSession(row.Id);
    }

    [RelayCommand]
    private async Task ToggleFolderAsync(SessionTreeRowViewModel row)
    {
        if (!_expanded.Add(row.Id))
            _expanded.Remove(row.Id);
        RebuildRows();
        await SaveExpandedAsync();
    }

    [RelayCommand]
    private void DismissFolderError() => FolderError = null;

    [RelayCommand]
    private void NewFolder() => OpenNameDialog("New folder", "Create", string.Empty, async name =>
        await FolderService.CreateFolderAsync(App.SovrantUserId, name, null).ConfigureAwait(false));

    [RelayCommand]
    private void NewSubfolder(SessionTreeRowViewModel row) => OpenNameDialog($"New folder in “{row.Text}”", "Create", string.Empty, async name =>
    {
        await FolderService.CreateFolderAsync(App.SovrantUserId, name, row.Id).ConfigureAwait(false);
        _expanded.UnionWith(SessionFolderTree.AncestorsOf(_folders, row.Id));
        await SaveExpandedAsync().ConfigureAwait(false);
    });

    [RelayCommand]
    private void RenameRow(SessionTreeRowViewModel row)
    {
        if (row.IsFolder)
        {
            OpenNameDialog("Rename folder", "Rename", row.Text, async name =>
                await FolderService.RenameFolderAsync(App.SovrantUserId, row.Id, name).ConfigureAwait(false));
        }
        else
        {
            var current = row.Text == SessionFolderTree.UntitledText ? string.Empty : row.Text;
            OpenNameDialog("Rename conversation", "Rename", current, async name =>
                await _sessionStore.SetTitleAsync(row.Id, SessionFolderRules.NormalizeName(name), ownerUserId: App.SovrantUserId).ConfigureAwait(false));
        }
    }

    [RelayCommand]
    private void MoveRow(SessionTreeRowViewModel row) =>
        OpenMoveDialog(row.IsFolder ? row.Id : null, row.IsSession ? row.Id : null, row.Text, row.FolderId);

    [RelayCommand]
    private void MoveCurrentSession()
    {
        if (CurrentSessionId is { } id)
            OpenMoveDialog(null, id, CurrentTitle, _currentFolderId);
    }

    [RelayCommand]
    private async Task DeleteFolderAsync(SessionTreeRowViewModel row) =>
        await RunFolderChangeAsync(async () =>
        {
            await FolderService.DeleteFolderAsync(App.SovrantUserId, row.Id).ConfigureAwait(false);
            _expanded.Remove(row.Id);
            await SaveExpandedAsync().ConfigureAwait(false);
        });

    [RelayCommand]
    private async Task DeleteRowSessionAsync(SessionTreeRowViewModel row) => await DeleteSessionAsync(row.Id);

    /// <summary>"Conversations and subfolders inside move up to …" — the delete-folder hint.</summary>
    private string DeleteHintFor(string? parentFolderId) =>
        $"Conversations and subfolders inside move up to {(parentFolderId is null ? "the top level" : $"“{_folders.FirstOrDefault(f => f.FolderId == parentFolderId)?.Name}”")}. No conversation is ever deleted.";

    // ── dialogs ─────────────────────────────────────────────────────────────

    private void OpenNameDialog(string heading, string action, string initial, Func<string, Task> submit) =>
        FolderDialog.OpenName(heading, action, initial, async name =>
        {
            await submit(name).ConfigureAwait(false);
            await LoadSessionsAsync().ConfigureAwait(false);
        });

    private void OpenMoveDialog(string? movingFolderId, string? sessionId, string title, string? currentParent)
    {
        var rows = SessionFolderTree.PickerRows(_folders, movingFolderId)
            .Select(r => new FolderPickRowViewModel(r.Folder.FolderId, r.Folder.Name, r.Depth,
                r.Refusal is null ? null : SessionFolderRules.Describe(r.Refusal.Value), r.Folder.FolderId == currentParent))
            .ToList();
        FolderDialog.OpenMove(
            $"Move “{title}”",
            movingFolderId is null
                ? "Pick a folder. Links to its agent, workflow, and runs stay as they are — a folder only changes where the conversation is listed."
                : "Pick where this folder and everything in it should go.",
            movingFolderId is null ? "Unfiled" : "Top level",
            currentParent,
            rows,
            move: async target =>
            {
                if (movingFolderId is not null)
                    await FolderService.MoveFolderAsync(App.SovrantUserId, movingFolderId, target).ConfigureAwait(false);
                else if (sessionId is not null && !await FolderService.MoveSessionAsync(App.SovrantUserId, sessionId, target).ConfigureAwait(false))
                    throw new SessionFolderException("That conversation can't be moved — it may have been deleted.");
                if (target is not null)
                    _expanded.UnionWith(SessionFolderTree.AncestorsOf(_folders, target));
                await SaveExpandedAsync().ConfigureAwait(false);
                await LoadSessionsAsync().ConfigureAwait(false);
            },
            createFolder: async (name, parent) =>
            {
                var created = await FolderService.CreateFolderAsync(App.SovrantUserId, name, parent).ConfigureAwait(false);
                await LoadSessionsAsync().ConfigureAwait(false);
                var refreshed = SessionFolderTree.PickerRows(_folders, movingFolderId)
                    .Select(r => new FolderPickRowViewModel(r.Folder.FolderId, r.Folder.Name, r.Depth,
                        r.Refusal is null ? null : SessionFolderRules.Describe(r.Refusal.Value), r.Folder.FolderId == currentParent))
                    .ToList();
                return (created.FolderId, (IReadOnlyList<FolderPickRowViewModel>)refreshed);
            });
    }

    // ── drag and drop (driven by SidebarView code-behind) ───────────────────

    /// <summary>The row being dragged, or null.</summary>
    public SessionTreeRowViewModel? DragRow { get; private set; }

    public void BeginDrag(SessionTreeRowViewModel row)
    {
        DragRow = row;
        row.IsDragging = true;
    }

    /// <summary>Why dropping the dragged row into <paramref name="targetFolderId"/> is refused, or null.</summary>
    public string? DropRefusalFor(string? targetFolderId)
    {
        if (DragRow is null || DragRow.IsSession)
            return null;
        return SessionFolderRules.CheckMove(_folders, DragRow.Id, targetFolderId) is { } e ? SessionFolderRules.Describe(e) : null;
    }

    /// <summary>Marks <paramref name="target"/> as the current drop target (or clears all when null).</summary>
    public void SetDropTarget(SessionTreeRowViewModel? target)
    {
        CancelHoverExpand();
        IsTopDropTarget = IsUnfiledDropTarget = false;
        HeadDropRefusal = null;
        foreach (var row in AllRows())
        {
            row.IsDropTarget = false;
            row.DropRefusal = null;
        }
        if (DragRow is null || target is null || !target.IsFolder)
            return;

        target.IsDropTarget = true;
        target.DropRefusal = DropRefusalFor(target.Id);

        // Hovering a collapsed folder for ~500 ms expands it so you can drop deeper.
        if (!target.IsExpanded && target.HasChildren)
        {
            var generation = ++_hoverExpandGeneration;
            _ = Task.Delay(500).ContinueWith(_ =>
            {
                if (generation != _hoverExpandGeneration || DragRow is null) return;
                Dispatcher.UIThread.Post(() =>
                {
                    if (DragRow is null || !target.IsDropTarget) return;
                    _expanded.Add(target.Id);
                    var dragId = DragRow.Id;
                    RebuildRows();
                    // Rebuild replaced the row objects; restore the drag + target marks.
                    DragRow = AllRows().FirstOrDefault(r => r.Id == dragId) ?? DragRow;
                    DragRow.IsDragging = true;
                    if (AllRows().FirstOrDefault(r => r.Id == target.Id && r.IsFolder) is { } again)
                    {
                        again.IsDropTarget = true;
                        again.DropRefusal = DropRefusalFor(again.Id);
                    }
                });
            }, TaskScheduler.Default);
        }
    }

    /// <summary>Marks the FOLDERS (<paramref name="top"/>) or UNFILED heading as the drop target.</summary>
    public void SetHeadDropTarget(bool top)
    {
        SetDropTarget(null);
        if (DragRow is null) return;
        IsTopDropTarget = top;
        IsUnfiledDropTarget = !top;
        HeadDropRefusal = top
            ? (DragRow.IsFolder ? DropRefusalFor(null) : "Drop conversations on a folder, or on Unfiled.")
            : (DragRow.IsSession ? null : "Drop folders on another folder, or on the Folders heading.");
    }

    /// <summary>Completes a drop into <paramref name="targetFolderId"/> (null = top level / unfiled).</summary>
    public async Task DropAsync(string? targetFolderId, bool ontoHeading)
    {
        var drag = DragRow;
        var refusal = ontoHeading ? HeadDropRefusal : DropRefusalFor(targetFolderId);
        EndDrag();
        if (drag is null || refusal is not null || drag.FolderId == targetFolderId)
            return;
        await RunFolderChangeAsync(async () =>
        {
            if (drag.IsFolder)
                await FolderService.MoveFolderAsync(App.SovrantUserId, drag.Id, targetFolderId).ConfigureAwait(false);
            else
                await FolderService.MoveSessionAsync(App.SovrantUserId, drag.Id, targetFolderId).ConfigureAwait(false);
            if (targetFolderId is not null)
            {
                _expanded.UnionWith(SessionFolderTree.AncestorsOf(_folders, targetFolderId));
                await SaveExpandedAsync().ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public void EndDrag()
    {
        CancelHoverExpand();
        if (DragRow is not null)
            DragRow.IsDragging = false;
        DragRow = null;
        SetDropTarget(null);
    }

    /// <summary>Invalidates any pending hover-to-expand timer.</summary>
    private void CancelHoverExpand() => _hoverExpandGeneration++;
}

/// <summary>Phase 133 — one sidebar row (folder or conversation) as Avalonia binds it.</summary>
public partial class SessionTreeRowViewModel : ViewModelBase
{
    public SessionTreeRowViewModel(SessionTreeRow row)
    {
        Row = row;
    }

    public SessionTreeRow Row { get; }
    public string Id => Row.Id;
    public string Text => Row.Text;
    public string? FolderId => Row.FolderId;
    public bool IsFolder => Row.Kind == SessionTreeRowKind.Folder;
    public bool IsSession => !IsFolder;
    public bool IsExpanded => Row.IsExpanded;
    public bool HasChildren => Row.HasChildren;
    public int Count => Row.Count;
    public string? Meta => Row.Meta;
    public bool HasMeta => !string.IsNullOrEmpty(Row.Meta);
    public bool MetaActive => Row.MetaActive;

    /// <summary>16px per level, matching the design mock's <c>--d</c> indent.</summary>
    public Avalonia.Thickness Indent => new(6 + Row.Depth * 16, 0, 0, 0);

    /// <summary>Chevron rotation: 90° when expanded.</summary>
    public double ChevronAngle => Row.IsExpanded ? 90 : 0;

    public bool CanAddSubfolder { get; init; }

    /// <summary>Folders only: where a delete moves this folder's contents.</summary>
    public string? DeleteHint { get; init; }

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isDragging;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValidDrop), nameof(IsRefusedDrop))]
    private bool _isDropTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValidDrop), nameof(IsRefusedDrop), nameof(HasDropRefusal))]
    private string? _dropRefusal;

    public bool IsValidDrop => IsDropTarget && DropRefusal is null;
    public bool IsRefusedDrop => IsDropTarget && DropRefusal is not null;
    public bool HasDropRefusal => DropRefusal is not null;
}

/// <summary>Phase 133 — one folder in the Move picker.</summary>
public partial class FolderPickRowViewModel(string folderId, string name, int depth, string? refusal, bool isCurrent) : ViewModelBase
{
    public string FolderId { get; } = folderId;
    public string Name { get; } = name;
    public Avalonia.Thickness Indent { get; } = new(10 + depth * 16, 0, 0, 0);
    public string? Refusal { get; } = refusal;
    public bool IsEnabled => Refusal is null;
    public bool IsCurrent { get; } = isCurrent;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Phase 133 — state for the window-wide folder overlay: either the Move picker or
/// the one-field name prompt. Refusals (<see cref="SessionFolderException"/>) are
/// shown inline and keep the dialog open.
/// </summary>
public partial class SessionFolderDialogViewModel : ViewModelBase
{
    private Func<string?, Task>? _move;
    private Func<string, string?, Task<(string FolderId, IReadOnlyList<FolderPickRowViewModel> Rows)>>? _createFolder;
    private Func<string, Task>? _submitName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private bool _isMoveOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private bool _isNameOpen;

    public bool IsOpen => IsMoveOpen || IsNameOpen;

    [ObservableProperty]
    private string _heading = string.Empty;

    [ObservableProperty]
    private string _subheading = string.Empty;

    [ObservableProperty]
    private string _rootLabel = "Unfiled";

    [ObservableProperty]
    private bool _isRootSelected;

    [ObservableProperty]
    private bool _isRootCurrent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => !string.IsNullOrEmpty(Error);

    [ObservableProperty]
    private string _nameValue = string.Empty;

    [ObservableProperty]
    private string _actionText = "Save";

    [ObservableProperty]
    private bool _isCreatingFolder;

    [ObservableProperty]
    private string _newFolderName = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<FolderPickRowViewModel> PickRows { get; } = [];

    private string? _currentParent;
    private string? _target;

    public bool CanMove => !IsBusy && _target != _currentParent;

    public void OpenMove(
        string heading, string subheading, string rootLabel, string? currentParent,
        IReadOnlyList<FolderPickRowViewModel> rows,
        Func<string?, Task> move,
        Func<string, string?, Task<(string FolderId, IReadOnlyList<FolderPickRowViewModel> Rows)>> createFolder)
    {
        Heading = heading;
        Subheading = subheading;
        RootLabel = rootLabel;
        _currentParent = currentParent;
        _move = move;
        _createFolder = createFolder;
        Error = null;
        IsCreatingFolder = false;
        NewFolderName = string.Empty;
        IsRootCurrent = currentParent is null;
        SetRows(rows);
        Select(currentParent);
        IsNameOpen = false;
        IsMoveOpen = true;
    }

    public void OpenName(string heading, string action, string initial, Func<string, Task> submit)
    {
        Heading = heading;
        ActionText = action;
        NameValue = initial;
        _submitName = submit;
        Error = null;
        IsMoveOpen = false;
        IsNameOpen = true;
    }

    private void SetRows(IReadOnlyList<FolderPickRowViewModel> rows)
    {
        PickRows.Clear();
        foreach (var row in rows)
            PickRows.Add(row);
    }

    private void Select(string? folderId)
    {
        _target = folderId;
        IsRootSelected = folderId is null;
        foreach (var row in PickRows)
            row.IsSelected = row.FolderId == folderId;
        OnPropertyChanged(nameof(CanMove));
    }

    [RelayCommand]
    private void SelectRoot() => Select(null);

    [RelayCommand]
    private void SelectFolder(FolderPickRowViewModel row)
    {
        if (row.IsEnabled)
            Select(row.FolderId);
    }

    [RelayCommand]
    private void StartCreateFolder()
    {
        Error = null;
        IsCreatingFolder = true;
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        if (_createFolder is null || string.IsNullOrWhiteSpace(NewFolderName)) return;
        try
        {
            var (id, rows) = await _createFolder(NewFolderName, _target).ConfigureAwait(true);
            IsCreatingFolder = false;
            NewFolderName = string.Empty;
            SetRows(rows);
            Select(id);
        }
        catch (SessionFolderException ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ConfirmMoveAsync()
    {
        if (_move is null || !CanMove) return;
        IsBusy = true;
        try
        {
            await _move(_target).ConfigureAwait(true);
            IsMoveOpen = false;
        }
        catch (SessionFolderException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanMove));
        }
    }

    [RelayCommand]
    private async Task ConfirmNameAsync()
    {
        if (_submitName is null || string.IsNullOrWhiteSpace(NameValue)) return;
        IsBusy = true;
        try
        {
            await _submitName(NameValue).ConfigureAwait(true);
            IsNameOpen = false;
        }
        catch (SessionFolderException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsMoveOpen = false;
        IsNameOpen = false;
    }
}
