using System.Diagnostics;
using FileExplorer.Tui.Options;
using InputLib;
using TuiLib.Controls;

namespace FileExplorer.Tui.Context;

public interface IEntryContext
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

    static IEntryContext? Create(MenuContextOptions options)
    {
        return null;
    }
    void StartRenderLoop();
    void StopRenderLoop();
    void RedrawMenu();
    void RedrawMenuSync();
    void DisableDrawing();
    void EnableDrawing();
    void RefreshItems();
    void SelectItem();
    string? Input(string inputText, string startValue = "", bool enterNull = false, bool escapeNo = false, bool inputHidden = false);
    void OnClickDir(TuiLabel sender, bool saveToHistory = true);
    bool RequiresElevatedAccess(string path);
    void CopyDirectory(string sourceDir, string destinationDir);
    
    Task NavigateAsync(string path);
    Task DeleteRemoteAsync(TuiListBoxItem<TuiLabel> item);
    Task RenameRemoteAsync(TuiListBoxItem<TuiLabel> item, string newName);
    Task CreateRemoteDirectoryAsync(string name);
    Task CreateRemoteFileAsync(string name);
    Task DownloadToAsync(TuiListBoxItem<TuiLabel> item, string localDestDir, IProgress<double>? progress = null);
    Task UploadFromAsync(string localPath, IProgress<double>? progress = null);
}