using System.Diagnostics;
using CmdMenu;

namespace FileExplorer.FileTypes;

public static class ImageFile
{
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff"];
    
    public static void OnClick(MenuContext context, MenuItem sender)
    {
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "xdg-open",
                Arguments = $"\"{sender.Text}\"",
                UseShellExecute = false,
            },
        };
        
        proc.Start();
    }
    
    public static bool IsImage(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path).ToLower());
    }
}