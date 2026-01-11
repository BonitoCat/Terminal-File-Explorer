namespace FileExplorer.Keybinds;

public class NavDownKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        if (_context.Menu.SelectedIndex == _context.Menu.GetItemCount() - 1 && continuous)
        {
            return;
        }
            
        _context.Menu.MoveSelected(1);
    }
}