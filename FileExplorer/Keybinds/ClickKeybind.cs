using TuiLib.Controls;
using FileExplorer.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Keybinds;

public class ClickKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (_context.Menu.SelectedItem is not CmdListBoxItem<CmdLabel> selectedLabel)
        {
            return;
        }
        
        string text = selectedLabel.Item.Text ?? "..";
        if (_context.Menu.SelectedItem?.Data.TryGetValue("DestinationPath", out string? destPath) ?? false)
        {
            _context.OnClickDir(new(destPath));
        }
        else
        {
            if (text != ".." && e.Continuous)
            {
                return;
            }
            
            Logger.LogI("Opened file");
            _context.Menu.CallSelectedItemClick();
        }
        
        if (text != "..")
        {
            _context.Menu.OnItemAdded += OnItemAdded;
            Task.Run(() =>
            {
                Task.Delay(100).Wait();
                _context.Menu.OnItemAdded -= OnItemAdded;
            });
        }
    }

    private void OnItemAdded(CmdListBoxItem<CmdLabel> item)
    {
        if (item.Item.Text == "..")
        {
            return;
        }

        _context.Menu.SelectedIndex = 1;
        _context.Menu.OnItemAdded -= OnItemAdded;
    }
}