using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CmdMenu;
using FileExplorer.FileTypes;
using InputLib;
using InputLib.EventArgs;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;
using SearchOption = System.IO.SearchOption;

namespace FileExplorer;

public class MenuContext
{
    public const string Red = "200;80;80";
    public const string Green = "130;250;50";
    public const string DarkGreen = "60;160;10";
    private const string Blue = "132;180;250";
    public const string DarkBlue = "60;100;200";
    
    public required Menu Menu { get; set; }
    public InputListener? Listener { get; set; }
    public FileSystemWatcher? FileWatcher { get; set; }
    public FileSystemWatcher? ParentWatcher { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool ShowFileSizes { get; set; }
    public Process? CommandLine { get; set; }
    public string BookmarkDir { get; set; } = "";
    public string? SearchString { get; set; }
    public Stack<string> DirHistory { get; set; } = new();
    public List<MenuItem> SelectedItems { get; } = new();
    public List<string> MoveItems { get; } = new();
    public MoveStyle? MoveStyle { get; set; } = FileExplorer.MoveStyle.None;
    public CancellationTokenSource RefreshCancelSource { get; set; } = new();
    public object OutLock { get; } = new();
    //public bool IsDrawing { get; set; }
    public bool RedrawRequested { get; set; }
    public readonly ManualResetEventSlim ExitEvent = new();

    private int _foldersLoaded;
    private int _filesLoaded;

    private static readonly Regex AnsiRegex =
        new(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
            RegexOptions.Compiled);
    
    public void RefreshItems()
    {
        SelectedItems.Clear();
        Menu.ClearItems();

        RefreshCancelSource.Cancel();
        Task.Delay(50).Wait();
        RefreshCancelSource = new();
        
        Task.Run(() =>
        {
            string cwd = Directory.GetCurrentDirectory();
            if (Directory.GetParent(cwd) != null)
            {
                MenuItem item = new("..", Color.White)
                {
                    Prefix = "   ",
                    Data =
                    {
                        {"ItemType", "Folder"},
                    },
                };
                item.OnClick += () => OnClickDir(item);
                
                Menu.AddItem(item);
            }

            NaturalStringComparer naturalComparer = new();
            
            List<string> dirPaths = Directory.EnumerateDirectories(cwd, "*", SearchOption.TopDirectoryOnly).ToList();
            dirPaths.Sort();

            Menu.AddItemRange(
                dirPaths
                    .Select(dirPath =>
                    {
                        return new MenuItem(Path.GetFileName(dirPath), Color.FromRgbString(Blue))
                        {
                            Prefix = $"{Color.FromRgbString(Blue).ToAnsi()}\x1b[1m🗁  \x1b[0m",
                            Data =
                            {
                                {"ItemType", "Folder"},
                                {"FullPath", dirPath},
                                {"DefaultColor", Blue},
                                {"CutColor", DarkBlue},
                            },
                        };
                    })
                    .Where(item => ShowHiddenFiles || !new DirectoryInfo(item.Text).Attributes.HasFlag(FileAttributes.Hidden))
                    .Where(item => SearchString == null ||
                                   (SearchString != null && item.Text.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase)))
                    .OrderBy(item => item.Text.StartsWith('.'))
                    .ThenBy(item => item.Text, naturalComparer)
                    .ToList());

            if (RefreshCancelSource.Token.IsCancellationRequested)
            {
                return;
            }
            
            RedrawMenu();

            _foldersLoaded = 0;
            Menu.GetItems().ForEach(item => UpdateFolderAttributes(item, RefreshCancelSource.Token));
            
            if (RefreshCancelSource.Token.IsCancellationRequested)
            {
                return;
            }
            
            RedrawMenu();
            
            List<string> fileNames = Directory.EnumerateFiles(cwd, "*", SearchOption.TopDirectoryOnly).ToList();
            fileNames.Sort();

            Menu.AddItemRange(
                fileNames
                    .Select(filePath =>
                    {
                        return new MenuItem(Path.GetFileName(filePath))
                        {
                            Prefix = $"{Color.White.ToAnsi()}\x1b[1m🗏︎  \x1b[0m",
                            Data =
                            {
                                {"ItemType", "File"},
                                {"FullPath", filePath},
                            },
                        };
                    })
                    .Where(item => ShowHiddenFiles || !File.GetAttributes(item.Text).HasFlag(FileAttributes.Hidden))
                    .Where(item => SearchString == null ||
                                   (SearchString != null && item.Text.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase)))
                    .OrderBy(item => item.Text.StartsWith('.'))
                    .ThenBy(item => item.Text, naturalComparer)
                    .ToList());
            
            if (RefreshCancelSource.Token.IsCancellationRequested)
            {
                return;
            }

            RedrawMenu();
            
            _filesLoaded = 0;
            Parallel.ForEach(Menu.GetItems(), item =>
            {
                string? fullPath = item.Data.GetValueOrDefault("FullPath");
                if (fullPath != null)
                {
                    string? mime = MimeHelper.GetMimeType(fullPath);
                    lock (item)
                    {
                        UpdateFileAttributes(item, mime, RefreshCancelSource.Token);
                    }
                }
            });
            
            if (RefreshCancelSource.Token.IsCancellationRequested)
            {
                return;
            }
            
            if (Menu.GetItemCount() == 0 && SearchString != null)
            {
                SearchString = null;
                RefreshItems();
            }
            
            if (Menu.SelectedIndex >= Menu.GetItemCount() && !RefreshCancelSource.Token.IsCancellationRequested)
            {
                Menu.SelectedIndex = Menu.GetItemCount() - 1;
                Menu.ViewIndex = Math.Max(Menu.GetItemCount() - Menu.ViewRange, 0);
            }
            
            if (RefreshCancelSource.Token.IsCancellationRequested)
            {
                return;
            }
            
            RedrawMenu();
        }, RefreshCancelSource.Token);
    }
    
    public void UpdateFolderAttributes(MenuItem dir, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }
        
        if (!dir.Data.TryGetValue("ItemType", out string? fileType) || fileType != "Folder")
        {
            return;
        }
        
        string dirName = dir.Text;
        if (dirName == "..")
        {
            return;
        }
        
        if (MoveItems.Contains(Path.GetFullPath(dir.Text)))
        {
            dir.ForegroundColor = Color.FromRgbString(DarkBlue);
        }
        
        if (RequiresElevatedAccess(dirName) && Environment.UserName != "root")
        {
            dir.Suffix += $"{Color.Orange.ToAnsi()} (Access Denied)";
        }
        else
        {
            dir.OnClick += () => OnClickDir(dir);
        }
        
        FileAttributes attributes = new DirectoryInfo(dirName).Attributes;
        if (attributes.HasFlag(FileAttributes.Hidden))
        {
            dir.Data.TryAdd("InfoHidden", "Hidden");
            dir.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
        }

        if (_foldersLoaded > 30)
        {
            RedrawMenu();
            
            _foldersLoaded = 0;
        }

        _foldersLoaded++;
    }

    public void UpdateFileAttributes(MenuItem file, string? mime, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }
        
        if (!file.Data.TryGetValue("ItemType", out string? fileType) || fileType != "File")
        {
            return;
        }

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
                    Arguments = $"-c \"xdg-open '{file.Text}' >/dev/null 2>&1\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            
            proc.Start();
        }
        
        string fileName = file.Text;
        if (mime == null)
        {
            file.Data.TryAdd("DefaultColor", Color.White.ToRgbString());
            file.Data.TryAdd("CutColor", Color.LightGray.ToRgbString());
            
            file.OnClick += () => TextFile.OnClick(this, file);
        }
        else if (mime.StartsWith("text/"))
        {
            file.Data.TryAdd("DefaultColor", Color.White.ToRgbString());
            file.Data.TryAdd("CutColor", Color.LightGray.ToRgbString());
            
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("image/"))
        {
            file.Prefix = $"{Color.Yellow.ToAnsi()}\x1b[1m🖼  \x1b[0m";
            file.ForegroundColor = Color.Yellow;
            
            file.Data.TryAdd("DefaultColor", Color.Yellow.ToRgbString());
            file.Data.TryAdd("CutColor", Color.Yellow.Transform(-70, -90, -40).ToRgbString());
            
            file.Data.TryAdd("FileType", "Image");
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("video/"))
        {
            file.Prefix = $"{Color.Orange.ToAnsi()}\x1b[1m🎞  \x1b[0m";
            file.ForegroundColor = Color.Orange;
            
            file.Data.TryAdd("DefaultColor", Color.Orange.ToAnsi());
            file.Data.TryAdd("CutColor", Color.Orange.Transform(-40, -70, -50).ToRgbString());
            
            file.Data.TryAdd("FileType", "Video");
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("audio/"))
        {
            file.Prefix = $"{Color.FromRgbString(Red).ToAnsi()}\x1b[1m♪  \x1b[0m";
            file.ForegroundColor = Color.FromRgbString(Red);
            
            file.Data.TryAdd("DefaultColor", Color.Red.ToRgbString());
            file.Data.TryAdd("CutColor", Color.Red.Transform(-40, -40, -20).ToRgbString());
            
            file.Data.TryAdd("FileType", "Audio");
            file.OnClick += XdgOpen;
        }
        else if (mime == "application/x-executable")
        {
            file.Prefix = $"{Color.FromRgbString(Green).ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.ForegroundColor = Color.FromRgbString(Green);
            
            file.Data.TryAdd("DefaultColor", Green);
            file.Data.TryAdd("CutColor", DarkGreen);
            
            file.Data.TryAdd("FileType", "Executable");
            file.OnClick += () => ExecutableFile.OnClick(this, file);
        }
        else
        {
            file.Data.TryAdd("DefaultColor", Color.White.ToRgbString());
            file.Data.TryAdd("CutColor", Color.LightGray.ToRgbString());
            
            file.OnClick += XdgOpen;
        }
        
        FileAttributes attributes = File.GetAttributes(fileName);
        if (attributes.HasFlag(FileAttributes.Hidden))
        {
            file.Data.TryAdd("InfoHidden", "Hidden");
            file.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
        }

        FileInfo info = new(fileName);
        file.Data.TryAdd("InfoSize", info.Length.ToString());
        
        if (_filesLoaded > 30)
        {
            RedrawMenu();
            
            _filesLoaded = 0;
        }

        _filesLoaded++;
    }

    public void RedrawMenu()
    {
        //UiDispatcher.Post(() => Menu.MenuUpdate.Invoke());
        /*if (IsDrawing)
        {
            return;
        }*/
        
        Task.Run(() =>
        {
            Menu.MenuUpdate.Invoke();
        });
        
        //RedrawRequested = true;
    }
    
    public void OnClickDir(MenuItem sender, bool saveToHistory = true)
    {
        SearchString = null;
        lock (Menu.Lock)
        {
            Console.Clear();
        }

        string cwd = Directory.GetCurrentDirectory();
        if (saveToHistory && cwd != BookmarkDir)
        {
            DirHistory.Push(Path.GetFullPath(cwd));
        }
        
        Directory.SetCurrentDirectory(sender.Text);
        FileWatcher?.Dispose();
        ParentWatcher?.Dispose();
        
        FileWatcher = new(cwd);
        FileWatcher.EnableRaisingEvents = true;
        FileWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;

        if (Directory.GetParent(cwd) != null)
        {
            ParentWatcher = new(Directory.GetParent(cwd)?.FullName ?? "");
            ParentWatcher.EnableRaisingEvents = true;
            ParentWatcher.NotifyFilter = NotifyFilters.DirectoryName;
            
            string currentDir = cwd;
            ParentWatcher.Deleted += (obj, e) =>
            {
                bool currDirExists;
                try
                {
                    Directory.GetCurrentDirectory();
                    currDirExists = true;
                }
                catch (FileNotFoundException)
                {
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
            
                OnClickDir(new(currentDir), false);
            };
        }
        
        FileWatcher.Deleted += (obj, e) =>
        {
            MenuItem item = Menu.GetItemByText(e.Name);
            if (item != null)
            {
                Menu.RemoveItem(item);
            }
            
            RedrawMenu();
        };

        FileWatcher.Renamed += (obj, e) =>
        {
            MenuItem? item = Menu.GetItems().FirstOrDefault(item => item.Text == e.OldName);
            if (item != null)
            {
                item.Text = e.Name ?? item.Text;
                Menu.MenuUpdate.Invoke();
            }
        };
        
        RefreshItems();
        
        Menu.ViewIndex = 0;
        Menu.SelectedIndex = 0;
    }
    
    public bool RequiresElevatedAccess(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read);
            }
            else if (Directory.Exists(path))
            {
                Directory.GetFiles(path);
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string? Input(string inputText, string startValue = "", bool enterNull = false, bool escapeNo = false)
    {
        Listener.RaiseEvents = false;
        Thread.Sleep(100);

        InputListener? keyListener = InputListener.New();
        if (keyListener == null)
        {
            Listener?.StartListening();
            return "";
        }
        
        Console.CursorVisible = true;
        
        keyListener.StartListening();
        StringBuilder builder = new(startValue);
        string? result = null;

        int cursor = builder.Length;
        void Redraw()
        {
            int top = Console.CursorTop;

            Console.SetCursorPosition(0, top);
            Console.Write("\x1b[2K");
            Console.SetCursorPosition(0, top);

            Console.Write(inputText + builder);
            Console.SetCursorPosition(cursor + StripAnsi(inputText).Length, top);
        }
        
        void OnKeyDown(Key key, KeyDownEventArgs e)
        {
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
                        cursor--;
                        Redraw();
                    }
                    
                    return;

                case Key.ArrowRight:
                    if (cursor < builder.Length)
                    {
                        cursor++;
                        Redraw();
                    }
                    
                    return;

                case Key.Backspace:
                    if (cursor > 0)
                    {
                        builder.Remove(cursor - 1, 1);
                        cursor--;
                        Redraw();
                    }
                    
                    return;
                
                case Key.Delete:
                    if (cursor == builder.Length)
                    {
                        return;
                    }
                    else
                    {
                        builder.Remove(cursor, 1);
                        Redraw();
                    }
                    
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
        Listener.ConsumeNextKeyUp(Key.Enter);
        
        Console.CursorVisible = false;

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
        MenuItem? item = Menu.GetItemAt(Menu.SelectedIndex);
        if (item?.Text == "..") return;
                    
        if (!SelectedItems.Remove(item))
        {
            SelectedItems.Add(item);
        }
    }
}