using TuiLib;
using FileExplorer.Context;
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
            _context.Menu.Items.FirstOrDefault(item => item.Item.Text == "..")?.CallOnClick();
        }
        
        _context.RedrawMenu();
    }
}