using FileExplorer.Context;
using InputLib.EventArgs;
using LoggerLib;
using TuiLib;
using TuiLib.Controls;
using TuiLib.Rendering.Borders;

namespace FileExplorer.Keybinds;

public class ContextKeybind(MenuContext context, List<MenuContext> contexts) : Keybind(context)
{
    private readonly CmdBorder _border = new()
    {
        Title = "Context Menu",
    };

    private readonly CmdBorder _outerBorder = new()
    {
        BorderStyle = new BorderStyleNone(),
    };
    
    private CmdListBox<CmdLabel> _contextMenu = new();
    private CmdRenderer _renderer = new();
    private List<CmdListBoxItem<CmdLabel>> _selectedItems = new();
    private List<CmdListBoxItem<CmdLabel>> _singleFileItems =
    [
        new("Open", () => {}),
        new("Copy", () => {}),
        new("Cut", () => {}),
        new("Paste", () => {}),
        new("Delete", () => {}),
        new("Rename", () => {}),
        new("Close Menuaaaaaaaaaaaaaaaaaaa", () => {}),
    ];
    
    private List<CmdListBoxItem<CmdLabel>> _singleFolderItems =
    [
        new("Open", () => {}),
        new("Copy", () => {}),
        new("Cut", () => {}),
        new("Paste", () => {}),
        new("Delete", () => {}),
        new("Rename", () => {}),
        new("Add to bookmarks", () => {}),
        new("Close Menu", () => {}),
    ];
    
    private List<CmdListBoxItem<CmdLabel>> _multipleItems =
    [
        new("Open", () => {}),
        new("Copy", () => {}),
        new("Cut", () => {}),
        new("Paste", () => {}),
        new("Delete", () => {}),
        new("Close Menu", () => {}),
    ];
    
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        Logger.LogI("Opening context menu");
        
        _context.Listener.PauseListening = true;
        HandleMenu();
        
        _context.Listener.PauseListening = false;
        
        Logger.LogI("Closing context menu");
        _context.RefreshItems();
    }

    private void OnWindowResize()
    {
        _contextMenu.MaxWidth = Math.Min(WindowManager.Instance.MainWindow.Width - 4, 20);
        _contextMenu.MaxHeight = Math.Min(WindowManager.Instance.MainWindow.Height - 4, _selectedItems.Count);
        _contextMenu.X = WindowManager.Instance.MainWindow.Width / 2;
        _contextMenu.Y = WindowManager.Instance.MainWindow.Height / 2;

        lock (_context.OutLock)
        {
            Console.Clear();
        }

        contexts.ForEach(context => context.RedrawMenu());
        _renderer.Render(_outerBorder.Render());
    }

    private void HandleMenu()
    {
        _contextMenu = new()
        {
            MaxWidth = Math.Min(WindowManager.Instance.MainWindow.Width - 4, 20),
            X = WindowManager.Instance.MainWindow.Width / 2,
            Y = WindowManager.Instance.MainWindow.Height / 2,
            AnchorPoint = AnchorPoint.Center,
        };
        
        _border.Child = _contextMenu;
        _outerBorder.Child = _border;
        
        if (_context.SelectedItems.Count > 1)
        {
            _selectedItems = _context.SelectedItems;
            FillMenuMultiple();
        }
        else
        {
            _selectedItems = _context.SelectedItems.Count == 1 ? _context.SelectedItems : [_context.Menu.SelectedItem ?? new()];
            if (_selectedItems[0].Item?.Text == "..")
            {
                return;
            }
            
            FillMenuSingle();
        }

        _contextMenu.MaxHeight = Math.Min(WindowManager.Instance.MainWindow.Height - 4, _contextMenu.Items.Count);

        WindowManager.Instance.MainWindow.OnWindowResize += OnWindowResize;
        _renderer.Render(_outerBorder.Render());

        Thread.Sleep(2000);
        
        WindowManager.Instance.MainWindow.OnWindowResize -= OnWindowResize;
    }

    private void FillMenuSingle()
    {
        _selectedItems[0].Data.TryGetValue("ItemType", out string? type);
        if (type == "Folder")
        {
            _contextMenu.ItemsSource = _singleFolderItems;
        }
        else if (type == "File")
        {
            _contextMenu.ItemsSource = _singleFileItems;
        }
    }
    
    private void FillMenuMultiple()
    {
        _contextMenu.ItemsSource = _multipleItems;
    }
}