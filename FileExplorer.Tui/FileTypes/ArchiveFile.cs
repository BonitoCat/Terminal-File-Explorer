using System.Diagnostics;
using TuiLib;
using TuiLib.Controls;
using FileExplorer.Tui.Context;
using InputLib;

namespace FileExplorer.Tui.FileTypes;

public static class ArchiveFile
{
    public static readonly string[] ArchiveExtensions = [".zip", ".tar", ".tar.gz", ".tgz", ".tar.bz2", ".tar.xz"];
    
    public static void OnClick(IEntryContext context, TuiLabel sender)
    {
        lock (context.OutLock)
        {
            Console.CursorVisible = true;
            string? input;

            do
            {
                input = context.Input(
                    $"\x1b[2K\r{Color.Reset.ToAnsi()} Do you want to extract the content of '{sender.Text}'? [Y/n]: ",
                    enterNull: true, escapeNo: true)?.Trim();

                if (input == null)
                {
                    break;
                }
            } while (input != "y" && input != "n");
            
            Console.CursorVisible = false;
            if (input == "n")
            {
                Console.Clear();
                context.RedrawMenu();
                
                return;
            }
            
            ProcessStartInfo startInfo = new()
            {
                FileName = "tar",
            };
        
            if (sender.Text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = "unzip";
                startInfo.Arguments = $"-qn \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xf \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xzf \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xzf \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xjf \"{sender.Text}\"";
            }
            else if (sender.Text.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Arguments = $"-xJf \"{sender.Text}\"";
            }
            else
            {
                InputListener.DisableEcho();
                context.Listener?.StartListening();
            
                return;
            }
        
            Process? proc = Process.Start(startInfo);
            proc?.WaitForExit();
        }
        
        InputListener.DisableEcho();
        context.Listener?.StartListening();

        Console.Clear();
        context.RefreshItems();
    }
    
    public static bool IsArchive(string path)
    {
        return ArchiveExtensions.Any(ext => path.ToLower().EndsWith(ext));
    }
}