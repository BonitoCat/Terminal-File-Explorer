using FileExplorer.Tui.Context;
using LoggerLib;
using TuiLib.Controls;

namespace FileExplorer.Tui.Keybinds;

public class SelectAllKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        Logger.LogI("Select all requested");
        foreach (TuiListBoxItem<TuiLabel> item in _context.Menu.Items)
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