using System.Diagnostics;
using System.Text;
using InputLib;

namespace FileExplorer;

class Program
{
    private static string _helpStr =
        $"""
         
          Command:
           {Process.GetCurrentProcess().ProcessName} [List of arguments]
 
          Arguments:
           -h | --help - Show this menu
           -o | --open - Open current directory in nemo
 
          Controls:
           Navigation:
           | Up- / Down Arrow - Navigate items
           | (WIP) Left- / Right Arrow - Navigate between menus
           | Enter - Open selected directory / file
           | Escape / Alt + Up Arrow - Go up one directory
           | Escape - Cancel current action
           | Ctrl + B / Alt + Left Arrow - Return to previous directory
           | Ctrl + W - Go to specific directory by path
           | Pos1 | Ctrl + Up Arrow - Go to fist item of menu
           | End | Ctrl + Down Arrow - Go to last item of menu
           | Ctrl + D - Switch between menu and command line
           
           Editing:
           | F2 - Rename selected item
           | Delete - Move item to recycle bin
           | Shift + Delete - Permanently delete item
           | Space - Select item
           | Shift + Space - Select a region of items
           | Ctrl + A - Select all directories and files
           | Ctrl + C - Copy item
           | Ctrl + X - Cut item
           | Ctrl + V - Paste item
           | Ctrl + N - Create new directory
           | Ctrl + Shift + N - Create new file
           | Shift + D - Duplicate directory / file
           
           Misc:
           | (WIP) F3 - Open / close second menu
           | F5 | Ctrl + R - Reload menu
           | Ctrl + F - Search in current directory
           | Ctrl + H - Toggle visibility of hidden files / directories
           | Ctrl + O - Open current directory in file explorer
           | F1 - Show this menu
           | F10 - Close file explorer (Also see Ctrl + D)
 
         """;
    
    private static CancellationTokenSource _refreshCancelSource = new();
    private static readonly object MenuLock = new();
    private static readonly Menu Menu = new();
    private static int _menuViewIndex;
    private static int _menuViewRange;
    private static int _menuScrollOvershoot = 2;
    private static bool _showHiddenFiles;

    private static string? _search;
    private static Stack<string> _dirHistory = new();
    private static Process? _commandLine;
    
    private static List<MenuItem> _selectedItems = new();
    
    private static List<string> _moveItems = new();
    private static MoveStyle? _moveStyle = MoveStyle.None;
    
    public static void Main(string[] args)
    {
        for (int i = 0; i < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    Console.WriteLine(_helpStr); 
                    return;
                
                case "--open":
                case "-o":
                    Process.Start("nemo", Directory.GetCurrentDirectory());
                    return;
            }
        }

        Console.TreatControlCAsInput = true;
        Console.CursorVisible = false;
        Console.Clear();
        
        Task.Run(() =>
        {
            _refreshCancelSource.Cancel();
            _refreshCancelSource = new();

            CancellationToken token = _refreshCancelSource.Token;
            RefreshMenuItems(token);
        });
        
        InputListener.CloseConsoleInput();
        InputListener? listener = InputListener.New();
        if (listener == null)
        {
            Console.WriteLine("Could not load input listener.");
            return;
        }

        listener.RepeatRateMs = 30;
        listener.StartListening();
        listener.OnKeyDown += (key, continuous) => HandleInput(listener, key, continuous);
        
