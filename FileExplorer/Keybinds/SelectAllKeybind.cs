using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.Context;

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
                        
            if (!_context.SelectedItems.Contains(item))
            {
                _context.SelectedItems.Add(item);
            }
        }
        
        _context.RedrawMenu();
    }
}