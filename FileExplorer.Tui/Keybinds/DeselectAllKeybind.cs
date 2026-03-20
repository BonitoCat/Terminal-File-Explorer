using FileExplorer.Tui.Context;
using InputLib.EventArgs;

namespace FileExplorer.Tui.Keybinds;

public class DeselectAllKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        _context.SelectedItems.Clear();
        _context.RedrawMenu();
    }
}