namespace FileExplorer;

public class MenuItem
{
    public string Text { get; set; }
    public string Suffix { get; set; }
    public string Color { get; set; }
    public Action? OnClickListener { get; set; }

    public MenuItem(string text, string color = AnsiColor.Reset, Action? onClickListener = null)
    {
        Text = text;
        Color = color;
        OnClickListener = onClickListener;
    }
    
    public bool CallClick()
    {
        if (OnClickListener == null) return false;
        OnClickListener?.Invoke();
        
        return true;
    }
}