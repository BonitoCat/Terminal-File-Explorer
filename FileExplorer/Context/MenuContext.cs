using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TuiLib;
using TuiLib.Controls;
using FileExplorer.FileTypes;
using InputLib;
using InputLib.EventArgs;
using InputLib.PlatformListener;
using LoggerLib;
using SearchOption = System.IO.SearchOption;

namespace FileExplorer.Context;

public class MenuContext
{
    public const string Red = "200;80;80";
    public const string Green = "130;250;50";
    public const string DarkGreen = "60;160;10";
    private const string Blue = "132;180;250";
    public const string DarkBlue = "60;100;200";
    
    public required CmdListBox<CmdLabel> Menu { get; set; }
    public InputListener? Listener { get; set; }
    public FileSystemWatcher? FileWatcher { get; set; }
    public FileSystemWatcher? ParentWatcher { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool ShowFileSizes { get; set; }
    public Process? CommandLine { get; set; }
    public string BookmarkDir { get; set; } = "";
    public string? SearchString { get; set; }
    public Stack<string> DirHistory { get; set; } = new();
    public List<CmdListBoxItem<CmdLabel>> SelectedItems { get; } = new();
    public CancellationTokenSource RefreshCancelSource { get; set; } = new();
    public bool CanDraw { get; private set; } = true;
    public int CachedLongestFileLine { get; set; } = -1;
    public bool IsReloading { get; private set; }
    public string Cwd { get; set; } = "/";
    public required object OutLock { get; set; }
    public required ClipboardContext ClipboardContext { get; set; }
    public required ManualResetEventSlim ExitEvent;
    public required bool ForceTtyInput { get; init; }
    
    private Timer? _cwdTimer;
    
    private volatile int _dirty;
    private readonly AutoResetEvent _renderSignal = new(false);
    private CancellationTokenSource? _renderCts;
    private Task? _renderTask;

    private static readonly Regex AnsiRegex =
        new(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
            RegexOptions.Compiled);
    
    public void RefreshItems()
    {
        Logger.LogI("Refreshing items...");

        CancellationTokenSource? oldSource = RefreshCancelSource;
        if (oldSource != null)
        {
            oldSource.Cancel();
            oldSource.Dispose();
        }

        CancellationTokenSource cts = new();
        RefreshCancelSource = cts;
        CancellationToken token = cts.Token;

        SelectedItems.Clear();
        Menu.ClearItems();
        
        Task.Run(() =>
        {
            lock (OutLock)
            {
                Console.Clear();
            }
            
            IsReloading = true;
            
            string cwd = Directory.GetCurrentDirectory();
            if (Directory.GetParent(cwd) != null)
            {
                CmdLabel item = new("..", Color.White)
                {
                    Prefix = "   ",
                };
                
                CmdListBoxItem<CmdLabel> lbItem = new(item)
                {
                    Data =
                    {
                        {"ItemType", "Folder"},
                    },
                };
                lbItem.OnClick += () => OnClickDir(item);
                
                Menu.AddItem(lbItem);
            }

            NaturalStringComparer naturalComparer = new();
            Stopwatch stopwatch = new();
            stopwatch.Start();
            
            List<string> dirPaths = Directory.EnumerateDirectories(cwd, "*", SearchOption.TopDirectoryOnly).ToList();
            dirPaths.Sort();

            Logger.LogI($"Directories enumerated in: {stopwatch.ElapsedMilliseconds} ms");

            Menu.AddItemRange(
                dirPaths
                    .Select(dirPath =>
                    {
                        CmdLabel item = new(Path.GetFileName(dirPath), Color.FromRgbString(Blue))
                        {
                            Prefix = $"{Color.FromRgbString(Blue).ToAnsi()}\x1b[1m🗁  \x1b[0m",
                        };
                        
                        CmdListBoxItem<CmdLabel> lbItem = new(item)
                        {
                            Data =
                            {
                                {"ItemType", "Folder"},
                                {"FullPath", dirPath},
                                {"DestinationPath", dirPath},
                                {"DefaultColor", Blue},
                                {"DimmedColor", DarkBlue},
                            },
                        };
                        lbItem.OnClick += () => OnClickDir(new CmdLabel(dirPath));

                        return lbItem;
                    })
                    .Where(item => ShowHiddenFiles || !new DirectoryInfo(item.Item.Text).Attributes.HasFlag(FileAttributes.Hidden))
                    .Where(item => SearchString == null ||
                                   (SearchString != null && item.Item.Text.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase)))
                    .OrderBy(item => item.Item.Text.StartsWith('.'))
                    .ThenBy(item => item.Item.Text, naturalComparer)
                    .ToList());
            
            Logger.LogI($"Directories added to menu in: {stopwatch.ElapsedMilliseconds}ms");
            RedrawMenu();

            if (token.IsCancellationRequested)
            {
                Logger.LogI("Item refresh cancelled");
                IsReloading = false;
                
                return;
            }
            
            ParallelOptions folderOptions = new()
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = 2,
            };

            int foldersLoaded = 0;
            Interlocked.Exchange(ref foldersLoaded, 0);
            
            Parallel.ForEach(Menu.Items.ToList().Skip(1), folderOptions, item =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                
                UpdateFolderAttributes(item, token);
                
                Interlocked.Increment(ref foldersLoaded);
                if (foldersLoaded > 30)
                {
                    RedrawMenu();
                    Interlocked.Exchange(ref foldersLoaded, 0);

                    Thread.Sleep(1);
                }
            });
            
            Logger.LogI($"Updated directory attributes in: {stopwatch.ElapsedMilliseconds}ms");
            RedrawMenu();
            
            if (token.IsCancellationRequested)
            {
                Logger.LogI("Item refresh cancelled");
                IsReloading = false;
                
                return;
            }
            
            List<string> fileNames = Directory.EnumerateFiles(cwd, "*", SearchOption.TopDirectoryOnly).ToList();
            fileNames.Sort();
            
            Logger.LogI($"Files enumerated in: {stopwatch.ElapsedMilliseconds}ms");

            Menu.AddItemRange(
                fileNames
                    .Select(filePath =>
                    {
                        CmdLabel item = new(Path.GetFileName(filePath))
                        {
                            Prefix = $"{Color.White.ToAnsi()}\x1b[1m🗏︎  \x1b[0m",
                        };

                        return new CmdListBoxItem<CmdLabel>(item)
                        {
                            Data =
                            {
                                {"ItemType", "File"},
                                {"FullPath", filePath},
                            },
                        };
                    })
                    .Where(item => ShowHiddenFiles || !File.GetAttributes(item.Item.Text).HasFlag(FileAttributes.Hidden))
                    .Where(item => SearchString == null ||
                                   (SearchString != null && item.Item.Text.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase)))
                    .OrderBy(item => item.Item.Text.StartsWith('.'))
                    .ThenBy(item => item.Item.Text, naturalComparer)
                    .ToList());
            
