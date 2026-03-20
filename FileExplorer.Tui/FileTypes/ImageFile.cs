using System.Diagnostics;
using FileExplorer.Tui.Context;
using TuiLib.Controls;

namespace FileExplorer.Tui.FileTypes;

public static class ImageFile
{
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff"];
    
    public static void OnClick(IEntryContext context, TuiLabel sender)
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