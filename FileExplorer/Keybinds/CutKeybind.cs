using CmdMenu;

namespace FileExplorer.Keybinds;

public class CutKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            _context.MoveItems.Clear();
            if (_context.SelectedItems.Count > 0)
            {
                _context.MoveItems.AddRange(_context.SelectedItems.Select(item => Path.GetFullPath(item.Text)));
            }
            else
            {
                MenuItem? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (item?.Text == "..")
                {
                    return;
                }
                        
                _context.MoveItems.Add(Path.GetFullPath(item.Text));
            }

            _context.MoveStyle = MoveStyle.Cut;
        }

        //_context.RedrawMenu();
        _context.RefreshItems();
    }
}