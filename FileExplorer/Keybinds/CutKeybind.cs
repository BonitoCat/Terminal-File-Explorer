using CmdMenu;

namespace FileExplorer.Keybinds;

public class CutKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        string[] paths = [];
        lock (_context.Menu.Lock)
        {
            if (_context.SelectedItems.Count > 0)
            {
                paths = _context.SelectedItems
                                .Select(item => item.Data.GetValueOrDefault("FullPath", ""))
                                .Where(path => !string.IsNullOrEmpty(path))
                                .ToArray();
            }
            else
            {
                MenuItem? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (item == null || item.Text == "..")
                {
                    return;
                }

                if (item.Data.TryGetValue("FullPath", out string? path))
                {
                    paths = [path];
                }
            }

            Clipboard.Write(ClipboardMode.Cut, paths);
        }

        _context.RedrawMenu();
    }
    
}