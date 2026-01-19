using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using CmdMenu;
using FileExplorer.Keybinds;
using InputLib;
using InputLib.EventArgs;

namespace FileExplorer;

class Program
{
    private static string _helpStr =
        $"""
         
          Command:
           {Process.GetCurrentProcess().ProcessName} [List of arguments]
 
          Arguments:
           -h | --help - Show this menu
           -v | --version - Show version
           -o | --open - Open current directory in nemo
 
          Controls:
           Navigation:
           | Up- / Down Arrow - Navigate items
           | Shift + Up / Down Arrow - Navigate items quickly
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
           | Shift + A - Deselect all directories and files
           | Ctrl + C - Copy item
           | Ctrl + X - Cut item
           | Ctrl + V - Paste item
           | Ctrl + N - Create new file
           | Shift + N - Create new directory
           | Shift + D - Duplicate directory / file
           
           Misc:
           | (WIP) F3 - Open / close second menu
           | F5 | Ctrl + R - Reload menu
           | Ctrl + F - Search in current directory
           | Ctrl + H - Toggle visibility of hidden files / directories
           | Ctrl + J - Toggle visibility of file sizes
           | Ctrl + O - Open current directory in file explorer
           | Shift + C - Copy current directory path to clipboard
           | F1 - Show this menu
           | F10 - Close file explorer (Also see Ctrl + D)
 
         """;
    
    private static MenuContext _context = new();
    private static List<Keybind> _keybinds = new();

