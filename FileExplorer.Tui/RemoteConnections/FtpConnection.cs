using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace FileExplorer.Tui.RemotePaths;

// Uses lftp which supports scripted FTP. Falls back to ftp for basic ops.
public class FtpConnection(RemoteConnectionInfo info) : IRemoteConnection
{
    public RemoteConnectionInfo Info        { get; } = info;
    public bool                 IsConnected { get; private set; }
    public string               CurrentPath { get; private set; } = "/";

    public Task ConnectAsync()
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public async Task<List<RemoteItem>> ListDirectoryAsync(string path)
    {
        CurrentPath = path;
        string output = await RunLftpAsync($"cls -l {EscapePath(path)}");
        return ParseClsOutput(output, path);
    }

    public async Task<string> ReadFileAsync(string path)
    {
        string tmp = Path.GetTempFileName();
        try
        {
            await DownloadAsync(path, tmp);
            return await File.ReadAllTextAsync(tmp);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    public async Task WriteFileAsync(string path, Stream data)
    {
        string tmp = Path.GetTempFileName();
        try
        {
            using FileStream fs = File.OpenWrite(tmp);
            await data.CopyToAsync(fs);
            await UploadAsync(tmp, path);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    public async Task DeleteAsync(string path, bool isDirectory)
    {
        string cmd = isDirectory ? $"rm -r {EscapePath(path)}" : $"rm {EscapePath(path)}";
        await RunLftpAsync(cmd);
    }

    public async Task RenameAsync(string oldPath, string newPath)
    {
        await RunLftpAsync($"mv {EscapePath(oldPath)} {EscapePath(newPath)}");
    }

    public async Task CreateDirectoryAsync(string path)
    {
        await RunLftpAsync($"mkdir -p {EscapePath(path)}");
    }

    public async Task DownloadAsync(string remotePath, string localPath, IProgress<double>? progress = null)
    {
        await RunLftpAsync($"get {EscapePath(remotePath)} -o {EscapePath(localPath)}");
    }

    public async Task UploadAsync(string localPath, string remotePath, IProgress<double>? progress = null)
    {
        await RunLftpAsync($"put {EscapePath(localPath)} -o {EscapePath(remotePath)}");
    }

    public void Dispose() { }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string BuildOpenCmd() =>
        $"open -u {Info.Username},{Info.Password ?? ""} ftp://{Info.Host}:{Info.Port}";

    private async Task<string> RunLftpAsync(string command)
    {
        // lftp runs commands via -c "open ...; command"
        string script = $"{BuildOpenCmd()}; {command}; quit";

        Process proc = new()
        {
            StartInfo = new()
            {
                FileName               = "lftp",
                Arguments              = $"-c \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            },
        };

        proc.Start();

        StringBuilder output = new();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        proc.BeginOutputReadLine();

        await proc.WaitForExitAsync();
        return output.ToString();
    }

    private static string EscapePath(string path) => $"'{path.Replace("'", "'\\''")}'";

    // lftp cls -l produces similar output to ls -l
    private static readonly Regex ClsRegex = new(
        @"^([dlrwxst\-]{10})\s+\d+\s+\S+\s+\S+\s+(\d+)\s+\S+\s+\S+\s+\S+\s+(.+)$",
        RegexOptions.Compiled);

    private static List<RemoteItem> ParseClsOutput(string output, string basePath)
    {
        List<RemoteItem> items = new();

        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Match m = ClsRegex.Match(line);
            if (!m.Success) continue;

            string perms = m.Groups[1].Value;
            long   size  = long.TryParse(m.Groups[2].Value, out long s) ? s : 0;
            string name  = m.Groups[3].Value.TrimEnd();
            bool   isDir = perms[0] == 'd';

            if (name is "." or "..") continue;

            string fullPath = basePath.TrimEnd('/') + '/' + name;
            items.Add(new RemoteItem(name, fullPath, isDir, size, DateTime.MinValue, perms));
        }

        return items;
    }
}
