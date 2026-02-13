using FileExplorer.Context;

namespace FileExplorer.Keybinds;

public class SelectKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        _context.SelectItem();
        _context.RedrawMenu();
    }
}