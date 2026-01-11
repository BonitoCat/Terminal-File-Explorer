using CmdMenu;

namespace FileExplorer.Keybinds;

public class MultiSelectKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        int lastSelectedIndex = _context.Menu.IndexOf(_context.SelectedItems.LastOrDefault());
        if (lastSelectedIndex == -1 || lastSelectedIndex == _context.Menu.SelectedIndex)
        {
            _context.SelectItem();
            return;
        }

        if (lastSelectedIndex < _context.Menu.SelectedIndex)
        {
            for (int i = lastSelectedIndex + 1; i <= _context.Menu.SelectedIndex; i++)
            {
                MenuItem? item = _context.Menu.GetItemAt(i);
                if (item?.Text == "..")
                {
                    continue;
                }
                        
                if (!_context.SelectedItems.Contains(item))
                {
                    _context.SelectedItems.Add(item);
                }
            }
        }
        else
        {
            for (int i = lastSelectedIndex - 1; i >= _context.Menu.SelectedIndex; i--)
            {
                MenuItem? item = _context.Menu.GetItemAt(i);
                if (item?.Text == "..")
                {
                    continue;
                }
                        
                if (!_context.SelectedItems.Contains(item))
                {
                    _context.SelectedItems.Add(item);
                }
            }
        }
    }
}