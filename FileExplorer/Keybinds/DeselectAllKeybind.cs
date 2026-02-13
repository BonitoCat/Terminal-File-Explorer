using FileExplorer.Context;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class DeselectAllKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        _context.SelectedItems.Clear();
        _context.RedrawMenu();
    }
}