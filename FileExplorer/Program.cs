using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.Context;
using FileExplorer.Keybinds;
using FileLib;
using InputLib;
using InputLib.EventArgs;
using InputLib.PlatformListener;
using LoggerLib;
using Microsoft.VisualBasic.FileIO;

namespace FileExplorer;

class Program
{
    private static string _helpStr =
        $"""
         
          Command:
           {Process.GetCurrentProcess().ProcessName} [List of arguments]
          
          Arguments:
           -h / --help - Show this menu
           -v / --version - Show installed version
           -o / --open - Open current directory in the default file explorer
           -d / --directory (path) - The directory to start in
           -t / --tty - Force {Process.GetCurrentProcess().ProcessName} to start with tty input
          
          Controls:
           Navigation:
           | Up- / Down Arrow - Navigate items
           | Shift + Up / Down Arrow - Navigate items quickly
           | F6 / Ctrl + Left- / -Right Arrow - Navigate between menus
           | Enter / Arrow Right - Open selected directory / file
           | Ctrl + Enter / -Arrow Right - Open selected file in nano
           | Escape / Alt + Up- / Left Arrow - Go up one directory
           | Escape - Cancel current action
           | Alt + Left Arrow - Return to previous directory
           | Ctrl + W - Go to specific directory by path
           | Pos1 / Ctrl + Up Arrow - Go to fist item of menu
           | End / Ctrl + Down Arrow - Go to last item of menu
           | F4 - Navigate ot bookmarks
           | Ctrl + D - Switch between menu and command line
           
           Editing:
           | F2 - Rename selected item
           | Delete - Move item to recycle bin
           | Shift + Delete - Permanently delete item
           | Space - Select item
           | Shift + Space - Select a region of items
           | Ctrl + A - Select all directories and files
           | Shift + A - Deselect all directories and files
           | Ctrl + B - Add / remove current folder in bookmarks
           | Ctrl + C - Copy item
           | Ctrl + X - Cut item
           | Ctrl + V - Paste item
           | Ctrl + N - Create new file
           | Shift + N - Create new directory
           | Shift + D - Duplicate directory / file
           
           Misc:
           | F3 - Open / close second menu
           | F5 | Ctrl + R - Reload menu
           | Ctrl + F - Search in current directory
           | Ctrl + H - Toggle visibility of hidden files / directories
           | Ctrl + J - Toggle visibility of file sizes
           | Ctrl + O - Open current directory in OS file explorer
           | Shift + C - Copy current directory path to clipboard
           | F1 - Show this menu
           | F10 - Close file explorer (Also see Ctrl + D)
         
         """;

    private static List<MenuContext> _contexts = [];
    private static int _selectedContextIndex;
    private static MenuContext? _selectedContext => _selectedContextIndex >= _contexts.Count ? null : _contexts[_selectedContextIndex];
    
    private static List<Keybind> _keybinds = new();
    private static string[] _fileSizes = ["B", "kiB", "MiB", "GiB"];
    
    private static readonly object OutLock = new();
    private static readonly ClipboardContext ClipboardContext = new();
    private static readonly ManualResetEventSlim ExitEvent = new();
    private static string? _startDir;
    private static bool _forceTtyInput;
    
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
                
                case "--version":
                case "-v":
                    Console.WriteLine(Generated.BuildInfo.Version + "\n");
                    return;
                
                case "--open":
                case "-o":
                    if (OperatingSystem.IsLinux())
                    {
                        Process proc = new()
                        {
                            StartInfo =
                            {
                                FileName = "xdg-open",
                                Arguments = ".",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            },
                        };

                        proc.Start();
                    }
                    
                    return;
                
                case "--directory":
                case "-d":
                    if (args.Length < i + 2)
                    {
                        Console.WriteLine("Missing argument (path)\n");
                        return;
                    }
                    
                    _startDir = args[i + 1];
                break;
                
