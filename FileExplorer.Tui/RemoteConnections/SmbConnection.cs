using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace FileExplorer.Tui.RemotePaths;

// Uses smbclient (Linux) or native UNC paths (Windows)
public class SmbConnection(RemoteConnectionInfo info) : IRemoteConnection
{
    public RemoteConnectionInfo Info        { get; } = info;
    public bool                 IsConnected { get; private set; }
    public string               CurrentPath { get; private set; } = "/";

    // SMB share is encoded in the host: "server/sharename"
    private string Server => Info.Host.Contains('/') ? Info.Host.Split('/')[0] : Info.Host;
    private string Share  => Info.Host.Contains('/') ? Info.Host.Split('/')[1] : "default";

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

        if (OperatingSystem.IsWindows())
        {
            return ListDirectoryWindows(path);
        }

        string smbPath = ToSmbPath(path);
        string output  = await RunSmbClientAsync($"ls {smbPath}*");
        return ParseSmbLsOutput(output, path);
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
        if (OperatingSystem.IsWindows())
        {
            string uncPath = ToUncPath(path);
            if (isDirectory) Directory.Delete(uncPath, true);
            else             File.Delete(uncPath);
            return;
        }

        string cmd = isDirectory ? $"rmdir {ToSmbPath(path)}" : $"rm {ToSmbPath(path)}";
        await RunSmbClientAsync(cmd);
    }

    public async Task RenameAsync(string oldPath, string newPath)
    {
        if (OperatingSystem.IsWindows())
        {
            File.Move(ToUncPath(oldPath), ToUncPath(newPath));
            return;
        }

        await RunSmbClientAsync($"rename {ToSmbPath(oldPath)} {ToSmbPath(newPath)}");
    }

    public async Task CreateDirectoryAsync(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(ToUncPath(path));
            return;
        }

        await RunSmbClientAsync($"mkdir {ToSmbPath(path)}");
    }

    public async Task DownloadAsync(string remotePath, string localPath, IProgress<double>? progress = null)
    {
        if (OperatingSystem.IsWindows())
        {
            File.Copy(ToUncPath(remotePath), localPath, true);
            return;
        }

        await RunSmbClientAsync($"get {ToSmbPath(remotePath)} {EscapeLocalPath(localPath)}");
    }

    public async Task UploadAsync(string localPath, string remotePath, IProgress<double>? progress = null)
    {
        if (OperatingSystem.IsWindows())
        {
            File.Copy(localPath, ToUncPath(remotePath), true);
            return;
        }

        await RunSmbClientAsync($"put {EscapeLocalPath(localPath)} {ToSmbPath(remotePath)}");
    }

    public void Dispose() { }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Converts internal path (/dir/file) to smbclient path (\dir\file)
    private static string ToSmbPath(string path) =>
        path.Replace('/', '\\').TrimStart('\\');

    // Converts to Windows UNC path: \\server\share\path
    private string ToUncPath(string path) =>
        $@"\\{Server}\{Share}\{path.Replace('/', '\\').TrimStart('\\')}";

    private static string EscapeLocalPath(string path) => $"\"{path}\"";

    private string BuildSmbArgs() =>
        $"//{Server}/{Share} -U {Info.Username}%{Info.Password ?? ""} -p {Info.Port}";

    private async Task<string> RunSmbClientAsync(string command)
    {
        Process proc = new()
        {
            StartInfo = new()
            {
                FileName               = "smbclient",
                Arguments              = $"{BuildSmbArgs()} -c \"{command}\"",
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

    // Windows — just use System.IO directly on the UNC path
    private List<RemoteItem> ListDirectoryWindows(string path)
    {
        string uncPath = ToUncPath(path);
        List<RemoteItem> items = new();

        foreach (string dir in Directory.EnumerateDirectories(uncPath))
        {
            DirectoryInfo di = new(dir);
            items.Add(new RemoteItem(di.Name, path.TrimEnd('/') + '/' + di.Name, true, 0, di.LastWriteTime, "d---------"));
        }

        foreach (string file in Directory.EnumerateFiles(uncPath))
        {
            FileInfo fi = new(file);
            items.Add(new RemoteItem(fi.Name, path.TrimEnd('/') + '/' + fi.Name, false, fi.Length, fi.LastWriteTime, "----------"));
        }

        return items;
    }

    // smbclient ls output:
    //   filename                    A/D  size  date time year
    private static readonly Regex SmbLsRegex = new(
        @"^\s+(.+?)\s+(A|D|H|S|R|N)\s+(\d+)\s+\w+\s+\w+\s+\d+\s+\d{2}:\d{2}:\d{2}\s+\d{4}$",
        RegexOptions.Compiled);

    private static List<RemoteItem> ParseSmbLsOutput(string output, string basePath)
    {
        List<RemoteItem> items = new();

        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Match m = SmbLsRegex.Match(line);
            if (!m.Success) continue;

            string name  = m.Groups[1].Value.TrimEnd();
            string attr  = m.Groups[2].Value;
            long   size  = long.TryParse(m.Groups[3].Value, out long s) ? s : 0;
            bool   isDir = attr == "D";

            if (name is "." or "..") continue;

            string fullPath = basePath.TrimEnd('/') + '/' + name;
            items.Add(new RemoteItem(name, fullPath, isDir, size, DateTime.MinValue, isDir ? "d---------" : "----------"));
        }

        return items;
    }
}
