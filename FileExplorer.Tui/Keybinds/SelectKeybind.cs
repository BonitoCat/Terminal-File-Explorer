using FileExplorer.Tui.Context;

namespace FileExplorer.Tui.Keybinds;

public class SelectKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        _context.SelectItem();
        _context.RedrawMenu();
    }
}