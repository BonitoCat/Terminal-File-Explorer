using System.Diagnostics;

namespace FileExplorer;

public static class MimeHelper
{
    public static string? GetMimeType(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }
        
        Process proc = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "xdg-mime",
                Arguments = $"query filetype \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        proc.Start();
        string output = proc.StandardOutput.ReadToEnd().Trim();
        _ = proc.StandardError.ReadToEnd();
            
        proc.WaitForExit();
        return string.IsNullOrWhiteSpace(output) ? null : output;
    }
}