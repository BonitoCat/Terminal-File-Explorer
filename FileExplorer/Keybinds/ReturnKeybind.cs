using CmdMenu;

namespace FileExplorer.Keybinds;

public class ReturnKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        if (_context.SearchString != null || _context.SelectedItems.Count > 0)
        {
            if (_context.SearchString != null)
            {
                _context.SearchString = null;
                        
                Console.Clear();
                Task.Run(() =>
                {
                    _context.RefreshItems();
                });
            }
            if (_context.SelectedItems.Count > 0)
            {
                _context.SelectedItems.Clear();
            }
        }
        else
        {
            if (_context.Menu.GetItems().Where(item => item.Text == "..").ToList().Count == 0)
            {
                return;
            }
                        
            _context.Menu.SelectedIndex = 0;
            _context.OnClickDir(new MenuItem(".."));
        }
    }
}