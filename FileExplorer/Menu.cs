namespace FileExplorer;

public class Menu
{
    private readonly object itemsLock = new();
    private List<MenuItem> Items { get; } = new();

    public int SelectedIndex { get; set; } = 0;

    public string GetMenuString()
    {
        lock (itemsLock)
        {
            string str = "";
            for (int i = 0; i < Items.Count; i++)
            {
                str += SelectedIndex == i ? AnsiColor.Reset + " > " : "   ";
                str += Items[i].Color + Items[i].Text + Items[i].Suffix + "\n";
            }

            return str;
        }
    }

    public void AddItem(MenuItem item)
    {
        lock (itemsLock)
        {
            Items.Add(item);
        }
    }
    
    public MenuItem GetItem(int index)
    {
        lock (itemsLock)
        {
            return Items[index];
        }
    }

    public int GetItemCount()
    {
        lock (itemsLock)
        {
            return Items.Count;
        }
    }
    
    public void ClearItems()
    {
        lock (itemsLock)
        {
            Items.Clear();
        }
    }

    public void MoveSelected(int dir)
    {
        dir = Math.Sign(dir);
        SelectedIndex = (SelectedIndex + dir + Items.Count) % Items.Count;
    }

    public bool CallSelectedItemClick()
    {
        return Items[SelectedIndex].CallClick();
    }
}