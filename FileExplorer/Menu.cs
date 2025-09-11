using System.Text;

namespace FileExplorer;

public class Menu
{
    private readonly object _itemsLock = new();
    private readonly List<MenuItem> _items = new();

    public int SelectedIndex { get; set; } = 0;
    
    public void AddItem(MenuItem item)
    {
        lock (_itemsLock)
        {
            _items.Add(item);
        }
    }
    
    public MenuItem? GetItemByName(string name)
    {
        lock (_itemsLock)
        {
            return _items.FirstOrDefault(item => item.Text == name);
        }
    }

    public MenuItem? GetItemAt(int index)
    {
        lock (_itemsLock)
        {
            if (index < 0 || index >= _items.Count)
            {
                return null;
            }
        
            return _items[index];   
        }
    }

    public int IndexOf(MenuItem? item)
    {
        lock (_itemsLock)
        {
            return _items.IndexOf(item);
        }
    }
    
    public int IndexOf(string name)
    {
        lock (_itemsLock)
        {
            return _items.Select(item => item.Text).ToList().IndexOf(name);
        }
    }

    public List<MenuItem> GetItems()
    {
        return _items;
    }

    public List<MenuItem> GetItemsClone()
    {
        lock (_itemsLock)
        {
            return new List<MenuItem>(_items);
        }
    }

    public int GetItemCount()
    {
        lock (_itemsLock)
        {
            return _items.Count;
        }
    }
    
    public void ClearItems()
    {
        lock (_itemsLock)
        {
            _items.Clear();
        }
    }

    public void MoveSelected(int dir)
    {
        dir = Math.Sign(dir);
        lock (_itemsLock)
        {
            SelectedIndex = (SelectedIndex + dir + _items.Count) % _items.Count;
        }
    }

    public bool CallSelectedItemClick()
    {
        return _items[SelectedIndex].CallClick();
    }
}