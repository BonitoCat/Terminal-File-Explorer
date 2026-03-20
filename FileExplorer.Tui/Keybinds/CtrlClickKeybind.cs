using FileExplorer.Tui.Context;
using FileExplorer.Tui.FileTypes;
using InputLib.EventArgs;
using TuiLib.Controls;

namespace FileExplorer.Tui.Keybinds;

public class CtrlClickKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        
        TuiListBoxItem<TuiLabel>? item = _context.Menu.SelectedItem;
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