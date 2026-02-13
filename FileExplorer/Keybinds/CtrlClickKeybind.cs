using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.Context;
using FileExplorer.FileTypes;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class CtrlClickKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        
        CmdListBoxItem<CmdLabel>? item = _context.Menu.SelectedItem;
        if (item == null)
        {
            return;
        }

        if (!item.Data.TryGetValue("ItemType", out string? fileType) || fileType != "File")
        {
            return;
        }
        
        TextFile.OnClick(_context, item.Item);
    }
}