    private static string[] _fileSizes = ["B", "KiB", "MiB", "GiB"];
    private static readonly Regex AnsiRegex =
        new(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
            RegexOptions.Compiled);

    
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
                    Process.Start("nemo", Directory.GetCurrentDirectory());
                    return;
            }
        }

        InputListener.Init();

        Console.OutputEncoding = Encoding.UTF8;
        Console.TreatControlCAsInput = true;
        Console.CursorVisible = false;
        Console.Clear();

        _context.Menu = new();
        _context.OnClickDir(new(Directory.GetCurrentDirectory()), false);
        
        InputListener.DisableEcho();
        _context.Listener = InputListener.New();
        
        if (_context.Listener == null)
        {
            Console.WriteLine("Could not load input listener");
            return;
        }
        
        _context.Listener.ClearKeyState();
        _context.Listener.ConsumeNextKeyDown(Key.Enter);
        _context.Listener.ConsumeNextKeyUp(Key.Enter);
        
        MapKeybinds(_context.Listener);

        _context.Listener.RepeatRateMs = 30;
        _context.Listener.StartListening();
        
        _context.Listener.OnKeyDown += (key, e) => HandleKeyDown(_context.Listener, key, e);
        _context.Listener.OnKeyUp += key => HandleKeyUp(_context.Listener, key);

        Task.Run(() =>
        {
            int consoleWidth = Console.WindowWidth;
            int consoleHeight = Console.WindowHeight;
            while (!_context.ExitEvent.IsSet)
            {
                if (consoleWidth != Console.WindowWidth || consoleHeight != Console.WindowHeight)
                {
                    consoleWidth = Console.WindowWidth;
                    consoleHeight = Console.WindowHeight;

                    OnResize();
                }
            }
        });
        
        _context.Menu.MenuUpdate += () =>
        {
            /*if (_context.IsDrawing)
            {
                return;
            }*/

            if (_context.CommandLine != null)
            {
                return;
            }

            DrawMenu();
        };

        _context.RedrawMenu();
        /*while (!_context.ExitEvent.IsSet)
        {
            if (_context.RedrawRequested)
            {
                DrawMenu();
                _context.RedrawRequested = false;
            }

            Thread.Sleep(16);
        }*/
        _context.ExitEvent.Wait();
    }

    private static void DrawMenu()
    {
        //_context.IsDrawing = true;
        lock (_context.OutLock)
        {
            Console.SetCursorPosition(0, 0);
            Console.Write("\x1b[?7l");
            
            if (_context.Menu.GetItemCount() == 0)
            {
                Console.Clear();
                Console.WriteLine($"\x1b[?7l\x1b[2K{Color.Reset.ToAnsi()} File-Explorer ({Directory.GetCurrentDirectory()})");
                Console.WriteLine("\x1b[?7h");
                
                //_context.IsDrawing = false;
                return;
            }
        }
        
        int consoleWidth = Console.WindowWidth;
        int consoleHeight = Console.WindowHeight;

        int longestLine;
        try
        {
            longestLine = _context.Menu
                                  .GetItems()
                                  .Where(item => item.Data.TryGetValue("ItemType", out string? type) && type == "File")
                                  .Max(item => StripAnsi(item.Prefix).Length + StripAnsi(item.Text).Length + StripAnsi(item.Suffix).Length);
        }
        catch (InvalidOperationException)
        {
            longestLine = -1;
        }
        
        StringBuilder builder = new();
        builder.AppendLine($"\x1b[?7l\x1b[2K{Color.Reset.ToAnsi()} File-Explorer ({Directory.GetCurrentDirectory()})");
        _context.Menu
                .GetViewItems()
                .ForEach(item =>
                {
                    int i = _context.Menu.IndexOf(item);
                    
                    builder.Append("\x1b[2K");
                    builder.Append(Color.Reset.ToAnsi(Color.AnsiType.Both));
                    builder.Append(i == _context.Menu.SelectedIndex ? " > " : "   ");

                    bool hasFullPath = item.Data.TryGetValue("FullPath", out string? path);
                    bool hasDefaultColor = item.Data.TryGetValue("DefaultColor", out string? defaultColor);
                    bool hasCutColor = item.Data.TryGetValue("CutColor", out string? cutColor);
                    if (hasFullPath && hasDefaultColor && hasCutColor)
                    {
                        string ansiColor = _context.MoveItems.Contains(path) && _context.MoveStyle == MoveStyle.Cut ? cutColor : defaultColor;
                        item.ForegroundColor = Color.FromRgbString(ansiColor);
                    }

                    if (_context.SelectedItems.Contains(item))
                    {
                        builder.Append(item.Prefix);
                        builder.Append(Color.White.ToAnsi(Color.AnsiType.Background));
                        builder.Append(Color.Black.ToAnsi());
                        builder.Append(item.Text);
                        builder.Append(Color.Reset.ToAnsi(Color.AnsiType.Both));
                        builder.Append(item.Suffix);
                    }
                    else
                    {
                        builder.Append(_context.Menu.GetItemAt(i));
                    }

                    if (_context.ShowFileSizes && longestLine != -1 && item.Data.TryGetValue("InfoSize", out string? size))
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
                            
                            int sizePos = longestLine - StripAnsi(item.Text).Length - StripAnsi(item.Suffix).Length + 5;
                            builder.Append(i == _context.Menu.SelectedIndex ? Color.White.ToAnsi() : Color.LightGray.ToAnsi());
                            builder.Append(new string(' ', sizePos) + $"{sizeCalc.ToString("F1")} {_fileSizes[sizeType]}");
                        }
                    }
                    
                    builder.AppendLine();
                });
        
        _context.Menu.ViewRange = Math.Max(consoleHeight - 6, 0);
        if (_context.Menu.ViewIndex + _context.Menu.ViewRange < _context.Menu.GetItemCount())
        {
            builder.AppendLine($"\x1b[2K\n\x1b[2K{Color.Reset.ToAnsi()}   --MORE--");
        }
        else
        {
            builder.Append("\x1b[2K\n\x1b[2K");
        }

        builder.Append("\x1b[?7h");
        
        lock (_context.OutLock)
        {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine(builder);
        }
        
        Thread.Sleep(10);
        if (Console.WindowHeight != consoleHeight || Console.WindowWidth != consoleWidth)
        {
            if (Console.WindowHeight >= _context.Menu.ViewIndex + _context.Menu.ViewRange - 1)
            {
                _context.Menu.ViewIndex = 0;
            }

            lock (_context.OutLock)
            {
                Console.Clear();
            }
        }
        
        //_context.IsDrawing = false;
    }
    
    private static string StripAnsi(string input)
    {
        return AnsiRegex.Replace(input, "");
    }


    private static void OnResize()
    {
        lock (_context.OutLock)
        {
            Console.Clear();
        }
        
        _context.RedrawMenu();
    }
    
    private static void MapKeybinds(InputListener listener)
    {
        _keybinds.Clear();
        
        _keybinds.Add(new NavUpKeybind(_context) { Keys = [Key.ArrowUp] });
        _keybinds.Add(new NavDownKeybind(_context) { Keys = [Key.ArrowDown] });
        
        _keybinds.Add(new ClickKeybind(_context) { Keys = [Key.Enter] });
        _keybinds.Add(new ReturnKeybind(_context) { Keys = [Key.Escape] });
        _keybinds.Add(new ReturnKeybind(_context) { Keys = [Key.Alt, Key.ArrowUp] });
        
        _keybinds.Add(new SelectKeybind(_context) { Keys = [Key.Space] });
        _keybinds.Add(new MultiSelectKeybind(_context) { Keys = [Key.LeftShift, Key.Space] });
        _keybinds.Add(new SelectAllKeybind(_context) { Keys = [Key.LeftCtrl, Key.A] });
        _keybinds.Add(new DeselectAllKeybind(_context) { Keys = [Key.LeftShift, Key.A] });
        
        _keybinds.Add(new CmdKeybind(_context) { Keys = [Key.LeftCtrl, Key.D] });
        _keybinds.Add(new NemoKeybind(_context) { Keys = [Key.LeftCtrl, Key.O] });
        
        _keybinds.Add(new DirHistoryKeybind(_context) { Keys = [Key.LeftCtrl, Key.B] });
        _keybinds.Add(new DirHistoryKeybind(_context) { Keys = [Key.Alt, Key.ArrowLeft] });
        
        _keybinds.Add(new JumpStartKeybind(_context) { Keys = [Key.LeftCtrl, Key.ArrowUp] });
        _keybinds.Add(new JumpStartKeybind(_context) { Keys = [Key.Home] });
        
        _keybinds.Add(new JumpEndKeybind(_context) { Keys = [Key.LeftCtrl, Key.ArrowDown] });
        _keybinds.Add(new JumpEndKeybind(_context) { Keys = [Key.End] });
        
        _keybinds.Add(new ReloadKeybind(_context) { Keys = [Key.LeftCtrl, Key.R] });
        _keybinds.Add(new ReloadKeybind(_context) { Keys = [Key.F5] });
        
        _keybinds.Add(new HideKeybind(_context) { Keys = [Key.LeftCtrl, Key.H] });
        _keybinds.Add(new SearchKeybind(_context) { Keys = [Key.LeftCtrl, Key.F] });
        
        _keybinds.Add(new NewFolderKeybind(_context) { Keys = [Key.LeftShift, Key.N] });
        _keybinds.Add(new NewFileKeybind(_context) { Keys = [Key.LeftCtrl, Key.N] });
        
        _keybinds.Add(new DirPathKeybind(_context) { Keys = [Key.LeftCtrl, Key.W] });
        
        _keybinds.Add(new CopyKeybind(_context) { Keys = [Key.LeftCtrl, Key.C] });
        _keybinds.Add(new CutKeybind(_context) { Keys = [Key.LeftCtrl, Key.X] });
        _keybinds.Add(new PasteKeybind(_context) { Keys = [Key.LeftCtrl, Key.V] });
        _keybinds.Add(new DuplicateKeybind(_context) { Keys = [Key.LeftShift, Key.D] });
        
        _keybinds.Add(new DeleteKeybind(_context) { Keys = [Key.Delete] });
        _keybinds.Add(new DeletePermKeybind(_context) { Keys = [Key.LeftShift, Key.Delete] });

        _keybinds.Add(new SizeKeybind(_context) {Keys = [Key.LeftCtrl, Key.J] });
        _keybinds.Add(new CopyPathKeybind(_context) {Keys = [Key.LeftShift, Key.C]});
        
        _keybinds.Add(new HelpKeybind(_context, listener, _helpStr) { Keys = [Key.F1] });
        _keybinds.Add(new RenameKeybind(_context) { Keys = [Key.F2] });
        _keybinds.Add(new ExitKeybind(_context, listener) { Keys = [Key.F10] });
    }

    private static void HandleKeyDown(InputListener listener, Key key, KeyDownEventArgs e)
    {
        List<Key> heldKeys = listener.GetHeldKeys().ToList();

        Keybind? bestMatch =
            _keybinds
                .Where(kb => kb.Keys.All(k => heldKeys.Contains(k)))
                .OrderByDescending(kb => kb.Keys.Count)
                .FirstOrDefault();

        if (bestMatch != null)
        {
            bestMatch.OnKeyDown(e);
        }
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

        if (bestMatch != null)
        {
            bestMatch.OnKeyUp();
        }
    }
}
