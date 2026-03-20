using FileExplorer.Tui.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class HideKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        lock (_context.Menu.Lock)
        {
            _context.ShowHiddenFiles = !_context.ShowHiddenFiles;
            Logger.LogI($"Toggled hidden items: {_context.SelectedItems}");
            
            _context.RefreshItems();
        }
    }
}