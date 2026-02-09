using System.Diagnostics;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.FileTypes;
using InputLib;
using InputLib.EventArgs;
using SearchOption = System.IO.SearchOption;

namespace FileExplorer;

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
    public List<CmdLabel> SelectedItems { get; } = new();
    public List<string> MoveItems { get; } = new();
    public MoveStyle? MoveStyle { get; set; } = FileExplorer.MoveStyle.None;
    public CancellationTokenSource RefreshCancelSource { get; set; } = new();
    public bool CanDraw { get; private set; } = true;
    public string Cwd { get; set; } = "/";
    public object OutLock { get; } = new();
    public readonly ManualResetEventSlim ExitEvent = new();

    private int _foldersLoaded;
    private int _filesLoaded;
    private int _redrawPending;

    private static readonly Regex AnsiRegex =
        new(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
            RegexOptions.Compiled);
    
    public void RefreshItems()
    {
        SelectedItems.Clear();
        Menu.ClearItems();

        RefreshCancelSource.Cancel();
        RefreshCancelSource = new();
        
        Task.Run(() =>
        {
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
            
            List<string> dirPaths = Directory.EnumerateDirectories(cwd, "*", SearchOption.TopDirectoryOnly).ToList();
            dirPaths.Sort();

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
                                {"DefaultColor", Blue},
                                {"CutColor", DarkBlue},
                            },
                        };

                        return lbItem;
                    })
                    .Where(item => ShowHiddenFiles || !new DirectoryInfo(item.Item.Text).Attributes.HasFlag(FileAttributes.Hidden))
                    .Where(item => SearchString == null ||
                                   (SearchString != null && item.Item.Text.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase)))
                    .OrderBy(item => item.Item.Text.StartsWith('.'))
                    .ThenBy(item => item.Item.Text, naturalComparer)
                    .ToList());

            if (RefreshCancelSource.Token.IsCancellationRequested)
            {
                return;
            }
            
            RedrawMenu();

            _foldersLoaded = 0;
            Menu.Items.ToList().ForEach(item => UpdateFolderAttributes(item, RefreshCancelSource.Token));
            
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
            
            if (RefreshCancelSource.Token.IsCancellationRequested)
            {
                return;
            }

            RedrawMenu();
            
            _filesLoaded = 0;
            Task task = Parallel.ForEachAsync(Menu.Items, async (item, token) =>
            {
                if (RefreshCancelSource.Token.IsCancellationRequested)
                {
                    return;
                }
                
                if (!item.Data.TryGetValue("FullPath", out string? fullPath))
                {
                    return;
                }

                string? mime = MimeHelper.GetMimeType(fullPath);
                UpdateFileAttributes(item, mime, RefreshCancelSource.Token);

                if (_filesLoaded % 15 == 0)
                {
                    RedrawMenu();
                    await Task.Yield();
                }
            });

            Task.Run(() =>
            {
                task.Wait(RefreshCancelSource.Token);
                RedrawMenu();
            });
            
            /*Task.Run(async () =>
            {
                foreach (CmdLabel item in Menu.Items)
                {
                    if (RefreshCancelSource.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!item.Data.TryGetValue("FullPath", out string? fullPath))
                    {
                        continue;
                    }

                    string? mime = MimeHelper.GetMimeType(fullPath);
                    UpdateFileAttributes(item, mime, RefreshCancelSource.Token);

                    if (_filesLoaded % 15 == 0)
                    {
                        RedrawMenu();
                        await Task.Yield();
                    }
                }

                RedrawMenu();
            });*/
            
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
        
        if (MoveItems.Contains(Path.GetFullPath(dir.Item.Text)))
        {
            dir.Item.Style.Foreground = Color.FromRgbString(DarkBlue);
        }
        
        if (RequiresElevatedAccess(dirName) && Environment.UserName != "root")
        {
            dir.Item.Suffix += $"{Color.Orange.ToAnsi()} (Access Denied)";
        }
        else
        {
            dir.OnClick += () => OnClickDir(dir.Item);
        }
        
        FileAttributes attributes = new DirectoryInfo(dirName).Attributes;
        if (attributes.HasFlag(FileAttributes.Hidden))
        {
            dir.Data.TryAdd("InfoHidden", "Hidden");
            dir.Item.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
        }

        if (_foldersLoaded > 30)
        {
            RedrawMenu();
            
            _foldersLoaded = 0;
        }

        _foldersLoaded++;
    }

    public void UpdateFileAttributes(CmdListBoxItem<CmdLabel> file, string? mime, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }
        
        if (!file.Data.TryGetValue("ItemType", out string? fileType) || fileType != "File")
        {
            return;
        }

        string fileName = file.Item.Text;
        if (mime == null)
        {
            file.Data.TryAdd("DefaultColor", Color.White.ToRgbString());
            file.Data.TryAdd("CutColor", Color.LightGray.ToRgbString());
            
            file.OnClick += () => TextFile.OnClick(this, file.Item);
        }
        else if (mime.StartsWith("text/"))
        {
            file.Data.TryAdd("DefaultColor", Color.White.ToRgbString());
            file.Data.TryAdd("CutColor", Color.LightGray.ToRgbString());
            
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("image/"))
        {
            file.Item.Prefix = $"{Color.Yellow.ToAnsi()}\x1b[1m🖼  \x1b[0m";
            file.Item.Style.Foreground = Color.Yellow;
            
            file.Data.TryAdd("DefaultColor", Color.Yellow.ToRgbString());
            file.Data.TryAdd("CutColor", Color.Yellow.Transform(-70, -90, -40).ToRgbString());
            
            file.Data.TryAdd("FileType", "Image");
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("video/"))
        {
            file.Item.Prefix = $"{Color.Orange.ToAnsi()}\x1b[1m🎞  \x1b[0m";
            file.Item.Style.Foreground = Color.Orange;
            
            file.Data.TryAdd("DefaultColor", Color.Orange.ToRgbString());
            file.Data.TryAdd("CutColor", Color.Orange.Transform(-40, -70, -50).ToRgbString());
            
            file.Data.TryAdd("FileType", "Video");
            file.OnClick += XdgOpen;
        }
        else if (mime.StartsWith("audio/"))
        {
            file.Item.Prefix = $"{Color.FromRgbString(Red).ToAnsi()}\x1b[1m♪  \x1b[0m";
            file.Item.Style.Foreground = Color.FromRgbString(Red);
            
            file.Data.TryAdd("DefaultColor", Color.Red.ToRgbString());
            file.Data.TryAdd("CutColor", Color.Red.Transform(-40, -40, -20).ToRgbString());
            
            file.Data.TryAdd("FileType", "Audio");
            file.OnClick += XdgOpen;
        }
        else if (mime == "application/vnd.debian.binary-package")
        {
            Color color = Color.Yellow.Transform(-20, -20, -20);
            file.Item.Prefix = $"{color.ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.Item.Style.Foreground = color;
            
            file.Data.TryAdd("DefaultColor", color.ToRgbString());
            file.Data.TryAdd("CutColor", color.Transform(-70, -90, -40).ToRgbString());
            
            file.Data.TryAdd("FileType", "Deb");
            file.OnClick += XdgOpen;
        }
        else if (ExecutableFile.IsExecutable(file.Item.Text))
        {
            file.Item.Prefix = $"{Color.FromRgbString(Green).ToAnsi()}\x1b[1mᐅ  \x1b[0m";
            file.Item.Style.Foreground = Color.FromRgbString(Green);
            
            file.Data.TryAdd("DefaultColor", Green);
            file.Data.TryAdd("CutColor", DarkGreen);
            
            file.Data.TryAdd("FileType", "Executable");
            file.OnClick += () => ExecutableFile.OnClick(this, file.Item);
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
            file.Item.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
        }

        FileInfo info = new(fileName);
        file.Data.TryAdd("InfoSize", info.Length.ToString());
        
        if (_filesLoaded > 30)
        {
            RedrawMenu();
            _filesLoaded = 0;
        }

        _filesLoaded++;
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

    public void RedrawMenu()
    {
        if (Interlocked.Exchange(ref _redrawPending, 1) == 1)
        {
            return;
        }

        Task.Run(() =>
        {
            Menu.MenuUpdate.Invoke();
            Interlocked.Exchange(ref _redrawPending, 0);
        });
    }

    public void DisableDrawing()
    {
        CanDraw = false;
    }

    public void EnableDrawing()
    {
        CanDraw = true;
    }
    
    public void OnClickDir(CmdLabel sender, bool saveToHistory = true)
    {
        SearchString = null;
        lock (Menu.Lock)
        {
            Console.Clear();
        }
        
        Cwd = Directory.GetCurrentDirectory();
        if (saveToHistory && Cwd != BookmarkDir)
        {
            DirHistory.Push(Path.GetFullPath(Cwd));
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
            CmdListBoxItem<CmdLabel>? item = Menu.Items.FirstOrDefault(item => item.Item.Text == e.Name);
            if (item != null)
            {
                Menu.RemoveItem(item);
            }
            
            RedrawMenu();
        };

        FileWatcher.Renamed += (obj, e) =>
        {
            CmdListBoxItem<CmdLabel>? item = Menu.Items.FirstOrDefault(item => item.Item.Text == e.OldName);
            if (item != null)
            {
                item.Item.Text = e.Name ?? item.Item.Text;
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

    public string? Input(string inputText, string startValue = "", bool enterNull = false, bool escapeNo = false, bool inputHidden = false)
    {
        Listener.RaiseEvents = false;

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
                    
                    builder.Remove(cursor, 1);
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
        
        if (!SelectedItems.Remove(item))
        {
            SelectedItems.Add(item);
        }
    }
}