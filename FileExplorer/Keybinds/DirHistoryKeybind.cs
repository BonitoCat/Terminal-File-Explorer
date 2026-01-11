namespace FileExplorer.Keybinds;

public class DirHistoryKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        if (_context.DirHistory.Count > 0)
        {
            _context.OnClickDir(new(_context.DirHistory.Pop()), false);
        }
    }
}