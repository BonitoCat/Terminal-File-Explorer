using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Text;
using CmdMenu;
using InputLib;

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
    public InputListener? listener { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public Process? CommandLine { get; set; }
    public string? SearchString { get; set; }
    public Stack<string> DirHistory { get; set; } = new();
    public List<MenuItem> SelectedItems { get; } = new();
    public List<string> MoveItems { get; } = new();
    public MoveStyle? MoveStyle { get; set; } = FileExplorer.MoveStyle.None;
    public CancellationTokenSource RefreshCancelSource { get; set; } = new();
    public object OutLock { get; } = new();
    
    public void RefreshItems(bool cancelLast = true)
    {
        if (cancelLast)
        {
            RefreshCancelSource.Cancel();
            RefreshCancelSource = new();
        }
        
        if (RefreshCancelSource.Token.IsCancellationRequested)
        {
            return;
        }
        
        lock (Menu.Lock)
        {
            SelectedItems.Clear();
            Menu.ClearItems();
        }
     
        if (Directory.GetParent(Directory.GetCurrentDirectory()) != null)
        {
            MenuItem item = new("..", Color.White);
            item.Prefix = "   ";
            item.OnClick += () => OnClickDir(item);
            
            lock (Menu.Lock)
            {
                Menu.AddItem(item);
            }
        }

        List<string> dirNames = Directory.EnumerateDirectories(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly)
                                         .Select(Path.GetFileName).Select(file => file ?? "Missing Name").ToList();
        dirNames.Sort();
        
        foreach (string dir in dirNames)
        {
            if (SearchString != null && !dir.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            
            MenuItem item = new(dir, Color.FromRgbString(Blue));
            item.Prefix = $"{Color.FromRgbString(Blue).ToAnsi()}\x1b[1m🗁  \x1b[0m";
            
            if (MoveItems.Contains(Path.GetFullPath(item.Text)))
            {
                item.ForegroundColor = Color.FromRgbString(DarkBlue);
            }
            
            if (RequiresElevatedAccess(dir) && Environment.UserName != "root")
            {
                item.Suffix += $"{Color.Orange.ToAnsi()} (Access Denied)";
            }
            else
            {
                item.OnClick += () => OnClickDir(item);
            }
            
            if ((File.GetAttributes(dir) & FileAttributes.Hidden) != 0)
            {
                if (!ShowHiddenFiles)
                {
                    continue;
                }
                
                item.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
            }
            
            lock (Menu.Lock)
            {
                if (RefreshCancelSource.Token.IsCancellationRequested)
                {
                    return;
                }
                
                Menu.AddItem(item);
            }
        }
        
        List<string> fileNames = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly)
                                          .Select(Path.GetFileName).Select(file => file ?? "Missing Name").ToList();
        fileNames.Sort();
        
        foreach (string file in fileNames)
        {
            if (SearchString != null &&
                !file.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            
            MenuItem item = new(file);
            if (IsExecutable(file))
            {
                item.Prefix = $"{Color.FromRgbString(Green).ToAnsi()}\x1b[1mᐅ  \x1b[0m";
                item.ForegroundColor = Color.FromRgbString(Green);
                
                if (MoveItems.Contains(Path.GetFullPath(item.Text)))
                {
                    item.ForegroundColor = Color.FromRgbString(DarkGreen);
                }
                
                item.OnClick += () => OnClickExec(item);
            }
            else if (IsImage(file))
            {
                item.Prefix = $"{Color.Yellow.ToAnsi()}\x1b[1m🖼  \x1b[0m";
                item.ForegroundColor = Color.Yellow;
                
                if (MoveItems.Contains(Path.GetFullPath(item.Text)))
                {
                    item.ForegroundColor = Color.Yellow.Transform(-70, -90, -40);
                }
                
                item.OnClick += () => OnClickImage(item);
            }
            else if (IsVideo(file))
            {
                item.Prefix = $"{Color.Orange.ToAnsi()}\x1b[1m🎞  \x1b[0m";
                item.ForegroundColor = Color.Orange;
                
                if (MoveItems.Contains(Path.GetFullPath(item.Text)))
                {
                    item.ForegroundColor = Color.Orange.Transform(-40, -70, -50);
                }
                
                item.OnClick += () => OnClickVideo(item);
            }
            else if (IsAudio(file))
            {
                item.Prefix = $"{Color.FromRgbString(Red).ToAnsi()}\x1b[1m♪  \x1b[0m";
                item.ForegroundColor = Color.FromRgbString(Red);
                
                if (MoveItems.Contains(Path.GetFullPath(item.Text)))
                {
                    item.ForegroundColor = Color.FromRgbString(Red).Transform(-40, -40, -20);
                }
                
                item.OnClick += () => OnClickVideo(item);
            }
            else if (IsArchive(file))
            {
                item.Prefix = $"{Color.Orange.Transform(-50, -20, -20).ToAnsi()}\x1b[1m🗀  \x1b[0m";
                item.ForegroundColor = Color.Orange.Transform(-50, -20, -20);
                
                if (MoveItems.Contains(Path.GetFullPath(item.Text)))
                {
                    item.ForegroundColor = Color.Orange.Transform(-90, -90, -70);
                }
                
                item.OnClick += () => OnClickArchive(item);
            }
            else
            {
                item.Prefix = $"{Color.White.ToAnsi()}\x1b[1m🗏︎  \x1b[0m";
                if (MoveItems.Contains(Path.GetFullPath(item.Text)))
                {
                    item.ForegroundColor = Color.LightGray;
                }
                
                item.OnClick += () => OnClickFile(item);
            }
            
            if ((File.GetAttributes(file) & FileAttributes.Hidden) != 0)
            {
                if (!ShowHiddenFiles)
                {
                    continue;
                }
                
                item.Suffix += $"{Color.Gray.ToAnsi()} (Hidden)";
            }

            lock (Menu.Lock)
            {
                if (RefreshCancelSource.Token.IsCancellationRequested)
                {
                    return;
                }
                
                Menu.AddItem(item);
            }
        }

        lock (Menu.Lock)
        {
            if (Menu.GetItemCount() == 0 && SearchString != null)
            {
                SearchString = null;
                RefreshItems();
            }
            
            if (Menu.SelectedIndex >= Menu.GetItemCount())
            {
                Menu.SelectedIndex = Menu.GetItemCount() - 1;
                Menu.ViewIndex = Math.Max(Menu.GetItemCount() - Menu.ViewRange, 0);
            }
        }
    }
    
    public void OnClickDir(MenuItem sender, bool saveToHistory = true)
    {
        SearchString = null;
        lock (Menu.Lock)
        {
            Console.Clear();
            Menu.ClearItems();
        }

        if (saveToHistory)
        {
            DirHistory.Push(Path.GetFullPath(Directory.GetCurrentDirectory()));
        }
        
        Directory.SetCurrentDirectory(sender.Text);
        Task.Run(() =>
        {
            RefreshItems();
        });
        
        Thread.Sleep(5);
        if (Menu.SelectedIndex != 0 && Menu.GetItemCount() > 1)
        {
            Menu.SelectedIndex = 1;
        }
        else
        {
            Menu.SelectedIndex = 0;
        }

        Menu.ViewIndex = 0;
    }

    public void OnClickFile(MenuItem sender)
    {
        listener?.StopListening();
        
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
            }
        };

        lock (Menu.Lock)
        {
            proc.Start();
            proc.WaitForExit();

            Console.CursorVisible = false;
            Console.WriteLine($"\x1b[8;{lines};{columns}t");
        }
        
        listener?.StartListening();
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
                input = ReadLine()?.Trim();
            }
            
            Console.CursorVisible = false;
            if (input == "n")
            {
                Console.Clear();
                return;
            }
            
            ProcessStartInfo startInfo = new()
            {
                FileName = "tar",
            };
        
            if (sender.Text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = "unzip";
                startInfo.Arguments = $"-q \"{sender.Text}\"";
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
                listener?.StartListening();
            
                return;
            }
        
            Process? proc = Process.Start(startInfo);
            proc?.WaitForExit();   
        }
        
        InputListener.DisableEcho();
        listener?.StartListening();

        Console.Clear();
        Task.Run(() =>
        {
            RefreshItems();
        });
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
    
    public string? ReadLine()
    {
        listener?.StopListening();

        InputListener? keyListener = InputListener.New();
        if (keyListener == null)
        {
            listener?.StartListening();
            return "";
        }

        keyListener.StartListening();
        StringBuilder builder = new();
        string? result = null;

        void OnKeyDown(Key key, bool continuous)
        {
            if (key == Key.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                    Console.Write("\b \b");
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
            if (key == Key.Enter)
            {
                result = string.IsNullOrEmpty(builder.ToString()) ? null : builder.ToString();
                keyListener.StopListening();
            }
            else if (key == Key.Escape)
            {
                result = "n";
                keyListener.StopListening();
            }
        }
        
        keyListener.OnKeyDown += OnKeyDown;
        keyListener.OnKeyUp += OnKeyUp;

        keyListener.WaitForClose();
        listener?.StartListening();
        
        keyListener.OnKeyDown -= OnKeyDown;
        keyListener.OnKeyUp -= OnKeyUp;

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