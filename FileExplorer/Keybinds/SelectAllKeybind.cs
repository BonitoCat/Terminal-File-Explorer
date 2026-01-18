using CmdMenu;

namespace FileExplorer.Keybinds;

public class SelectAllKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        foreach (MenuItem item in _context.Menu.GetItems())
        {
            if (item.Text == "..")
            {
                continue;
            }
                        
            if (!_context.SelectedItems.Contains(item))
            {
                _context.SelectedItems.Add(item);
            }
        }
        
        _context.RedrawMenu();
    }
}