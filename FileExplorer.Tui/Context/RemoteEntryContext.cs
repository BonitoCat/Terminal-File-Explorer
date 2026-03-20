using System.Diagnostics;
using FileExplorer.Tui.Options;
using FileExplorer.Tui.RemotePaths;
using FileLib;
using InputLib;
using InputLib.PlatformListener;
using LoggerLib;
using TuiLib;
using TuiLib.Controls;

namespace FileExplorer.Tui.Context;

public class RemoteEntryContext : IEntryContext
{
    public IRemoteConnection Connection { get; }
    public string CurrentPath => Connection.CurrentPath;
    public bool IsLoading { get; private set; }
    public TuiListBox<TuiLabel> Menu { get; set; }
    public InputListener? Listener { get; set; }
    public object OutLock { get; set; }
    public Process? CommandLine { get; set; }
    public string Cwd { get; set; }
    public string BookmarkDir { get; set; }
    public string? SearchString { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool ShowFileSizes { get; set; }
    public bool IsReloading { get; set; }
    public bool CanDraw { get; set; } = true;
    public int CachedLongestFileLine { get; set; }
    public Stack<string> DirHistory { get; set; } = new();
    public List<TuiListBoxItem<TuiLabel>> SelectedItems { get; } = [];
    public CancellationTokenSource RefreshCancelSource { get; set; }
    public ClipboardContext ClipboardContext { get; set; }
    public ManualResetEventSlim ExitEvent { get; set; }
    public bool ForceTtyInput { get; set; }
    public bool IsRemote { get; } = true;
    
    private volatile int _dirty;
    private readonly AutoResetEvent _renderSignal = new(false);
    private CancellationTokenSource? _renderCts;
    private Task? _renderTask;
    
    public static IEntryContext Create()
    {
        return new RemoteEntryContext();
    }

    private RemoteEntryContext()
    {
        
    }
    
    public RemoteEntryContext(IRemoteConnection connection, MenuContextOptions options)
    {
        Connection = connection;
        Menu = new() { ScrollBarColor = Color.Gray };
        ClipboardContext = options.ClipboardContext;
        OutLock = options.OutLock;
        ExitEvent = options.ExitEvent;
        ForceTtyInput = options.ForceTtyInput;
        
        Listener = options.ForceTtyInput ? new TtyInputListener() : InputListener.New();
        if (Listener == null)
        {
            throw new InvalidOperationException("Could not load input listener\n");
        }
        
        Logger.LogI($"Created new input listener of type: {Listener.GetType()}");
        
        Listener.PauseListening = true;
        Listener.RaiseEvents = false;
        
        OnClickDir(new TuiLabel(Directory.GetCurrentDirectory()), false);
        
        BookmarkDir = Path.Combine(DirectoryHelper.GetAppDataDirPath(), "fe", "Bookmarks");
        DirectoryHelper.CreateDir(BookmarkDir);

        Listener.RepeatIntervalMs = 30;
        Listener.StartListening();

        Listener.OnKeyDown += options.HandleKeyDown;
        Listener.OnKeyUp += options.HandleKeyUp;
        Listener.OnKeyJustPressed += options.HandleKeyJustPressed;

        Menu.MenuUpdate += options.MenuUpdate;
        
        StartRenderLoop();
        Logger.LogI("Created new remote menu");
    }
    
    public void StartRenderLoop()
    {
        if (_renderTask != null)
        {
            return;
        }

        _renderCts = new CancellationTokenSource();
        CancellationToken token = _renderCts.Token;

        _renderTask = Task.Factory.StartNew(() =>
        {
            Stopwatch frameTimer = Stopwatch.StartNew();
            long lastFrameMs = 0;
            const int frameMs = 16;

            while (!token.IsCancellationRequested)
            {
                _renderSignal.WaitOne();

                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (!CanDraw)
                {
                    continue;
                }

                if (Interlocked.Exchange(ref _dirty, 0) == 0)
                {
                    continue;
                }

                long now = frameTimer.ElapsedMilliseconds;
                long elapsedSinceLast = now - lastFrameMs;
                if (elapsedSinceLast < frameMs)
                {
                    Thread.Sleep((int) (frameMs - elapsedSinceLast));
                }

                try
                {
                    Menu.MenuUpdate.Invoke();
                }
                catch { }

                lastFrameMs = frameTimer.ElapsedMilliseconds;

                if (Volatile.Read(ref _dirty) != 0)
                {
                    _renderSignal.Set();
                }
            }
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void StopRenderLoop()
    {
        _renderCts?.Cancel();
        _renderSignal.Set();

        _renderCts?.Dispose();
        _renderCts = null;
        _renderTask = null;
    }

    public void RedrawMenu()
    {
        if (!CanDraw)
        {
            return;
        }

        Interlocked.Exchange(ref _dirty, 1);
        _renderSignal.Set();
    }

    public void RedrawMenuSync()
    {
        if (!CanDraw)
        {
            return;
        }
        
        Menu.MenuUpdate.Invoke();
    }

    public void DisableDrawing()
    {
        Logger.LogI("Disabled drawing");
        CanDraw = false;
    }

    public void EnableDrawing()
    {
        Logger.LogI("Enabled drawing");
        CanDraw = true;
    }

    public void RefreshItems()
    {
        
    }

    public void SelectItem()
    {
        
    }

    public string? Input(string inputText, string startValue = "", bool enterNull = false, bool escapeNo = false,
                         bool inputHidden = false)
    {
        return "";
    }

    public void OnClickDir(TuiLabel sender, bool saveToHistory = true)
    {
        _ = NavigateAsync(sender.Text);
    }

    public bool RequiresElevatedAccess(string path)
    {
        return false;
    }

    public Task DeleteRemoteAsync(TuiListBoxItem<TuiLabel> item)
    {
        return Task.CompletedTask;
    }

    public Task RenameRemoteAsync(TuiListBoxItem<TuiLabel> item, string newName)
    {
        return Task.CompletedTask;
    }

    public Task CreateRemoteDirectoryAsync(string name)
    {
        return Task.CompletedTask;
    }

    public Task CreateRemoteFileAsync(string name)
    {
        return Task.CompletedTask;
    }

    public void CopyDirectory(string sourceDir, string destinationDir)
    {
        
    }

    public async Task NavigateAsync(string path)
    {
        IsLoading = true;
        Cwd = path;
        
        Menu.ClearItems();
        Menu.AddItem(new TuiListBoxItem<TuiLabel>("  Connecting..."));
        RedrawMenu();

        try
        {
            List<RemoteItem> items = await Connection.ListDirectoryAsync(path);
            Menu.ClearItems();

            string? parent = GetParentPath(path);
            if (parent != null)
            {
                TuiLabel label = new("..")
                {
                    Prefix = "   ",
                };
                
                TuiListBoxItem<TuiLabel> parentItem = new(label);
                parentItem.OnClick += () => _ = NavigateAsync(parent);
                Menu.AddItem(parentItem);
            }

            foreach (RemoteItem item in items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name))
            {
                TuiLabel label = BuildLabel(item);
                TuiListBoxItem<TuiLabel> listItem = new(label)
                {
                    Data =
                    {
                        { "ItemType",   item.IsDirectory ? "Folder" : "File" },
                        { "FullPath",   item.FullPath },
                        { "InfoSize",   item.Size.ToString() },
                        { "RemoteItem", "true" },
                    },
                };

                if (item.IsDirectory)
                {
                    listItem.OnClick += () => OnClickDir(new TuiLabel(item.FullPath));
                }

                Menu.AddItem(listItem);
            }
        }
        catch (Exception ex)
        {
            Menu.ClearItems();
            Menu.AddItem(new TuiListBoxItem<TuiLabel>(new TuiLabel($"Error: {ex.Message}")));
        }
        finally
        {
            IsLoading = false;
            RedrawMenu();
        }
    }

    public async Task DeleteAsync(TuiListBoxItem<TuiLabel> item)
    {
        if (!item.Data.TryGetValue("FullPath", out string? path)) return;
        bool isDir = item.Data.TryGetValue("ItemType", out string? type) && type == "Folder";
        await Connection.DeleteAsync(path, isDir);
        await NavigateAsync(CurrentPath);
    }

    public async Task RenameAsync(TuiListBoxItem<TuiLabel> item, string newName)
    {
        if (!item.Data.TryGetValue("FullPath", out string? path)) return;
        string newPath = GetParentPath(path)?.TrimEnd('/') + '/' + newName ?? newName;
        await Connection.RenameAsync(path, newPath);
        await NavigateAsync(CurrentPath);
    }

    public async Task CreateDirectoryAsync(string name)
    {
        string path = CurrentPath.TrimEnd('/') + '/' + name;
        await Connection.CreateDirectoryAsync(path);
        await NavigateAsync(CurrentPath);
    }

    public async Task CreateFileAsync(string name)
    {
        string path = CurrentPath.TrimEnd('/') + '/' + name;
        await Connection.WriteFileAsync(path, new MemoryStream());
        await NavigateAsync(CurrentPath);
    }

    // Download selected item to a local destination
    public async Task DownloadToAsync(TuiListBoxItem<TuiLabel> item, string localDestDir, IProgress<double>? progress = null)
    {
        if (!item.Data.TryGetValue("FullPath", out string? remotePath)) return;
        string localPath = Path.Combine(localDestDir, Path.GetFileName(remotePath));
        await Connection.DownloadAsync(remotePath, localPath, progress);
    }

    // Upload a local file to the current remote path
    public async Task UploadFromAsync(string localPath, IProgress<double>? progress = null)
    {
        string remotePath = CurrentPath.TrimEnd('/') + '/' + Path.GetFileName(localPath);
        await Connection.UploadAsync(localPath, remotePath, progress);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TuiLabel BuildLabel(RemoteItem item)
    {
        if (item.IsDirectory)
        {
            return new TuiLabel(item.Name, Color.FromRgbString(EntryContext.DarkBlue))
            {
                Prefix = $"{Color.FromRgbString(EntryContext.DarkBlue).ToAnsi()}\x1b[1m🗁  \x1b[0m",
            };
        }

        return new TuiLabel(item.Name)
        {
            Prefix = $"{Color.White.ToAnsi()}\x1b[1m🗏︎  \x1b[0m",
        };
    }

    private static string? GetParentPath(string path)
    {
        string trimmed = path.TrimEnd('/');
        int lastSlash  = trimmed.LastIndexOf('/');
        if (lastSlash <= 0) return null;
        return trimmed[..lastSlash];
    }
}