            Logger.LogI($"Files added to menu in: {stopwatch.ElapsedMilliseconds}ms");
            RedrawMenu();
            
            if (token.IsCancellationRequested)
            {
                Logger.LogI("Item refresh cancelled");
                IsReloading = false;
                
                return;
            }
            
            ParallelOptions fileOptions = new()
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = 2,
            };
            
            int filesLoaded = 0;
            Interlocked.Exchange(ref filesLoaded, 0);

            Parallel.ForEach(Menu.Items.ToList().Skip(1), fileOptions, item =>
            {
                if (!item.Data.TryGetValue("FullPath", out string? fullPath))
                {
                    return;
                }

                string? mime = MimeHelper.GetMimeTypeFast(fullPath);
                UpdateFileAttributesFast(item, mime, token);
                
                Interlocked.Increment(ref filesLoaded);
                if (filesLoaded > 100)
                {
                    RedrawMenu();
                    Interlocked.Exchange(ref filesLoaded, 0);

                    Thread.Sleep(1);
                }
            });
            
            Logger.LogI($"Updated file attributes in: {stopwatch.ElapsedMilliseconds}ms");
            
            IsReloading = false;
            RedrawMenu();
            
            if (token.IsCancellationRequested)
            {
                Logger.LogI("Item refresh cancelled");
                return;
            }
            
            if (Menu.GetItemCount() == 0 && SearchString != null)
            {
                SearchString = null;
                RefreshItems();
            }
            
            if (Menu.SelectedIndex >= Menu.GetItemCount() && !token.IsCancellationRequested)
            {
                Menu.SelectedIndex = Menu.GetItemCount() - 1;
                Menu.ViewIndex = Math.Max(Menu.GetItemCount() - Menu.ViewRange, 0);
            }
            
            if (token.IsCancellationRequested)
            {
                Logger.LogI("Item refresh cancelled");
                return;
            }
            
            Logger.LogI($"Items refreshed, found items: {Menu.GetItemCount()} in {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Stop();
            
            RedrawMenu();
        }, token);
    }
    
    public void UpdateFolderAttributes(CmdListBoxItem<CmdLabel> dir, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }
        
        if (!dir.Data.TryGetValue("ItemType", out string? fileType) || fileType != "Folder")
        {
            return;
        }
        
        string dirName = dir.Item.Text;
        if (dirName == "..")
        {
            return;
        }
        
        if (ClipboardContext.Items.Contains(Path.GetFullPath(dir.Item.Text)))
        {
            dir.Item.Style.Foreground = Color.FromRgbString(DarkBlue);
        }
        
        if (RequiresElevatedAccess(dirName) && Environment.UserName != "root")
        {
            dir.Item.Suffix += $"{Color.Orange.ToAnsi()} (Access Denied)";
            dir.OnClick -= () => OnClickDir(dir.Item);
        }
        
        FileAttributes attributes = new DirectoryInfo(dirName).Attributes;
        if (attributes.HasFlag(FileAttributes.Hidden))
        {
            dir.Data.TryAdd("InfoHidden", "Hidden");
            dir.Item.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
        }
    }

    public void UpdateFileAttributesFast(CmdListBoxItem<CmdLabel> file, string? mime, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }
        
        if (!file.Data.TryGetValue("ItemType", out string? fileType) || fileType != "File")
        {
            return;
        }
        
        string defaultColor = Color.White.ToRgbString();
        string dimmedColor = Color.LightGray.ToRgbString();
        
        if (ExecutableFile.IsExecutable(file.Item.Text))
        {
            file.Item.Prefix = $"{Color.FromRgbString(Green).ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.Item.Style.Foreground = Color.FromRgbString(Green);
                
            defaultColor = Green;
            dimmedColor = DarkGreen;
                
            file.Data.TryAdd("FileType", "Executable");
        }
        else if (mime == null)
        {
            if (ArchiveFile.IsArchive(file.Item.Text))
            {
                Color color = Color.Orange.Transform(-50, -20, -20);
                file.Item.Prefix = $"{color.ToAnsi()}\x1b[1m🗀  \x1b[0m";
                file.Item.Style.Foreground = color;
                
                defaultColor = color.ToRgbString();
                dimmedColor = color.Transform(-40, -70, -50).ToRgbString();
                
                file.Data.TryAdd("FileType", "Archive");
            }
        }
        else if (mime.StartsWith("text/"))
        {
        }
        else if (mime.StartsWith("image/"))
        {
            file.Item.Prefix = $"{Color.Yellow.ToAnsi()}\x1b[1m🖼  \x1b[0m";
            file.Item.Style.Foreground = Color.Yellow;
            
            defaultColor = Color.Yellow.ToRgbString();
            dimmedColor = Color.Yellow.Transform(-70, -90, -40).ToRgbString();
            
            file.Data.TryAdd("FileType", "Image");
        }
        else if (mime.StartsWith("video/"))
        {
            file.Item.Prefix = $"{Color.Orange.ToAnsi()}\x1b[1m🎞  \x1b[0m";
            file.Item.Style.Foreground = Color.Orange;
            
            defaultColor = Color.Orange.ToRgbString();
            dimmedColor = Color.Orange.Transform(-40, -70, -50).ToRgbString();
            
            file.Data.TryAdd("FileType", "Video");
        }
        else if (mime.StartsWith("audio/"))
        {
            file.Item.Prefix = $"{Color.FromRgbString(Red).ToAnsi()}\x1b[1m♪  \x1b[0m";
            file.Item.Style.Foreground = Color.FromRgbString(Red);
            
            defaultColor = Color.Red.ToRgbString();
            dimmedColor = Color.Red.Transform(-40, -40, -20).ToRgbString();
            
            file.Data.TryAdd("FileType", "Audio");
        }
        else if (mime == "application/vnd.debian.binary-package")
        {
            Color color = Color.Yellow.Transform(-20, -20, -20);
            file.Item.Prefix = $"{color.ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.Item.Style.Foreground = color;
            
            defaultColor = color.ToRgbString();
            dimmedColor = color.Transform(-70, -90, -40).ToRgbString();
            
            file.Data.TryAdd("FileType", "Deb");
        }
        
        file.Data["DefaultColor"] = defaultColor;
        file.Data["DimmedColor"] = dimmedColor;
        
        void OnClick()
        {
            file.OnClick -= OnClick;
            
            UpdateFileAttributesAccurate(file, MimeHelper.GetMimeTypeAccurate(file.Item.Text));
            file.CallOnClick();
        }
        
        file.OnClick += OnClick;
        
        string fileName = file.Item.Text;
        FileAttributes attributes = File.GetAttributes(fileName);
        
        if (attributes.HasFlag(FileAttributes.Hidden))
        {
            file.Data["InfoHidden"] = "Hidden";
            file.Item.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
        }

        FileInfo info = new(fileName);
        file.Data["InfoSize"] = info.Length.ToString();
        
        CachedLongestFileLine = Math.Max(file.Item.Length, CachedLongestFileLine);
    }

    public void UpdateFileAttributesAccurate(CmdListBoxItem<CmdLabel> file, string? mime)
    {
        if (!file.Data.TryGetValue("ItemType", out string? fileType) || fileType != "File")
        {
            return;
        }

        string defaultColor = Color.White.ToRgbString();
        string dimmedColor = Color.LightGray.ToRgbString();
        string fileName = file.Item.Text;
        
        if (ExecutableFile.IsExecutable(file.Item.Text))
        {
            file.Item.Prefix = $"{Color.FromRgbString(Green).ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.Item.Style.Foreground = Color.FromRgbString(Green);
                
            defaultColor = Green;
            dimmedColor = DarkGreen;
                
            file.Data.TryAdd("FileType", "Executable");
            file.OnClick += () => ExecutableFile.OnClick(this, fileName);
        }
        if (mime == null)
        {
            if (ArchiveFile.IsArchive(file.Item.Text))
            {
                Color color = Color.Orange.Transform(-50, -20, -20);
                file.Item.Prefix = $"{color.ToAnsi()}\x1b[1m🗀  \x1b[0m";
                file.Item.Style.Foreground = color;
                
                defaultColor = color.ToRgbString();
                dimmedColor = color.Transform(-40, -70, -50).ToRgbString();
                
                file.Data.TryAdd("FileType", "Archive");
                file.OnClick += () => ArchiveFile.OnClick(this, fileName);
            }
        }
        else if (mime.StartsWith("text/"))
        {
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("image/"))
        {
            file.Item.Prefix = $"{Color.Yellow.ToAnsi()}\x1b[1m🖼  \x1b[0m";
            file.Item.Style.Foreground = Color.Yellow;
            
            defaultColor = Color.Yellow.ToRgbString();
            file.Data.TryAdd("DimmedColor", Color.Yellow.Transform(-70, -90, -40).ToRgbString());
            
            file.Data.TryAdd("FileType", "Image");
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("video/"))
        {
            file.Item.Prefix = $"{Color.Orange.ToAnsi()}\x1b[1m🎞  \x1b[0m";
            file.Item.Style.Foreground = Color.Orange;
            
            defaultColor = Color.Orange.ToRgbString();
            dimmedColor = Color.Orange.Transform(-40, -70, -50).ToRgbString();
            
            file.Data.TryAdd("FileType", "Video");
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("audio/"))
        {
            file.Item.Prefix = $"{Color.FromRgbString(Red).ToAnsi()}\x1b[1m♪  \x1b[0m";
            file.Item.Style.Foreground = Color.FromRgbString(Red);
            
            defaultColor = Color.Red.ToRgbString();
            dimmedColor = Color.Red.Transform(-40, -40, -20).ToRgbString();
            
            file.Data.TryAdd("FileType", "Audio");
            file.OnClick += XdgOpen;
        }
        else if (mime == "application/vnd.debian.binary-package")
        {
            Color color = Color.Yellow.Transform(-20, -20, -20);
            file.Item.Prefix = $"{color.ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.Item.Style.Foreground = color;
            
            defaultColor = color.ToRgbString();
            dimmedColor = color.Transform(-70, -90, -40).ToRgbString();
            
            file.Data.TryAdd("FileType", "Deb");
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("application/x-pie-executable"))
        {
            file.Item.Prefix = $"{Color.FromRgbString(Green).ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.Item.Style.Foreground = Color.FromRgbString(Green);
                
            defaultColor = Green;
            dimmedColor = DarkGreen;
                
            file.Data.TryAdd("FileType", "Executable");
            file.OnClick += () => ExecutableFile.OnClick(this, fileName);
        }
        else
        {
            file.OnClick += XdgOpen;
        }
        
        file.Data["DefaultColor"] = defaultColor;
        file.Data["DimmedColor"] = dimmedColor;
        
        FileAttributes attributes = File.GetAttributes(fileName);
        if (attributes.HasFlag(FileAttributes.Hidden))
        {
            file.Data.TryAdd("InfoHidden", "Hidden");
            file.Item.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
        }

        FileInfo info = new(fileName);
        file.Data.TryAdd("InfoSize", info.Length.ToString());
        
        return;

        void XdgOpen()
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }

            Process proc = new()
            {
                StartInfo =
                {
                    FileName = "sh",
                    Arguments = $"-c \"xdg-open '{file.Item.Text}' >/dev/null 2>&1\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            
            proc.Start();
        }
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
    
    public void OnClickDir(CmdLabel sender, bool saveToHistory = true)
    {
        try
        {
            Cwd = Directory.GetCurrentDirectory();
        }
        catch (FileNotFoundException)
        {
            Logger.LogW("Could not find current directory, searching for next best...");
            Cwd = FindExistingTopFolder();
            
            Directory.SetCurrentDirectory(Cwd);
            OnClickDir(new(Cwd), false);
            Logger.LogI("Found new directory");
        }

        if (!Directory.Exists(sender.Text))
        {
            Logger.LogE("Clicked directory not found");
            return;
        }

        if (RequiresElevatedAccess(sender.Text))
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
        FileWatcher?.Dispose();
        ParentWatcher?.Dispose();
        
        FileWatcher = new(Cwd);
        FileWatcher.EnableRaisingEvents = true;
        FileWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;

        if (Directory.GetParent(Cwd) != null)
        {
            ParentWatcher = new(Directory.GetParent(Cwd)?.FullName ?? "");
            ParentWatcher.EnableRaisingEvents = true;
            ParentWatcher.NotifyFilter = NotifyFilters.DirectoryName;
            
            string currentDir = Cwd;
            ParentWatcher.Deleted += (_, _) =>
            {
                bool currDirExists;
                try
                {
                    Directory.GetCurrentDirectory();
                    currDirExists = true;
                }
                catch (FileNotFoundException)
                {
                    Logger.LogI("Current directory was deleted");
                    currDirExists = false;
                }
            
                while (!currDirExists)
                {
                    DirectoryInfo? info = Directory.GetParent(currentDir);
                    if (info != null)
                    {
                        currentDir = info.FullName;
                        currDirExists = true;
                    }
                    else
                    {
                        currentDir = Regex.Replace(currentDir, "[\\/][^\\/]+$", "");
                    }
                }
                
                Logger.LogI("New directory found");
                OnClickDir(new(currentDir), false);
            };
        }
        
        FileWatcher.Deleted += (_, e) =>
        {
            CmdListBoxItem<CmdLabel>? item = Menu.Items.FirstOrDefault(item => item.Item.Text == e.Name);
            if (item != null)
            {
                Menu.RemoveItem(item);
                
                if (item.Data.TryGetValue("ItemType", out string? itemType))
                {
                    switch (itemType)
                    {
                        case "Folder":
                            Logger.LogI("A folder was deleted");
                        break;
                        
                        case "File":
                            Logger.LogI("A file was deleted");
                        break;
                    }
                };
            }
            
            RedrawMenu();
        };

        FileWatcher.Renamed += (_, e) =>
        {
            CmdListBoxItem<CmdLabel>? item = Menu.Items.FirstOrDefault(item => item.Item.Text == e.OldName);
            if (item != null)
            {
                item.Item.Text = e.Name ?? item.Item.Text;
                RedrawMenu();
            }
        };

        _cwdTimer?.Dispose();
        _cwdTimer = new(_ =>
        {
            if (!Directory.Exists(Cwd))
            {
                Logger.LogI("Current directory was deleted");
                
                string path = FindExistingTopFolder();
                Directory.SetCurrentDirectory(path);
                
                Logger.LogI("Found new directory");
                OnClickDir(new(path), false);   
            }
        }, null, 0L, 300L);

        RefreshItems();
        
        Menu.ViewIndex = 0;
        Menu.SelectedIndex = 0;
    }

    public string FindExistingTopFolder()
    {
        if (!Directory.Exists(Cwd))
        {
            string path = Cwd;
            List<string> parts = path.Split(Path.DirectorySeparatorChar).ToList();

            if (parts.Count > 0)
            {
                parts[0] = Path.DirectorySeparatorChar + parts[0];
            }
            else
            {
                Path.DirectorySeparatorChar.ToString();
            }
            
            do
            {
                parts.RemoveAt(parts.Count - 1);
                path = Path.Combine(parts.ToArray());
            } while (!Directory.Exists(path) && !string.IsNullOrWhiteSpace(path.TrimEnd(Path.DirectorySeparatorChar)));

            return path;
        }

        return Cwd;
    }
    
    public bool RequiresElevatedAccess(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return false;
            }

            if (Directory.Exists(path))
            {
                using IEnumerator<string> e = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
                e.MoveNext();
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            Logger.LogI("Directory access denied");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? Input(string inputText, string startValue = "", bool enterNull = false, bool escapeNo = false, bool inputHidden = false)
    {
        Logger.LogI("Reading input...");
        Listener.RaiseEvents = false;

        InputListener? keyListener = ForceTtyInput ? new TtyInputListener() : InputListener.New();
        if (keyListener == null)
        {
            Listener?.StartListening();
            return "";
        }
        
        Logger.LogI($"Created new input listener of type: {keyListener.GetType()}");
        
        Console.CursorVisible = true;
        
        keyListener.StartListening();
        StringBuilder builder = new(startValue);
        string? result = null;

        int cursor = builder.Length;
        void Redraw()
        {
            Console.CursorVisible = false;
            int top = Menu.MaxHeight + 4;

            Console.SetCursorPosition(0, top);
            Console.Write("\x1b[2K");

            Console.Write(inputText);
            if (!inputHidden)
            {
                Console.Write(builder);
            }
            else
            {
                Console.Write(new string('*', builder.Length));
            }
            
            Console.SetCursorPosition(cursor + StripAnsi(inputText).Length, top);
            Console.CursorVisible = true;
        }
        
        void OnKeyDown(Key key, KeyDownEventArgs e)
        {
            if (key == Key.Escape)
            {
                return;
            }
            
            if (key == Key.Enter)
            {
                result = enterNull && builder.Length == 0
                    ? null
                    : builder.ToString();

                keyListener.Dispose();
                return;
            }

            switch (key)
            {
                case Key.ArrowLeft:
                    if (cursor > 0)
                    {
                        if (keyListener.IsKeyDown(Key.LeftCtrl))
                        {
                            do
                            {
                                cursor--;
                            } while (cursor > 0 && builder[cursor] != ' ');
                        }
                        else
                        {
                            cursor--;
                        }
                        
                        Redraw();
                    }
                    
                    return;

                case Key.ArrowRight:
                    if (cursor < builder.Length)
                    {
                        if (keyListener.IsKeyDown(Key.LeftCtrl))
                        {
                            do
                            {
                                cursor++;
                            } while (cursor < builder.Length && builder[cursor] != ' ');
                        }
                        else
                        {
                            cursor++;
                        }
                        
                        Redraw();
                    }
                    
                    return;

                case Key.Backspace:
                    if (cursor > 0)
                    {
                        if (keyListener.IsKeyDown(Key.LeftCtrl))
                        {
                            char lastChar;
                            do
                            {
                                lastChar = builder[cursor - 1];
                                builder.Remove(cursor - 1, 1);
                                cursor--;
                            } while (cursor > 0 && lastChar != ' ');
                        }
                        else
                        {
                            builder.Remove(cursor - 1, 1);
                            cursor--;
                        }
                        
                        Redraw();
                    }
                    
                    return;
                
                case Key.Delete:
                    if (cursor == builder.Length)
                    {
                        return;
                    }
                    
                    if (keyListener.IsKeyDown(Key.LeftCtrl))
                    {
                        char lastChar;
                        do
                        {
                            lastChar = builder[cursor];
                            builder.Remove(cursor, 1);
                        } while (cursor < builder.Length && lastChar != ' ');
                    }
                    else
                    {
                        builder.Remove(cursor, 1);
                    }
                    
                    Redraw();
                    
                    return;
                
                case Key.V:
                    if (!keyListener.IsKeyDown(Key.LeftCtrl))
                    {
                        return;
                    }

                    string clip = Clipboard.Read();
                    builder.Insert(cursor, clip);
                    cursor += clip.Length;
                    
                    Redraw();
                    
                    return;
            }

            char c = keyListener.GetKeyChar(key);
            if (c != '\0')
            {
                builder.Insert(cursor, c);
                cursor++;
                Redraw();
            }
        }
        
        void OnKeyUp(Key key)
        {
            if (key == Key.Escape)
            {
                result = escapeNo ? "n" : null;
                keyListener.Dispose();
            }
        }
        
        keyListener.OnKeyDown += OnKeyDown;
        keyListener.OnKeyUp += OnKeyUp;

        Redraw();
        
        keyListener.WaitForDispose();
        keyListener.OnKeyDown -= OnKeyDown;
        keyListener.OnKeyUp -= OnKeyUp;
        
        Listener.RaiseEvents = true;
        
        Listener.ClearKeyState();
        Listener.ConsumeNextKeyDown(Key.Enter);
        
        Console.CursorVisible = false;
        Logger.LogI("Done reading input");
        
        return result;
    }
    
    public string StripAnsi(string input)
    {
        return AnsiRegex.Replace(input, "");
    }
    
    public void CopyDirectory(string sourceDir, string destinationDir)
    {
        DirectoryInfo dir = new(sourceDir);
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (DirectoryInfo subDir in dirs)
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
    
    public void SelectItem()
    {
        CmdLabel? item = Menu.SelectedItem.Item;
        if (item == null || item.Text == "..")
        {
            return;
        }
        
        if (!SelectedItems.Remove(Menu.SelectedItem))
        {
            Logger.LogI("Added item to selection");
            SelectedItems.Add(Menu.SelectedItem);
        }
        else
        {
            Logger.LogI("Removed item from selection");
        }
    }
}