using InputLib;

namespace FileExplorer;

public abstract class Keybind
{
    public List<Key> Keys = new();
    protected MenuContext _context;

    public Keybind(MenuContext context)
    {
        _context = context;
    }
    
    public Keybind(List<Key> keys, MenuContext context)
    {
        Keys = keys;
        _context = context;
    }

    public virtual void OnKeyDown(bool continuous) { }

    public virtual void OnKeyUp() { }
}