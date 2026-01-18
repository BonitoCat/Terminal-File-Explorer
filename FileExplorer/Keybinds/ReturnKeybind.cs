using CmdMenu;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class ReturnKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (_context.SearchString != null || _context.SelectedItems.Count > 0)
        {
            if (_context.SearchString != null)
            {
                _context.SearchString = null;
                        
                Console.Clear();
                _context.RefreshItems();
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
        
        _context.RedrawMenu();
    }
}