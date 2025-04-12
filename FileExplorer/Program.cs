using System.Diagnostics;
using System.Text;

namespace FileExplorer;

class Program
{
    private static CancellationTokenSource refreshCancelSource = new();
    private static object menuLock = new();
    private static Menu menu;
    private static int menuViewIndex;
    private static int menuViewRange;

    private static Stack<string> dirHistory = new();
    private static Process? commandLine;
    
    public static void Main(string[] args)
    {
        string helpStr =
            $"""
            Command:
             {Process.GetCurrentProcess().ProcessName} [List of arguments]
             
            Arguments:
             -h | --help - Show this menu
             -o - Open current directory in file explorer
             
            Controls:
             Up- / Down Arrow - Navigate menu
             Enter | Space - Open selected directory
             Pos1 | Ctrl + Up Arrow - Go to top of menu
             End | Ctrl + Down Arrow - Go to bottom of menu
             Escape - Go back one directory
             F5 | Ctrl + R - Reload menu
             Ctrl + B - Return to last directory
             Ctrl + O - Open current directory in file explorer
             Ctrl + D - Switch between menu and command line
            
            """;
        
        for (int i = 0; i < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    Console.WriteLine(helpStr); 
                    return;
                
                case "-o":
                    Process.Start("nemo", Directory.GetCurrentDirectory());
                    return;
            }
        }
        
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.CursorVisible = true;
        };
        
        Console.CursorVisible = false;
        Console.Clear();
        
        menu = new();
        Task.Run(async () =>
        {
            await refreshCancelSource.CancelAsync();
            refreshCancelSource = new();

            var token = refreshCancelSource.Token;
            await RefreshMenuItems(token);
        });

        Thread inputThread = new(() =>
        {
            while (true)
            {
                HandleInput();
                Thread.Sleep(1);
            }
        });
        inputThread.Start();
        
        while (true)
        {
            if (commandLine != null)
            {
                Thread.Sleep(50);
                continue;
            }
            
            lock (menuLock)
            {
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"{AnsiColor.Reset} File-Explorer ({Directory.GetCurrentDirectory()})");

                string[] menuItems = menu.GetMenuString().Split("\n");
                StringBuilder builder = new();
            
                int consoleWidth = Console.WindowWidth;
                int consoleHeight = Console.WindowHeight;
                
                menuViewRange = consoleHeight - 5;
                if (menu.SelectedIndex > menuViewIndex + menuViewRange - 1)
                {
                    menuViewIndex = menu.SelectedIndex - menuViewRange + 1;
                }
                else if (menu.SelectedIndex < menuViewIndex)
                {
                    menuViewIndex = menu.SelectedIndex;
                }

                for (int i = menuViewIndex; i < Math.Min(menuViewIndex + menuViewRange, menu.GetItemCount()); i++)
                {
                    builder.Append(menuItems[i]);
                    builder.Append(new String(' ', consoleWidth - menuItems[i].Length));
                    builder.Append("\n");

                    if (i == Math.Min(menuViewIndex + menuViewRange, menu.GetItemCount()) - 1 && menuViewIndex + menuViewRange >= menu.GetItemCount())
                    {
                        builder.Append(new String(' ', consoleWidth) + "\n");
                        builder.Append(new String(' ', consoleWidth));
                    }
                }
            
                Console.WriteLine(builder);
                if (menuViewIndex + menuViewRange < menu.GetItemCount())
                {
                    Console.WriteLine($"{AnsiColor.Reset} --MORE--");
                }

                if (Console.WindowHeight != consoleHeight)
                {
                    if (Console.WindowHeight >= menuViewIndex + menuViewRange - 1)
                    {
                        menuViewIndex = 0;
                    }
                
                    Console.Clear();
                }
            }
            
            Thread.Sleep(10);
        }
    }

    private static async Task RefreshMenuItems(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        lock (menuLock)
        {
            menu.ClearItems();
        }
        
        Action onClickFile = OnClickFile;
        Action onClickExec = OnClickExec;
     
        if (Directory.GetParent(Directory.GetCurrentDirectory()) != null)
        {
            MenuItem item = new("..", AnsiColor.White);
            Action onClickDir = () => OnClickDir(item);
            item.OnClickListener = onClickDir;
            
            lock (menuLock)
            {
                menu.AddItem(item);
            }
        }

        IEnumerable<string?> dirPaths = Directory.EnumerateDirectories(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly);
        List<string?> dirNames = dirPaths.Select(Path.GetFileName).ToList();
        dirNames.Sort((a, b) => string.Compare(a, b));
        
        foreach (string? dir in dirNames)
        {
            if (dir == null) continue;
            
            MenuItem item = new(dir, AnsiColor.Blue);
            Action onClickDir = () => OnClickDir(item);
            item.OnClickListener = onClickDir;
            
            if (RequiresElevatedAccess(dir) && Environment.UserName != "root")
            {
                item.Suffix += $"{AnsiColor.Orange} (Access Denied)";
                item.OnClickListener = null;
            }
            
            if ((File.GetAttributes(dir) & FileAttributes.Hidden) != 0)
            {
                item.Suffix += $"{AnsiColor.Gray} (Hidden)";
            }
            
            lock (menuLock)
            {
                if (token.IsCancellationRequested) return;
                menu.AddItem(item);
            }
        }
        
        IEnumerable<string?> filePaths = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly);
        List<string?> fileNames = filePaths.Select(Path.GetFileName).ToList();
        fileNames.Sort((a, b) => string.Compare(a, b));
        
        foreach (string? file in fileNames)
        {
            if (file == null) continue;
            
            MenuItem item = new(file, onClickListener: onClickFile);
            if (IsExecutable(file))
            {
                item.Color = AnsiColor.Green;
                item.OnClickListener = onClickExec;
            }

            lock (menuLock)
            {
                if (token.IsCancellationRequested) return;
                menu.AddItem(item);
            }
        }

        lock (menuLock)
        {
            if (menu.SelectedIndex >= menu.GetItemCount())
            {
                menu.SelectedIndex = menu.GetItemCount() - 1;
            }
        }
    }
    
    private static void OnClickDir(MenuItem sender, bool saveToHistory = true)
    {
        lock (menuLock)
        {
            Console.Clear();
            menu.ClearItems();
        }

        if (saveToHistory)
        {
            dirHistory.Push(Path.GetFullPath(Directory.GetCurrentDirectory()));
        }
        
        Directory.SetCurrentDirectory(sender.Text);
        Task.Run(async () =>
        {
            await refreshCancelSource.CancelAsync();
            refreshCancelSource = new();

            var token = refreshCancelSource.Token;
            await RefreshMenuItems(token);
        });
        
        Thread.Sleep(5);
        if (menu.SelectedIndex != 0 && menu.GetItemCount() > 1)
        {
            menu.SelectedIndex = 1;
        }
        else
        {
            menu.SelectedIndex = 0;
        }

        menuViewIndex = 0;
    }

    private static void OnClickFile()
    {
        
    }

    private static void OnClickExec()
    {
        
    }
    
    private static bool IsExecutable(string path)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
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

    private static void HandleInput()
    {
        ConsoleKeyInfo keyInfo = Console.ReadKey(true);
        if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.D: OpenCommandLine(); break;
                case ConsoleKey.O:
                    Process proc = new();
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "nemo",
                        Arguments = Directory.GetCurrentDirectory(),
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true
                    };
            
                    proc.Start();
                break;
                
                case ConsoleKey.B:
                    if (dirHistory.Count > 0)
                    {
                        OnClickDir(new(dirHistory.Pop()), false);
                    }
                break;
                
                case ConsoleKey.UpArrow:
                    lock (menuLock)
                    {
                        menu.SelectedIndex = 0;
                        menuViewIndex = 0;
                    }

                break;
                
                case ConsoleKey.DownArrow:
                    lock (menuLock)
                    {
                        menu.SelectedIndex = menu.GetItemCount() - 1;
                        menuViewIndex = menu.GetItemCount() - menuViewRange;
                    }
                break;
                
                case ConsoleKey.R:
                    Task.Run(async () =>
                    {
                        await refreshCancelSource.CancelAsync();
                        refreshCancelSource = new();

                        var token = refreshCancelSource.Token;
                        await RefreshMenuItems(token);
                    });
                break;
            }
        }
        else
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow: menu.MoveSelected(-1); break;
                case ConsoleKey.DownArrow: menu.MoveSelected(1); break;
                
                case ConsoleKey.Spacebar:
                case ConsoleKey.Enter:
                    menu.CallSelectedItemClick();
                break;
                
                case ConsoleKey.Escape:
                    menu.SelectedIndex = 0;
                    OnClickDir(new MenuItem(".."));
                break;
                
                case ConsoleKey.Home:
                    lock (menuLock)
                    {
                        menu.SelectedIndex = 0;
                        menuViewIndex = 0;
                    }
                break;
                
                case ConsoleKey.End:
                    lock (menuLock)
                    {
                        menu.SelectedIndex = menu.GetItemCount() - 1;
                        menuViewIndex = menu.GetItemCount() - menuViewRange;
                    }
                break;
                
                case ConsoleKey.F5:
                    Task.Run(async () =>
                    {
                        await refreshCancelSource.CancelAsync();
                        refreshCancelSource = new();

                        var token = refreshCancelSource.Token;
                        await RefreshMenuItems(token);
                    });
                break;
            }
        }
    }
    
    private static void OpenCommandLine()
    {
        Console.CursorVisible = true;
        
        string shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
        commandLine = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            }
        };
        
        ConsoleCancelEventHandler? handler = (sender, e) =>
        {
            if (commandLine != null && !commandLine.HasExited)
            {
                commandLine.Kill();
            }
        };

        Console.CancelKeyPress += handler;
        
        commandLine.Start();
        commandLine.WaitForExit();
        commandLine = null;
        
        Console.CancelKeyPress -= handler;
        Console.CursorVisible = false;
        Console.Clear();
        
        Task.Run(async () =>
        {
            await refreshCancelSource.CancelAsync();
            refreshCancelSource = new();

            var token = refreshCancelSource.Token;
            await RefreshMenuItems(token);
        });
    }
    
    private static bool RequiresElevatedAccess(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
            }
            else if (Directory.Exists(path))
            {
                Directory.GetFiles(path);
            }
            else
            {
                return false;
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
}