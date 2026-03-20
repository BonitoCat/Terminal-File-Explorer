using FileExplorer.Tui.Context;
using InputLib;
using InputLib.EventArgs;

namespace FileExplorer.Tui;

public abstract class Keybind
{
    public List<Key> Keys = new();
    protected IEntryContext _context;

    public Keybind(IEntryContext context)
    {
        _context = context;
    }
    
    public Keybind(List<Key> keys, IEntryContext context)
    {
        Keys = keys;
        _context = context;
    }

    public virtual void OnKeyDown(KeyDownEventArgs e) { }

    public virtual void OnKeyUp() { }
    
    public virtual void OnKeyJustPressed() { }
}