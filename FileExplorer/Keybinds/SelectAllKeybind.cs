using CmdMenu;
using CmdMenu.Controls;

namespace FileExplorer.Keybinds;

public class SelectAllKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        foreach (CmdListBoxItem<CmdLabel> item in _context.Menu.Items)
        {
            if (item.Item.Text == "..")
            {
                continue;
            }
                        
            if (!_context.SelectedItems.Contains(item.Item))
            {
                _context.SelectedItems.Add(item.Item);
            }
        }
        
        _context.RedrawMenu();
    }
}