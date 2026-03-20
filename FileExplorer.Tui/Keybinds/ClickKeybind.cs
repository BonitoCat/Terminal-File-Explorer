using TuiLib.Controls;
using FileExplorer.Tui.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class ClickKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (_context.Menu.SelectedItem is not TuiListBoxItem<TuiLabel> selectedLabel)
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

    private void OnItemAdded(TuiListBoxItem<TuiLabel> item)
    {
        if (item.Item.Text == "..")
        {
            return;
        }

        _context.Menu.SelectedIndex = 1;
        _context.Menu.OnItemAdded -= OnItemAdded;
    }
}