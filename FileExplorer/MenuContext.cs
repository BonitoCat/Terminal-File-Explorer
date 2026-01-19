using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CmdMenu;
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
    
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff"];
    
    private static readonly string[] VideoExtensions =
    [
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".mpeg", ".mpg",
        ".m4v", ".webm", ".3gp", ".ts", ".mts", ".m2ts", ".f4v", ".ogv",
        ".rm", ".rv", ".vob", ".mxf", ".swf", ".drc", ".gif",
    ];
    
    private static readonly string[] AudioExtensions =
    [
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".oga", ".wma",
        ".m4a", ".alac", ".aiff", ".aif", ".opus", ".pcm", ".amr",
        ".mid", ".midi", ".caf", ".dsd",
    ];
    
    private static readonly string[] ArchiveExtensions = [".zip", ".tar", ".tar.gz", ".tgz", ".tar.bz2", ".tar.xz"];
    
    public Menu Menu { get; set; }
    public InputListener? Listener { get; set; }
    public FileSystemWatcher? FileWatcher { get; set; }
    public FileSystemWatcher? ParentWatcher { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool ShowFileSizes { get; set; }
    public Process? CommandLine { get; set; }
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

            _filesLoaded = 0;
            Menu.GetItems().ForEach(item => UpdateFileAttributes(item, RefreshCancelSource.Token));
            
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
            //UiDispatcher.ClearPending();
            RedrawMenu();
            
            _foldersLoaded = 0;
        }

        _foldersLoaded++;
    }

    public void UpdateFileAttributes(MenuItem file, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }
        
        if (!file.Data.TryGetValue("ItemType", out string? fileType) || fileType != "File")
        {
            return;
        }

        string fileName = file.Text;
        if (IsExecutable(fileName))
        {
            file.Prefix = $"{Color.FromRgbString(Green).ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.ForegroundColor = Color.FromRgbString(Green);

            file.Data.TryAdd("DefaultColor", Green);
            file.Data.TryAdd("CutColor", DarkGreen);
            
            file.Data.TryAdd("FileType", "Executable");
            file.OnClick += () => OnClickExec(file);
        }
        else if (IsImage(fileName))
        {
            file.Prefix = $"{Color.Yellow.ToAnsi()}\x1b[1m🖼  \x1b[0m";
            file.ForegroundColor = Color.Yellow;
            
            file.Data.TryAdd("DefaultColor", Color.Yellow.ToRgbString());
            file.Data.TryAdd("CutColor", Color.Yellow.Transform(-70, -90, -40).ToRgbString());
            
            file.Data.TryAdd("FileType", "Image");
            file.OnClick += () => OnClickImage(file);
        }
        else if (IsVideo(fileName))
        {
            file.Prefix = $"{Color.Orange.ToAnsi()}\x1b[1m🎞  \x1b[0m";
            file.ForegroundColor = Color.Orange;
            
            file.Data.TryAdd("DefaultColor", Color.Orange.ToAnsi());
            file.Data.TryAdd("CutColor", Color.Orange.Transform(-40, -70, -50).ToRgbString());
            
            file.Data.TryAdd("FileType", "Video");
            file.OnClick += () => OnClickVideo(file);
        }
        else if (IsAudio(fileName))
        {
            file.Prefix = $"{Color.FromRgbString(Red).ToAnsi()}\x1b[1m♪  \x1b[0m";
            file.ForegroundColor = Color.FromRgbString(Red);
            
            file.Data.TryAdd("DefaultColor", Color.Red.ToRgbString());
            file.Data.TryAdd("CutColor", Color.Red.Transform(-40, -40, -20).ToRgbString());
            
            file.Data.TryAdd("FileType", "Audio");
            file.OnClick += () => OnClickAudio(file);
        }
        else if (IsArchive(fileName))
        {
            file.Prefix = $"{Color.Orange.Transform(-50, -20, -20).ToAnsi()}\x1b[1m🗀  \x1b[0m";
            file.ForegroundColor = Color.Orange.Transform(-50, -20, -20);
            
            file.Data.TryAdd("DefaultColor", Color.Orange.Transform(-50, -20, -20).ToRgbString());
            file.Data.TryAdd("CutColor", Color.Orange.Transform(-90, -90, -70).ToRgbString());
            
            file.Data.TryAdd("FileType", "Archive");
            file.OnClick += () => OnClickArchive(file);
        }
        else
        {
            file.Data.TryAdd("DefaultColor", Color.White.ToRgbString());
            file.Data.TryAdd("CutColor", Color.LightGray.ToRgbString());
            
            file.OnClick += () => OnClickFile(file);
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
            //UiDispatcher.ClearPending();
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

        if (saveToHistory)
        {
            DirHistory.Push(Path.GetFullPath(Directory.GetCurrentDirectory()));
        }
        
        Directory.SetCurrentDirectory(sender.Text);
        FileWatcher?.Dispose();
        ParentWatcher?.Dispose();
        
        FileWatcher = new(Directory.GetCurrentDirectory());
        FileWatcher.EnableRaisingEvents = true;
        FileWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;

        if (Directory.GetParent(Directory.GetCurrentDirectory()) != null)
        {
            ParentWatcher = new(Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "");
            ParentWatcher.EnableRaisingEvents = true;
            ParentWatcher.NotifyFilter = NotifyFilters.DirectoryName;
            
            string currentDir = Directory.GetCurrentDirectory();
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
    }

    public void OnClickFile(MenuItem sender)
    {
        Listener.RaiseEvents = false;
        
        int lines = Console.WindowHeight;
        int columns = Console.WindowWidth;
        Console.WriteLine($"\x1b[8;{Math.Max(lines, 45)};{Math.Max(Console.WindowWidth, 80)}t");
        
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "nano",
                Arguments = sender.Text,
                UseShellExecute = false,
            },
        };

        lock (Menu.Lock)
        {
            proc.Start();
            proc.WaitForExit();

            Console.CursorVisible = false;
            Console.Clear();
            Console.WriteLine($"\x1b[8;{lines};{columns}t");
            RedrawMenu();
        }

        Listener.RaiseEvents = true;
    }

    public void OnClickImage(MenuItem sender)
    {
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "xdg-open",
                Arguments = $"\"{sender.Text}\"",
                UseShellExecute = false
            }
        };
        proc.Start();
    }

    public void OnClickVideo(MenuItem sender)
    {
        OnClickImage(sender);
    }

    public void OnClickAudio(MenuItem sender)
    {
        OnClickImage(sender);
    }

    public void OnClickExec(MenuItem sender)
    {
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "gnome-terminal",
                Arguments = $"-- bash -c '{Path.GetFullPath(sender.Text)}'; exec bash",
                UseShellExecute = false
            }
        };

        proc.Start();
        proc.WaitForExit();
    }

    public void OnClickArchive(MenuItem sender)
    {
        lock (OutLock)
        {
            Console.CursorVisible = true;
            string? input = "";
            
            while (input != null && input != "y" && input != "n")
            {
                Console.Write($"\x1b[2K\r{Color.Reset.ToAnsi()} Do you want to extract the content of '{sender.Text}'? [Y/n]: ");
                input = ReadLine(escapeNo: true)?.Trim();
            }
            
            Console.CursorVisible = false;
            if (input == "n")
            {
                Console.Clear();
                RedrawMenu();
                
                return;
            }
            
            ProcessStartInfo startInfo = new()
            {
                FileName = "tar",
            };
        
            if (sender.Text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = "unzip";
                startInfo.Arguments = $"-qn \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xf \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xzf \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xzf \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xjf \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xJf \"{sender.Text}\"";
            }
            else
            {
                InputListener.DisableEcho();
                Listener?.StartListening();
            
                return;
            }
        
            Process? proc = Process.Start(startInfo);
            proc?.WaitForExit();   
        }
        
        InputListener.DisableEcho();
        Listener?.StartListening();

        Console.Clear();
        RefreshItems();
    }

    public bool IsExecutable(string path)
    {
        Process process = new()
        {
            StartInfo = new()
            {
                FileName = "bash",
                Arguments = $"-c \"[ -x \\\"{path}\\\" ]\"",
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        process.WaitForExit();
        
        return process.ExitCode == 0;
    }
    
    public bool IsImage(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path).ToLower());
    }
    
    public bool IsVideo(string path)
    {
        return VideoExtensions.Contains(Path.GetExtension(path).ToLower());
    }

    public bool IsAudio(string path)
    {
        return AudioExtensions.Contains(Path.GetExtension(path).ToLower());
    }

    public bool IsArchive(string path)
    {
        return ArchiveExtensions.Any(ext => path.ToLower().EndsWith(ext));
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

    public string? ReadLine(bool enterNull = false, bool escapeNo = false)
    {
        Listener.RaiseEvents = false;
        Thread.Sleep(100);

        InputListener? keyListener = InputListener.New();
        if (keyListener == null)
        {
            Listener?.StartListening();
            return "";
        }
        
        keyListener.StartListening();
        StringBuilder builder = new();
        string? result = null;

        void OnKeyDown(Key key, KeyDownEventArgs e)
        {
            if (key == Key.Enter)
            {
                if (enterNull)
                {
                    result = string.IsNullOrEmpty(builder.ToString()) ? null : builder.ToString();
                }
                else
                {
                    result = string.IsNullOrEmpty(builder.ToString()) ? "y" : builder.ToString();
                }
                
                keyListener.Dispose();
            }
            
            if (key == Key.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                    Console .Write("\b \b");
                }
            }
            else
            {
                char c = keyListener.GetKeyChar(key);
                if (c != '\0')
                {
                    builder.Append(c);
                    Console.Write(c);
                }
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

        keyListener.WaitForDispose();
        keyListener.OnKeyDown -= OnKeyDown;
        keyListener.OnKeyUp -= OnKeyUp;
        
        Listener.RaiseEvents = true;
        Listener.ClearKeyState();
        
        Listener.ConsumeNextKeyDown(Key.Enter);
        Listener.ConsumeNextKeyUp(Key.Enter);

        return result;
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