using CmdMenu;

namespace FileExplorer.Keybinds;

public class CutKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            if (_context.SelectedItems.Count > 0)
            {
                _context.MoveItems.Clear();
                _context.MoveItems.AddRange(_context.SelectedItems
                                                    .Select(item => item.Data.GetValueOrDefault("FullPath", ""))
                                                    .Where(path => !string.IsNullOrEmpty(path)));
            }
            else
            {
                MenuItem? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (item?.Text == "..")
                {
                    return;
                }
                
                if (item.Data.TryGetValue("FullPath", out string? path))
                {
                    _context.MoveItems.Clear();
                    _context.MoveItems.Add(path);
                }
            }

            _context.MoveStyle = MoveStyle.Cut;
            _context.SelectedItems.Clear();
        }

        _context.RedrawMenu();
    }
}