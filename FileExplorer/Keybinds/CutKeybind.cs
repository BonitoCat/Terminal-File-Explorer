using TuiLib;
using TuiLib.Controls;
using FileExplorer.Context;

namespace FileExplorer.Keybinds;

public class CutKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        string[] paths = [];
        lock (_context.Menu.Lock)
        {
            if (_context.SelectedItems.Count > 0)
            {
                paths = _context.SelectedItems
                                .Select(item => item.Data.GetValueOrDefault("FullPath", ""))
                                .Where(path => !string.IsNullOrEmpty(path))
                                .ToArray();

                _context.SelectedItems.RemoveAll(item => paths.Contains(item.Data.GetValueOrDefault("FullPath", "")));
            }
            else
            {
                CmdListBoxItem<CmdLabel>? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (item == null || item.Item.Text == "..")
                {
                    return;
                }

                if (item.Data.TryGetValue("FullPath", out string? path))
                {
                    paths = [path];
                }
            }
            
            _context.ClipboardContext.Items.Clear();
            _context.ClipboardContext.Items.AddRange(paths);
            _context.ClipboardContext.Mode = ClipboardMode.Cut;

            Clipboard.WritePaths(ClipboardMode.Cut, paths);
        }

        _context.RedrawMenu();
    }
    
}