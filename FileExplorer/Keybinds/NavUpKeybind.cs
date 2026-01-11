namespace FileExplorer.Keybinds;

public class NavUpKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        if (_context.Menu.SelectedIndex == 0 && continuous)
        {
            return;
        }
            
        _context.Menu.MoveSelected(-1);
    }
}