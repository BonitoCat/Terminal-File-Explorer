using System.Diagnostics;
using InputLib;
using LoggerLib;
using TuiLib.Controls;

namespace FileExplorer.Tui.Context;

public class QuickAccessEntryContext : IEntryContext
{
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
    public bool CanDraw { get; set; }
    public int CachedLongestFileLine { get; set; }
    public Stack<string> DirHistory { get; set; }
    public List<TuiListBoxItem<TuiLabel>> SelectedItems { get; }
    public CancellationTokenSource RefreshCancelSource { get; set; }
    public ClipboardContext ClipboardContext { get; set; }
    public ManualResetEventSlim ExitEvent { get; set; }
    public bool ForceTtyInput { get; set; }
    public bool IsRemote { get; }
    
    private Timer? _cwdTimer;
    
    private volatile int _dirty;
    private readonly AutoResetEvent _renderSignal = new(false);
    private CancellationTokenSource? _renderCts;
    private Task? _renderTask;
    
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
        CanDraw = false;
    }

    public void EnableDrawing()
    {
        CanDraw = true;
    }

    public void RefreshItems()
    {
        throw new NotImplementedException();
    }

    public void SelectItem()
    {
        return;
    }

    public string? Input(string inputText, string startValue = "", bool enterNull = false, bool escapeNo = false,
                         bool inputHidden = false)
    {
        return null;
    }

    public void OnClickDir(TuiLabel sender, bool saveToHistory = true)
    {
        bool hasFullPath = sender.Data.TryGetValue("FullPath", out string? fullPath);
        if (!hasFullPath || fullPath is null)
        {
            return;
        }
        
        if (RequiresElevatedAccess(fullPath))
        {
            Logger.LogE("Clicked directory requires higher privileges");
            return;
        }
        
        SearchString = null;
        lock (OutLock)
        {
            Console.Clear();
            Logger.LogI("Clearing screen");
        }
        
        if (saveToHistory && Cwd != BookmarkDir)
        {
            DirHistory.Push(Path.GetFullPath(Cwd));
            Logger.LogI("Added directory to stack");
        }
        
        Cwd = Path.GetFullPath(sender.Text);
        Directory.SetCurrentDirectory(Cwd);
    }

    public bool RequiresElevatedAccess(string path)
    {
        throw new NotImplementedException();
    }

    public void CopyDirectory(string sourceDir, string destinationDir)
    {
        throw new NotImplementedException();
    }

    public Task NavigateAsync(string path)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRemoteAsync(TuiListBoxItem<TuiLabel> item)
    {
        throw new NotImplementedException();
    }

    public Task RenameRemoteAsync(TuiListBoxItem<TuiLabel> item, string newName)
    {
        throw new NotImplementedException();
    }

    public Task CreateRemoteDirectoryAsync(string name)
    {
        throw new NotImplementedException();
    }

    public Task CreateRemoteFileAsync(string name)
    {
        throw new NotImplementedException();
    }

    public Task DownloadToAsync(TuiListBoxItem<TuiLabel> item, string localDestDir, IProgress<double>? progress = null)
    {
        throw new NotImplementedException();
    }

    public Task UploadFromAsync(string localPath, IProgress<double>? progress = null)
    {
        throw new NotImplementedException();
    }
}