                case "--tty":
                case "-t":
                    _forceTtyInput = true;
                break;
            }
        }
        
        Directory.SetCurrentDirectory(_startDir ?? Directory.GetCurrentDirectory());

        Logger.LogDir = Path.Combine(DirectoryHelper.GetAppDataDirPath(), "fe", "logs");
        Logger.KeepLogs = 20;
        Logger.LogDebug = false;
        
        Logger.CreateFile();
        
        InputListener.Init();
        InputListener.DisableEcho();
        
        Clipboard.ReadPaths(out ClipboardMode mode, out string[] paths);
        ClipboardContext.Items.AddRange(paths);
        ClipboardContext.Mode = mode;
        
        _contexts.Add(CreateMenuContext());
        UpdateContexts();
        SwitchContext(0);
        
        Console.OutputEncoding = Encoding.UTF8;
        Console.TreatControlCAsInput = true;
        Console.CursorVisible = false;
        Console.Clear();

        WindowManager.Instance.MainWindow.Title = "Terminal File-Explorer";
        WindowManager.Instance.MainWindow.OnWindowResize += OnResize;

        if (_selectedContext == null || _selectedContext.Listener == null)
        {
            Console.WriteLine("Something went wrong while loading\n");
            return;
        }
        
        _selectedContext.Listener.ClearKeyState();
        _selectedContext.Listener.ConsumeNextKeyDown(Key.Enter);
        _selectedContext.Listener.ConsumeNextKeyUp(Key.Enter);

        Task.Run(() =>
        {
            int consoleWidth = Console.WindowWidth;
            int consoleHeight = Console.WindowHeight;
            while (!_selectedContext.ExitEvent.IsSet)
            {
                if (consoleWidth == Console.WindowWidth && consoleHeight == Console.WindowHeight)
                {
                    continue;
                }
                
                consoleWidth = Console.WindowWidth;
                consoleHeight = Console.WindowHeight;

                OnResize();
            }
        });

        _selectedContext.RedrawMenu();
        ExitEvent.Wait();
    }

    private static void DrawMenu(MenuContext context)
    {
        lock (OutLock)
        lock (context.OutLock)
        {
            void PrintTopBar()
            {
                string cwd = $"File-Explorer ({(context.Cwd == context.BookmarkDir ? "Bookmarks" : context.Cwd)})";
                
                Console.SetCursorPosition(context.Menu.X, context.Menu.Y);
                Console.Write($"\x1b[?7l{Color.Reset.ToAnsi()} ");
                
                int diff = Math.Max(cwd.Length - context.Menu.MaxWidth, 0);
                string dots = new('.', Math.Min(diff + 1, 3));
                Console.Write(cwd.Length > context.Menu.MaxWidth - 1 ? dots + cwd.Substring(diff + dots.Length + 1) : cwd);
            }
            
            if (context.Menu.GetItemCount() == 0)
            {
                PrintTopBar();
                Console.WriteLine("\x1b[?7h");
                
                return;
            }
            
            int longestLine;
            try
            {
                longestLine = context.Menu
                                     .Items
                                     .Where(item => item.Data.TryGetValue("ItemType", out string? type) && type == "File")
                                     .Max(item => item.Item.Length);
            }
            catch (InvalidOperationException)
            {
                longestLine = -1;
            }
        
            PrintTopBar();
            
            int dy = 1;
            context.Menu
               .GetViewItems()
               .ForEach(item =>
               {
                   int i = context.Menu.IndexOf(item);
                   
                   StringBuilder builder = new();
                   builder.Append(Color.Reset.ToAnsi(Color.AnsiType.Both));
                   builder.Append(i == context.Menu.SelectedIndex ? " > " : "   ");

                   bool hasFullPath = item.Data.TryGetValue("FullPath", out string? fullPath);
                   bool hasDefaultColor = item.Data.TryGetValue("DefaultColor", out string? defaultColor);
                   bool hasDimmedColor = item.Data.TryGetValue("DimmedColor", out string? cutColor);
                   if (hasFullPath && hasDefaultColor && hasDimmedColor)
                   {
                       string ansiColor = 
                           context != _selectedContext ||
                           (context.ClipboardContext.Items.Contains(fullPath) && context.ClipboardContext.Mode == ClipboardMode.Cut)
                           ? cutColor
                           : defaultColor;
                       
                       item.Item.Style.Foreground = Color.FromRgbString(ansiColor);
                   }
                   
                   if (context.SelectedItems.Contains(item))
                   {
                       builder.Append(item.Item.Prefix);
                       builder.Append(Color.White.ToAnsi(Color.AnsiType.Background));
                       builder.Append(Color.Black.ToAnsi());
                       builder.Append(item.Item.Text);
                       builder.Append(Color.Reset.ToAnsi(Color.AnsiType.Both));
                       builder.Append(item.Item.Suffix);
                   }
                   else
                   {
                       builder.Append(context.Menu.GetItemAt(i).Item);
                   }

                   if (context.ShowFileSizes && longestLine != -1 && item.Data.TryGetValue("InfoSize", out string? size))
                   {
                       if (long.TryParse(size, out long sizeLong))
                       {
                           double sizeCalc = sizeLong;
                           int sizeType = 0;
                           while (sizeCalc >= 1024 && sizeType < _fileSizes.Length)
                           {
                               sizeCalc /= 1024f;
                               sizeType++;
                           }
                           
                           int sizePos = longestLine - item.Item.TextLength - item.Item.SuffixLength + 5;
                           builder.Append(i == context.Menu.SelectedIndex ? Color.White.ToAnsi() : Color.LightGray.ToAnsi());
                           builder.Append(new string(' ', sizePos) + $"{sizeCalc.ToString("F1")} {_fileSizes[sizeType]}");
                       }
                   }
                   
                   builder.Append(new string(' ',
                       Math.Max(context.Menu.MaxWidth - Color.TrimAnsi(builder.ToString()).Length, 0)));
                   
                   lock (OutLock)
                   lock (context.OutLock)
                   {
                       Console.SetCursorPosition(context.Menu.X, context.Menu.Y + dy);
                       Console.Write(builder);
                       dy++;
                   }
               });
            
            if (context.Menu.ViewIndex + context.Menu.ViewRange < context.Menu.GetItemCount())
            {
                Console.SetCursorPosition(context.Menu.X, context.Menu.Y + dy + 1);
                string moreText = $"{Color.Reset.ToAnsi()}   --MORE--";
                Console.Write(moreText + new string(' ', Math.Max(context.Menu.MaxWidth - Color.TrimAnsi(moreText).Length, 0)));
            }
            else
            {
                Console.SetCursorPosition(context.Menu.X, context.Menu.Y + dy + 1);
                Console.Write(new string(' ', Math.Max(context.Menu.MaxWidth, 0)));
            }
            
            Console.WriteLine("\x1b[?7h");
        }
    }
    
    private static void OnResize()
    {
        Logger.LogI("Resized window");
        
        UpdateContexts();
        lock (OutLock)
        {
            Console.Clear();
        }
        
        foreach (MenuContext context in _contexts)
        {
            context.RedrawMenu();
        }
    }

    private static MenuContext CreateMenuContext()
    {
        MenuContext context = new()
        {
            Menu = new(),
            ClipboardContext = ClipboardContext,
            OutLock = OutLock,
            ExitEvent = ExitEvent,
            ForceTtyInput = _forceTtyInput,
        };

        context.Listener = _forceTtyInput ? new TtyInputListener() : InputListener.New();
        if (context.Listener == null)
        {
            throw new InvalidOperationException("Could not load input listener\n");
        }
        
        context.Listener.PauseListening = true;
        context.Listener.RaiseEvents = false;
        
        context.OnClickDir(new CmdLabel(Directory.GetCurrentDirectory()), false);
        
        context.BookmarkDir = Path.Combine(DirectoryHelper.GetAppDataDirPath(), "fe", "Bookmarks");
        DirectoryHelper.CreateDir(context.BookmarkDir);

        context.Listener.RepeatIntervalMs = 30;
        context.Listener.StartListening();
        
        context.Listener.OnKeyDown += (key, e) => HandleKeyDown(context.Listener, key, e);
        context.Listener.OnKeyUp += key => HandleKeyUp(context.Listener, key);
        context.Listener.OnKeyJustPressed += key => HandleKeyJustPressed(context.Listener, key);
        
        context.Menu.MenuUpdate += () =>
        {
            foreach (MenuContext context in _contexts.OrderBy(context => context.Menu.ZIndex))
            {
                if (context.CommandLine != null)
                {
                    return;
                }

                DrawMenu(context);
            }
        };
        
        Logger.LogI("Created new menu");

        return context;
    }

    private static void UpdateContexts()
    {
        for (int i = 0; i < _contexts.Count; i++)
        {
            _contexts[i].Menu.MaxWidth = Console.WindowWidth / _contexts.Count;
            _contexts[i].Menu.MaxHeight = Math.Max(Console.WindowHeight - 6, 0);
            _contexts[i].Menu.X = Console.WindowWidth / _contexts.Count * i;
            _contexts[i].Menu.ZIndex = i;
            
            _contexts[i].Menu.ViewRange = Math.Max(WindowManager.Instance.MainWindow.Height - 6, 0);
            if (Console.WindowHeight >= _contexts[i].Menu.ViewIndex + _contexts[i].Menu.ViewRange - 1)
            {
                _contexts[i].Menu.ViewIndex = 0;
            }
        }
    }

    private static void SwitchContext(int dir)
    {
        _contexts.ForEach(context =>
        {
            context.Listener.PauseListening = true;
            context.Listener.RaiseEvents = false;
        });
        _selectedContextIndex = Math.Clamp(_selectedContextIndex + dir, 0, _contexts.Count - 1);
        MapKeybinds(_selectedContext);
        
        _selectedContext.Listener.PauseListening = false;
        _selectedContext.Listener.RaiseEvents = true;

        Directory.SetCurrentDirectory(_selectedContext.Cwd);
        
        Console.Clear();
        _contexts.ForEach(DrawMenu);
    }

    private static void MapKeybinds(MenuContext context)
    {
        _keybinds.Clear();
        
        _keybinds.Add(new NavUpKeybind(context) { Keys = [Key.ArrowUp] });
        _keybinds.Add(new NavDownKeybind(context) { Keys = [Key.ArrowDown] });
        
        _keybinds.Add(new ClickKeybind(context) { Keys = [Key.Enter] });
        _keybinds.Add(new ClickKeybind(context) { Keys = [Key.ArrowRight] });
        _keybinds.Add(new CtrlClickKeybind(context) { Keys = [Key.LeftCtrl, Key.Enter] });
        
        _keybinds.Add(new ReturnKeybind(context) { Keys = [Key.Escape] });
        _keybinds.Add(new ReturnKeybind(context) { Keys = [Key.Alt, Key.ArrowUp] });
        _keybinds.Add(new ReturnKeybind(context) { Keys = [Key.ArrowLeft] });
        
        _keybinds.Add(new SelectKeybind(context) { Keys = [Key.Space] });
        _keybinds.Add(new MultiSelectKeybind(context) { Keys = [Key.LeftShift, Key.Space] });
        _keybinds.Add(new SelectAllKeybind(context) { Keys = [Key.LeftCtrl, Key.A] });
        _keybinds.Add(new DeselectAllKeybind(context) { Keys = [Key.LeftShift, Key.A] });
        
        _keybinds.Add(new CmdKeybind(context) { Keys = [Key.LeftCtrl, Key.D] });
        _keybinds.Add(new NemoKeybind(context) { Keys = [Key.LeftCtrl, Key.O] });
        
        _keybinds.Add(new DirHistoryKeybind(context) { Keys = [Key.Alt, Key.ArrowLeft] });
        
        _keybinds.Add(new JumpStartKeybind(context) { Keys = [Key.LeftCtrl, Key.ArrowUp] });
        _keybinds.Add(new JumpStartKeybind(context) { Keys = [Key.Home] });
        
        _keybinds.Add(new JumpEndKeybind(context) { Keys = [Key.LeftCtrl, Key.ArrowDown] });
        _keybinds.Add(new JumpEndKeybind(context) { Keys = [Key.End] });
        
        _keybinds.Add(new ReloadKeybind(context) { Keys = [Key.LeftCtrl, Key.R] });
        _keybinds.Add(new ReloadKeybind(context) { Keys = [Key.F5] });
        
        _keybinds.Add(new HideKeybind(context) { Keys = [Key.LeftCtrl, Key.H] });
        _keybinds.Add(new SearchKeybind(context) { Keys = [Key.LeftCtrl, Key.F] });
        
        _keybinds.Add(new NewFolderKeybind(context) { Keys = [Key.LeftShift, Key.N] });
        _keybinds.Add(new NewFileKeybind(context) { Keys = [Key.LeftCtrl, Key.N] });
        
        _keybinds.Add(new DirPathKeybind(context) { Keys = [Key.LeftCtrl, Key.W] });
        
        _keybinds.Add(new CopyKeybind(context) { Keys = [Key.LeftCtrl, Key.C] });
        _keybinds.Add(new CutKeybind(context) { Keys = [Key.LeftCtrl, Key.X] });
        _keybinds.Add(new PasteKeybind(context) { Keys = [Key.LeftCtrl, Key.V] });
        _keybinds.Add(new DuplicateKeybind(context) { Keys = [Key.LeftShift, Key.D] });
        
        _keybinds.Add(new DeleteKeybind(context) { Keys = [Key.Delete] });
        _keybinds.Add(new DeletePermKeybind(context) { Keys = [Key.LeftShift, Key.Delete] });

        _keybinds.Add(new SizeKeybind(context) {Keys = [Key.LeftCtrl, Key.J] });
        _keybinds.Add(new CopyPathKeybind(context) {Keys = [Key.LeftShift, Key.C]});
        
        _keybinds.Add(new BookmarkMenuKeybind(context) {Keys = [Key.F4]});
        _keybinds.Add(new AddBookmarkKeybind(context) {Keys = [Key.LeftCtrl, Key.B]});
        
        _keybinds.Add(new HelpKeybind(context, _helpStr) { Keys = [Key.F1] });
        _keybinds.Add(new RenameKeybind(context) { Keys = [Key.F2] });
        _keybinds.Add(new ExitKeybind(context) { Keys = [Key.F10] });
        
        _keybinds.Add(new CreateMenuKeybind(context, () =>
        {
            if (_contexts.Count == 1)
            {
                _contexts.Add(CreateMenuContext());
            }
            else if (_contexts.Count == 2)
            {
                _contexts.RemoveAll(context => context != _selectedContext);
                _selectedContextIndex = 0;
            }
            
            UpdateContexts();
            
            Console.Clear();
            _contexts.ForEach(DrawMenu);
        }) { Keys = [Key.F3] });
        
        _keybinds.Add(new SwitchMenuKeybind(context, -1, SwitchContext) { Keys = [Key.LeftCtrl, Key.ArrowLeft] });
        _keybinds.Add(new SwitchMenuKeybind(context, 1, SwitchContext) { Keys = [Key.LeftCtrl, Key.ArrowRight] });
        _keybinds.Add(new SwitchMenuKeybind(context, _selectedContextIndex == 0 ? 1 : -1, SwitchContext) { Keys = [Key.F6] });
        
        Logger.LogI("Mapped keybinds");
    }

    private static void HandleKeyDown(InputListener listener, Key key, KeyDownEventArgs e)
    {
        List<Key> heldKeys = listener.GetHeldKeys().ToList();
        Keybind? bestMatch =
            _keybinds
                .Where(kb => kb.Keys.All(k => heldKeys.Contains(k)))
                .OrderByDescending(kb => kb.Keys.Count)
                .FirstOrDefault();

        bestMatch?.OnKeyDown(e);
    }

    private static void HandleKeyUp(InputListener listener, Key key)
    {
        List<Key> heldKeys = listener.GetHeldKeys().ToList();
        heldKeys.Add(key);

        Keybind? bestMatch =
            _keybinds
                .Where(kb => kb.Keys.All(k => heldKeys.Contains(k)))
                .OrderByDescending(kb => kb.Keys.Count)
                .FirstOrDefault();

        bestMatch?.OnKeyUp();
    }
    
    private static void HandleKeyJustPressed(InputListener listener, Key key)
    {
        List<Key> heldKeys = listener.GetHeldKeys().ToList();
        Keybind? bestMatch =
            _keybinds
                .Where(kb => kb.Keys.All(k => heldKeys.Contains(k)))
                .OrderByDescending(kb => kb.Keys.Count)
                .FirstOrDefault();

        bestMatch?.OnKeyJustPressed();
    }
}