        while (true)
        {
            if (_commandLine != null)
            {
                Thread.Sleep(50);
                continue;
            }
            
            int consoleWidth = Console.WindowWidth;
            int consoleHeight = Console.WindowHeight;
            
            lock (MenuLock)
            {
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"{AnsiColorF.Reset} File-Explorer ({Directory.GetCurrentDirectory()})");
                
                _menuViewRange = consoleHeight - 6;
                if (Menu.SelectedIndex - _menuScrollOvershoot < _menuViewIndex)
                {
                    _menuViewIndex = Math.Max(Menu.SelectedIndex - _menuScrollOvershoot, 0);
                }
                else if (Menu.SelectedIndex + _menuScrollOvershoot + 1 > _menuViewIndex + _menuViewRange)
                {
                    _menuViewIndex = Math.Min(Menu.SelectedIndex - _menuViewRange + 1 + _menuScrollOvershoot, Menu.GetItemCount() - _menuViewRange);
                }

                StringBuilder builder = new();
                for (int i = _menuViewIndex; i < Math.Min(_menuViewIndex + _menuViewRange, Menu.GetItemCount()); i++)
                {
                    builder.Append("\x1b[2K");
                    if (_selectedItems.Contains(Menu.GetItemAt(i)))
                    {
                        builder.Append(AnsiColorF.Reset);
                        builder.Append(i == Menu.SelectedIndex ? " > " : "   ");
                        
                        builder.Append(AnsiColorB.White);
                        builder.Append(AnsiColorF.Black);
                        builder.Append(Menu.GetItemAt(i).Text);
                        builder.Append(AnsiColorB.Reset);
                        builder.Append(AnsiColorF.Reset);
                    }
                    else
                    {
                        builder.Append(Menu.SelectedIndex == i ? AnsiColorF.Reset + " > " : "   ");
                        builder.Append(Menu.GetItemAt(i));
                    }
                    
                    builder.Append("\n");
                }
            
                Console.WriteLine(builder);
                if (_menuViewIndex + _menuViewRange < Menu.GetItemCount())
                {
                    Console.WriteLine($"{AnsiColorF.Reset} --MORE--");
                }
                else
                {
                    Console.Write("\x1b[2K");
                }
            }
            
