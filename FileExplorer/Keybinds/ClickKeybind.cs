using CmdMenu;

namespace FileExplorer.Keybinds;

public class ClickKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        string text = _context.Menu.SelectedItem?.Text ?? "..";
        if (text != ".." && continuous)
        {
            return;
        }
        
        _context.Menu.CallSelectedItemClick();
        
        if (text != "..")
        {
            _context.Menu.OnItemAdded += OnItemAdded;
            Task.Run(() =>
            {
                Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
                _context.Menu.OnItemAdded -= OnItemAdded;
            });
        }
    }

    private void OnItemAdded(MenuItem item)
    {
        if (item.Text == "..")
        {
            return;
        }

        _context.Menu.SelectedIndex = 1;
        _context.Menu.OnItemAdded -= OnItemAdded;
    }
}