            Thread.Sleep(10);
            if (Console.WindowHeight != consoleHeight || Console.WindowWidth != consoleWidth)
            {
                if (Console.WindowHeight >= _menuViewIndex + _menuViewRange - 1)
                {
                    _menuViewIndex = 0;
                }
                
                Console.Clear();
            }
        }
    }

    private static void RefreshMenuItems(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        lock (MenuLock)
        {
            _selectedItems.Clear();
            Menu.ClearItems();
        }
     
        if (Directory.GetParent(Directory.GetCurrentDirectory()) != null)
        {
            MenuItem item = new("..", AnsiColorF.White);
            Action onClickDir = () => OnClickDir(item);
            item.OnClickListener = onClickDir;
            
            lock (MenuLock)
            {
                Menu.AddItem(item);
            }
        }

        IEnumerable<string?> dirPaths = Directory.EnumerateDirectories(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly);
        List<string> dirNames = dirPaths.Select(Path.GetFileName).Select(file => file ?? "Missing Name").ToList();
        dirNames.Sort(string.Compare);
        
        foreach (string dir in dirNames)
        {
            if (_search != null && !dir.ToLower().Contains(_search.ToLower())) continue;
            
            MenuItem item = new(dir, AnsiColorF.Blue);
            if (_moveItems.Contains(Path.GetFullPath(item.Text)))
            {
                item.Color = AnsiColorF.DarkBlue;
            }
            
            Action onClickDir = () => OnClickDir(item);
            item.OnClickListener = onClickDir;
            
            if (RequiresElevatedAccess(dir) && Environment.UserName != "root")
            {
                item.Suffix += $"{AnsiColorF.Orange} (Access Denied)";
                item.OnClickListener = null;
            }
            
            if ((File.GetAttributes(dir) & FileAttributes.Hidden) != 0)
            {
                if (!_showHiddenFiles)
                {
                    continue;
                }
                
                item.Suffix += $"{AnsiColorF.Gray} (Hidden)";
            }
            
            lock (MenuLock)
            {
                if (token.IsCancellationRequested) return;
                Menu.AddItem(item);
            }
        }
        
        IEnumerable<string?> filePaths = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly);
        List<string> fileNames = filePaths.Select(Path.GetFileName).Select(file => file ?? "Missing Name").ToList();
        fileNames.Sort(string.Compare);
        
        foreach (string file in fileNames)
        {
            if (_search != null && !file.ToLower().Contains(_search.ToLower())) continue;
            
            MenuItem item = new(file);
            if (IsExecutable(file))
            {
                item.Color = AnsiColorF.Green;
                if (_moveItems.Contains(Path.GetFullPath(item.Text)))
                {
                    item.Color = AnsiColorF.DarkGreen;
                }
                
                item.OnClickListener = () => OnClickExec(item);
            }
            else
            {
                if (_moveItems.Contains(Path.GetFullPath(item.Text)))
                {
                    item.Color = AnsiColorF.LightGray;
                }
                
                item.OnClickListener = () => OnClickFile(item);
            }
            
            if ((File.GetAttributes(file) & FileAttributes.Hidden) != 0)
            {
                if (!_showHiddenFiles)
                {
                    continue;
                }
                
                item.Suffix += $"{AnsiColorF.Gray} (Hidden)";
            }

            lock (MenuLock)
            {
                if (token.IsCancellationRequested) return;
                Menu.AddItem(item);
            }
        }

        lock (MenuLock)
        {
            if (Menu.GetItemCount() == 0 && _search != null)
            {
                _search = null;
                RefreshMenuItems(token);
            }
            if (Menu.SelectedIndex >= Menu.GetItemCount())
            {
                Menu.SelectedIndex = Menu.GetItemCount() - 1;
                _menuViewIndex = Math.Max(Menu.GetItemCount() - _menuViewRange, 0);
            }

            Console.Clear();
        }
    }
    
    private static void OnClickDir(MenuItem sender, bool saveToHistory = true)
    {
        _search = null;
        lock (MenuLock)
        {
            Console.Clear();
            Menu.ClearItems();
        }

        if (saveToHistory)
        {
            _dirHistory.Push(Path.GetFullPath(Directory.GetCurrentDirectory()));
        }
        
        Directory.SetCurrentDirectory(sender.Text);
        Task.Run(() =>
        {
            _refreshCancelSource.Cancel();
            _refreshCancelSource = new();

            CancellationToken token = _refreshCancelSource.Token;
            RefreshMenuItems(token);
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

        _menuViewIndex = 0;
    }

    private static void OnClickFile(MenuItem sender)
    {
        int lines = Console.WindowHeight;
        int columns = Console.WindowWidth;
        Console.WriteLine($"\x1b[8;{Math.Max(lines, 45)};{Math.Max(Console.WindowWidth, 80)}t");
        
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "nano",
                Arguments = sender.Text,
                UseShellExecute = false
            }
        };

        lock (MenuLock)
        {
            proc.Start();
            proc.WaitForExit();

            Console.CursorVisible = false;
            Console.WriteLine($"\x1b[8;{lines};{columns}t");
        }
    }

    private static void OnClickExec(MenuItem sender)
    {
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "gnome-terminal",
                Arguments = $"-- bash -c '{Path.GetFullPath(sender.Text)}'; exec bash",
                UseShellExecute = false
            }
        };;

        proc.Start();
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

    private static void HandleInput(InputListener listener, Key key, bool continuous)
    {
        if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.D)
        {
            OpenCommandLine();
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.O)
        {
            Process proc = new();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "nemo",
                Arguments = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            
            proc.Start();
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.A)
        {  
            foreach (MenuItem item in Menu.GetItems())
            {
                if (item.Text == "..")
                {
                    continue;
                }
                        
                if (!_selectedItems.Contains(item))
                {
                    _selectedItems.Add(item);
                }
            }
        }
        else if ((listener.IsKeyDown(Key.LeftCtrl) && key == Key.B) || (listener.IsKeyDown(Key.Alt) && key == Key.ArrowLeft))
        {
            if (_dirHistory.Count > 0)
            {
                OnClickDir(new(_dirHistory.Pop()), false);
            }
        }
        else if ((listener.IsKeyDown(Key.LeftCtrl) && key == Key.ArrowUp) || key == Key.Home)
        {
            lock (MenuLock)
            {
                Menu.SelectedIndex = 0;
                _menuViewIndex = 0;
            }
        }
        else if ((listener.IsKeyDown(Key.LeftCtrl) && key == Key.ArrowDown) || key == Key.End)
        {
            lock (MenuLock)
            {
                Menu.SelectedIndex = Menu.GetItemCount() - 1;
                _menuViewIndex = Math.Max(Menu.GetItemCount() - _menuViewRange, 0);
            }
        }
        else if ((listener.IsKeyDown(Key.LeftCtrl) && key == Key.R) || key == Key.F5)
        {
            Task.Run(() =>
            {
                _refreshCancelSource.Cancel();
                _refreshCancelSource = new();

                CancellationToken token = _refreshCancelSource.Token;
                RefreshMenuItems(token);
            });
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.H)
        {
            lock (MenuLock)
            {
                _showHiddenFiles = !_showHiddenFiles;
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();
                            
                    Console.Clear();
                    Menu.ClearItems();
                            
                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                });
            }
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.F)
        {
            lock (MenuLock)
            {
                Console.CursorVisible = true;
                Console.Write($"{AnsiColorF.Reset} Search: ");
                string? search = ReadLine();
                
                Console.CursorVisible = false;
                if (search == null)
                {
                    Console.Clear();
                    return;
                }
                        
                _search = search;
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();

                    _menuViewIndex = 0;
                    Menu.SelectedIndex = 0;
                            
                    Console.Clear();
                    Menu.ClearItems();
                            
                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                });
            }
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && listener.IsKeyDown(Key.LeftShift) && key == Key.N)
        {
            lock (MenuLock)
            {
                Console.CursorVisible = true;
                Console.Write($"{AnsiColorF.Reset} File name: ");
                string? name = ReadLine();
                
                Console.CursorVisible = false;
                if (name == null)
                {
                    Console.Clear();
                    return;
                }

                try
                {
                    File.Create(name).Close();
                }
                catch (Exception)
                {
                    return;
                }
                
                Console.Clear();
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();

                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                    Menu.SelectedIndex = Menu.IndexOf(name);
                });
            }
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.N)
        {
            lock (MenuLock)
            {
                Console.CursorVisible = true;
                Console.Write($"{AnsiColorF.Reset} Directory name: ");
                string? name = ReadLine();
                            
                Console.CursorVisible = false;
                if (name == null)
                {
                    Console.Clear();
                    return;
                }
                            
                try
                {
                    Directory.CreateDirectory(name);
                }
                catch (Exception)
                {
                    return;
                }
                
                Console.Clear();
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();

                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                    Menu.SelectedIndex = Menu.IndexOf(name);
                });
            }
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.W)
        {
            lock (MenuLock)
            {
                Console.CursorVisible = true;
                Console.Write($"{AnsiColorF.Reset} Path of directory: ");
                string? input = ReadLine();

                if (input == null)
                {
                    Console.CursorVisible = false;
                    Console.Clear();

                    return;
                }

                if (Directory.Exists(input))
                {
                    _dirHistory.Push(Directory.GetCurrentDirectory());
                    Directory.SetCurrentDirectory(input);
                }

                Console.CursorVisible = false;
                Console.Clear();
                        
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();

                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                });
            }
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.C)
        {
            lock (MenuLock)
            {
                _moveItems.Clear();
                if (_selectedItems.Count > 0)
                {
                    _moveItems.AddRange(_selectedItems.Select(item => Path.GetFullPath(item.Text)));
                }
                else
                {
                    MenuItem? item = Menu.GetItemAt(Menu.SelectedIndex);
                    if (item?.Text == "..")
                    {
                        return;
                    }
                
                    _moveItems.Add(Path.GetFullPath(item.Text));
                }

                _moveStyle = MoveStyle.Copy;
            }
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.X)
        {
            lock (MenuLock)
            {
                _moveItems.Clear();
                if (_selectedItems.Count > 0)
                {
                    _moveItems.AddRange(_selectedItems.Select(item => Path.GetFullPath(item.Text)));
                }
                else
                {
                    MenuItem? item = Menu.GetItemAt(Menu.SelectedIndex);
                    if (item?.Text == "..")
                    {
                        return;
                    }
                        
                    _moveItems.Add(Path.GetFullPath(item.Text));
                }

                _moveStyle = MoveStyle.Cut;
            }
            
            RefreshMenuItems(new());
        }
        else if (listener.IsKeyDown(Key.LeftCtrl) && key == Key.V)
        {
            lock (MenuLock)
            {
                if (_moveStyle == MoveStyle.Cut)
                {
                    foreach (string itemPath in _moveItems)
                    {
                        try
                        {
                            string name = Path.GetFileName(itemPath);
                            if (Directory.Exists(itemPath))
                            {
                                Directory.Move(itemPath, name);
                                _selectedItems.Add(Menu.GetItemByName(name));
                            }
                            else if (File.Exists(itemPath))
                            {
                                File.Move(itemPath, name);
                                _selectedItems.Add(Menu.GetItemByName(name));
                            }
                        }
                        catch { }
                    }
                }
                else if (_moveStyle == MoveStyle.Copy)
                {
                    foreach (string itemPath in _moveItems)
                    {
                        try
                        {
                            if (Directory.Exists(itemPath))
                            {
                                CopyDirectory(itemPath, Path.GetFileName(itemPath));
                            }
                            else if (File.Exists(itemPath))
                            {
                                File.Copy(itemPath, Path.GetFileName(itemPath));
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    return;
                }
                
                _moveStyle = MoveStyle.None;
                Console.Clear();
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();

                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                });
            }
        }
        else if (listener.IsKeyDown(Key.LeftShift) && key == Key.D)
        {
            lock (MenuLock)
            {
                List<string> dupeItems = new();
                if (_selectedItems.Count > 0)
                {
                    dupeItems.AddRange(_selectedItems.Select(item => Path.GetFullPath(item.Text)));
                }
                else
                {
                    MenuItem? item = Menu.GetItemAt(Menu.SelectedIndex);
                    if (item?.Text == "..")
                    {
                        return;
                    }
                    
                    dupeItems.Add(Path.GetFullPath(item.Text));
                }

                foreach (string itemPath in dupeItems)
                {
                    int counter = 1;
                    if (Directory.Exists(itemPath))
                    {
                        string dirName = Path.GetFileName(itemPath);
                        string newName;

                        do
                        {
                            newName = $"{dirName}({counter++})";
                        } while (Directory.Exists(newName));
                        
                        CopyDirectory(itemPath, newName);
                    }
                    else if (File.Exists(itemPath))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(itemPath);
                        string fileExt = Path.GetExtension(itemPath);
                        string newName;
                        
                        do
                        {
                            newName = $"{fileName}({counter++}){fileExt}";
                        } while (File.Exists(newName));
                        
                        File.Copy(itemPath, newName);
                    }
                }
                
                Console.Clear();
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();

                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                });
            }
        }
        else if (listener.IsKeyDown(Key.LeftShift) && key == Key.Delete)
        {
            lock (MenuLock)
            {
                List<MenuItem> items = [];
                Console.CursorVisible = true;
                if (_selectedItems.Count > 1)
                {
                    items.AddRange(_selectedItems);
                    string? input;
                    
                    do
                    {
                        Console.Write($"{AnsiColorF.Reset} Are you sure you want to permanently delete {_selectedItems.Count} items? [y/n]: ");
                        input = ReadLine()?.Trim();
                        
                        if (input == null || input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                            
                            return;
                        }
                    } while (input != "y");
                }
                else
                {
                    MenuItem item = Menu.GetItemAt(Menu.SelectedIndex);
                    if (_selectedItems.Count == 1)
                    {
                        item = _selectedItems[0];
                    }

                    if (item.Text == "..")
                    {
                        Console.CursorVisible = false;
                        return;
                    }

                    items.Add(item);
                    string? input;
                    
                    do
                    {
                        Console.Write($"{AnsiColorF.Reset} Are you sure you want to permanently delete '{item.Text}'? [y/n]: ");
                        input = ReadLine()?.Trim();
                        
                        if (input == null || input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                            
                            return;
                        }
                    } while (input != "y");
                }

                foreach (string item in items.Select(item => item.Text))
                {
                    try
                    {
                        if (Directory.Exists(item))
                        {
                            Directory.Delete(item, true);
                            
                            List<string> tempHistory = new(_dirHistory);
                            tempHistory = tempHistory.Where(path => !path.Contains(Path.GetFullPath(item))).ToList();

                            _dirHistory = new(tempHistory);
                        }
                        else if (File.Exists(item))
                        {
                            File.Delete(item);
                        }
                    }
                    catch { }
                }
                
                Console.CursorVisible = false;
                Console.Clear();
                
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();

                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                });
            }
        }
        else if (key == Key.Delete)
        {
            lock (MenuLock)
            {
                List<MenuItem> items = [];
                Console.CursorVisible = true;
                if (_selectedItems.Count > 1)
                {
                    items.AddRange(_selectedItems);
                    string? input;
                    
                    do
                    {
                        Console.Write($"{AnsiColorF.Reset} Are you sure you want to move {_selectedItems.Count} items to the recycle bin? [y/n]: ");
                        input = ReadLine()?.Trim();
                        
                        if (input == null || input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                            
                            return;
                        }
                    } while (input != "y");
                }
                else
                {
                    MenuItem? item = Menu.GetItemAt(Menu.SelectedIndex);
                    if (_selectedItems.Count == 1)
                    {
                        item = _selectedItems[0];
                    }

                    if (item?.Text == "..")
                    {
                        Console.CursorVisible = false;
                        return;
                    }

                    items.Add(item);
                    string? input;
                    
                    do
                    {
                        Console.Write($"{AnsiColorF.Reset} Are you sure you want to move '{item.Text}' to the recycle bin? [y/n]: ");
                        input = ReadLine()?.Trim();
                        
                        if (input == null || input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                            
                            return;
                        }
                    } while (input != "y");
                }

                try
                {
                    foreach (string item in items.Select(item => item.Text))
                    {
                        if (Directory.Exists(item))
                        {
                            List<string> tempHistory = new(_dirHistory);
                            tempHistory = tempHistory.Where(path => !path.Contains(Path.GetFullPath(item))).ToList();

                            _dirHistory = new(tempHistory);
                        }

                        ProcessStartInfo startInfo = new()
                        {
                            FileName = "gio",
                            Arguments = $"trash \"{item}\"",
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        
                        Process proc = new();
                        proc.StartInfo = startInfo;
                        
                        proc.Start();
                        proc.WaitForExit();
                    }
                }
                catch { }
                
                Console.CursorVisible = false;
                Console.Clear();
                
                Task.Run(() =>
                {
                    _refreshCancelSource.Cancel();
                    _refreshCancelSource = new();

                    CancellationToken token = _refreshCancelSource.Token;
                    RefreshMenuItems(token);
                });
            }
        }
        else if (listener.IsKeyDown(Key.LeftShift) && key == Key.Space)
        {
            int lastSelectedIndex = Menu.IndexOf(_selectedItems.LastOrDefault());
            if (lastSelectedIndex == -1 || lastSelectedIndex == Menu.SelectedIndex)
            {
                SelectItem();
                return;
            }

            if (lastSelectedIndex < Menu.SelectedIndex)
            {
                for (int i = lastSelectedIndex + 1; i <= Menu.SelectedIndex; i++)
                {
                    MenuItem? item = Menu.GetItemAt(i);
                    if (item?.Text == "..") continue;
                        
                    if (!_selectedItems.Contains(item))
                    {
                        _selectedItems.Add(item);
                    }
                }
            }
            else
            {
                for (int i = lastSelectedIndex - 1; i >= Menu.SelectedIndex; i--)
                {
                    MenuItem? item = Menu.GetItemAt(i);
                    if (item?.Text == "..") continue;
                        
                    if (!_selectedItems.Contains(item))
                    {
                        _selectedItems.Add(item);
                    }
                }
            }
        }
        else if (key == Key.Space)
        {
            SelectItem();
        }
        else if (key == Key.ArrowUp)
        {
            if (Menu.SelectedIndex == 0 && continuous)
            {
                return;
            }
            
            Menu.MoveSelected(-1);
        }
        else if (key == Key.ArrowDown)
        {
            if (Menu.SelectedIndex == Menu.GetItemCount() - 1 && continuous)
            {
                return;
            }
            
            Menu.MoveSelected(1);
        }
        else if (key == Key.Enter)
        {
            Menu.CallSelectedItemClick();
        }
        else if (key == Key.Escape)
        {
            if (_search != null || _selectedItems.Count > 0)
            {
                if (_search != null)
                {
                    _search = null;
                        
                    Console.Clear();
                    Task.Run(() =>
                    {
                        _refreshCancelSource.Cancel();
                        _refreshCancelSource = new();

                        CancellationToken token = _refreshCancelSource.Token;
                        RefreshMenuItems(token);
                    });
                }
                if (_selectedItems.Count > 0)
                {
                    _selectedItems.Clear();
                }
            }
            else
            {
                if (Menu.GetItems().Where(item => item.Text == "..").ToList().Count == 0)
                {
                    return;
                }
                        
                Menu.SelectedIndex = 0;
                OnClickDir(new MenuItem(".."));
            }
        }
        else if (key == Key.F1)
        {
            lock (MenuLock)
            {
                Console.Clear();
                Console.WriteLine(_helpStr);
                Thread.Sleep(1000);
                
                listener.WaitForInput();
                Console.Clear();
            }
        }
        else if (key == Key.F2)
        {
            lock (MenuLock)
            {
                if (Menu.GetItemAt(Menu.SelectedIndex).Text == "..")
                {
                    return;
                }
                
                Console.CursorVisible = true;
                Console.Write($"{AnsiColorF.Reset} Rename to: ");

                string? name = ReadLine()?.Trim();
                if (name == null)
                {
                    Console.CursorVisible = false;
                    Console.Clear();
                    
                    return;
                }
                
                char[] invalidNameChars = Path.GetInvalidFileNameChars();
                if (Encoding.Latin1.GetByteCount(name) != name.Length || name?.ToCharArray().Where(c => invalidNameChars.Contains(c)).ToList().Count > 0 || Menu.GetItemsClone().Select(item => item.Text).Contains(name) || name == "..")
                {
                    Console.CursorVisible = false;
                    Console.Clear();
                    
                    return;
                }

                string? input = "";
                while (input != "y" && input != "n")
                {
                    Console.Write($"{AnsiColorF.Reset} Are you sure? [y/n]: ");
                    input = Console.ReadLine()?.Trim();
                }

                if (input == "n")
                {
                    Console.CursorVisible = false;
                    Console.Clear();
                    
                    return;
                }
                
                try
                {
                    MenuItem? item = Menu.GetItemAt(Menu.SelectedIndex);
                    if (Directory.Exists(item?.Text))
                    {
                        Directory.Move(item.Text, name);
                        
                        List<string> dirHistoryList = _dirHistory.ToList();
                        _dirHistory.Clear();
                        
                        for (int i = dirHistoryList.Count - 1; i >= 0; i--)
                        {
                            string dirPath = dirHistoryList[i];
                            if (dirPath == Path.GetFullPath(item.Text))
                            {
                                dirPath = Path.GetFullPath(name);
                            }
                            
                            _dirHistory.Push(dirPath);
                        }
                    }
                    else if (File.Exists(item?.Text))
                    {
                        File.Move(item.Text, name);
                    }
                    
                    item.Text = name;
                }
                catch { }
                
                Console.CursorVisible = false;
                Console.Clear();
            }
        }
        else if (key == Key.F10)
        {
            lock (MenuLock)
            {
                Console.CursorVisible = true;
                Console.Clear();
                
                InputListener.OpenConsoleInput();
                listener.StopListening();
                
                Environment.Exit(0);
            }
        }
        else
        {
            return;
        }
    }

    private static void SelectItem()
    {
        MenuItem? item = Menu.GetItemAt(Menu.SelectedIndex);
        if (item?.Text == "..") return;
                    
        if (!_selectedItems.Remove(item))
        {
            _selectedItems.Add(item);
        }
    }

    private static string? ReadLine()
    {
        InputListener.OpenConsoleInput();
        
        StringBuilder builder = new();
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    builder.Clear();
                    break;
                }
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (builder.ToString().Length > 0)
                    {
                        Console.Write("\b \b");
                        builder.Remove(builder.Length - 1, 1);
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(keyInfo.KeyChar) || char.IsWhiteSpace(keyInfo.KeyChar) || char.IsSymbol(keyInfo.KeyChar))
                    {
                        builder.Append(keyInfo.KeyChar);
                        Console.Write(keyInfo.KeyChar);
                    }
                }
            }
        }

        InputListener.CloseConsoleInput();
        return string.IsNullOrEmpty(builder.ToString()) ? null : builder.ToString();
    }
    
    private static void CopyDirectory(string sourceDir, string destinationDir)
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
    
    private static void OpenCommandLine()
    {
        Console.CursorVisible = true;
        
        string shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
        _commandLine = new Process
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
        
        lock (MenuLock)
        {
            _commandLine.Start();
        }
        
        _commandLine.WaitForExit();
        _commandLine = null;
        
        Console.CursorVisible = false;
        Task.Run(() =>
        {
            _refreshCancelSource.Cancel();
            _refreshCancelSource = new();
                            
            Console.Clear();
            CancellationToken token = _refreshCancelSource.Token;
            RefreshMenuItems(token);
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
