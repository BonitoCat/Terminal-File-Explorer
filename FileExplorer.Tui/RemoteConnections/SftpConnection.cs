using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace FileExplorer.Tui.RemotePaths;

public class SftpConnection(RemoteConnectionInfo info) : IRemoteConnection
{
    public RemoteConnectionInfo Info { get; } = info;
    public bool IsConnected { get; private set; }
    public string CurrentPath { get; private set; } = "/";

    private Process? _sshProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly SemaphoreSlim _cmdLock = new(1, 1);
    private const string Sentinel = "<<<FE_DONE_$?>>>";
    private static readonly bool SshPassAvailable = CheckCommandAvailable("sshpass");
    
    private static readonly Regex LsRegex = new(
        @"^([dlrwxst\-]{10})\s+\d+\s+\S+\s+\S+\s+(\d+)\s+(\d{4}-\d{2}-\d{2})\s+(\d{2}:\d{2})\s+(.+)$",
        RegexOptions.Compiled);

    public async Task ConnectAsync()
    {
        string sshArgs = BuildSshArgs();

        string exe  = SshPassAvailable && Info.Password != null ? "sshpass" : "ssh";
        string args = SshPassAvailable && Info.Password != null
            ? $"-p '{Info.Password}' ssh {sshArgs}"
            : sshArgs;

        ProcessStartInfo startInfo = new()
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (SshPassAvailable && Info.Password != null)
        {
            startInfo.FileName  = "sshpass";
            startInfo.Arguments = $"-e ssh {sshArgs}";
            startInfo.Environment["SSHPASS"] = Info.Password;
        }
        
        _sshProcess = new() { StartInfo = startInfo };
        _sshProcess.Start();

        _stdin  = _sshProcess.StandardInput;
        _stdout = _sshProcess.StandardOutput;

        string test = await RunCommandAsync("echo connected");
        if (!test.Contains("connected"))
        {
            string err = await _sshProcess.StandardError.ReadToEndAsync();
            throw new Exception($"SSH handshake failed: {err}");
        }

        IsConnected = true;
        Debug.WriteLine("[SSH] connected");
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        try
        {
            _stdin?.WriteLine("exit");
            _sshProcess?.WaitForExit(2000);
            _sshProcess?.Kill();
        }
        catch { }
        
        return Task.CompletedTask;
    }

    public async Task<List<RemoteItem>> ListDirectoryAsync(string path)
    {
        CurrentPath = path;
        string output = await RunCommandAsync($"ls -la --time-style=long-iso {EscapeRemotePath(path)}");
        Debug.WriteLine($"[ls] {output}");
        
        return ParseLsOutput(output, path);
    }

    public async Task<string> ReadFileAsync(string path)
    {
        return await RunCommandAsync($"cat {EscapeRemotePath(path)}");
    }

    public async Task WriteFileAsync(string path, Stream data)
    {
        using MemoryStream ms = new();
        await data.CopyToAsync(ms);
        
        string content = Encoding.UTF8.GetString(ms.ToArray());
        string escaped = content.Replace("'", "'\\''");
        
        await RunCommandAsync($"printf '%s' '{escaped}' > {EscapeRemotePath(path)}");
    }

    public async Task DeleteAsync(string path, bool isDirectory)
    {
        string cmd = isDirectory ? $"rm -rf {EscapeRemotePath(path)}" : $"rm -f {EscapeRemotePath(path)}";
        await RunCommandAsync(cmd);
    }

    public async Task RenameAsync(string oldPath, string newPath)
    {
        await RunCommandAsync($"mv {EscapeRemotePath(oldPath)} {EscapeRemotePath(newPath)}");
    }

    public async Task CreateDirectoryAsync(string path)
    {
        await RunCommandAsync($"mkdir -p {EscapeRemotePath(path)}");
    }

    public async Task DownloadAsync(string remotePath, string localPath, IProgress<double>? progress = null)
    {
        await RunDetachedAsync("scp", BuildScpArgs($"{Info.Username}@{Info.Host}:{remotePath} {localPath}"));
    }

    public async Task UploadAsync(string localPath, string remotePath, IProgress<double>? progress = null)
    {
        await RunDetachedAsync("scp", BuildScpArgs($"{localPath} {Info.Username}@{Info.Host}:{remotePath}"));
    }

    public void Dispose()
    {
        try
        {
            _stdin?.Close();
            _sshProcess?.Kill();
            _sshProcess?.Dispose();
        }
        catch { }
    }

    private async Task<string> RunCommandAsync(string command)
    {
        await _cmdLock.WaitAsync();
        try
        {
            await SendRawAsync(command);
            return await ReadUntilSentinelAsync();
        }
        finally
        {
            _cmdLock.Release();
        }
    }

    private async Task SendRawAsync(string command)
    {
        await _stdin!.WriteLineAsync($"{command}; echo '{Sentinel}'");
        await _stdin.FlushAsync();
    }

    private async Task<string> ReadUntilSentinelAsync()
    {
        StringBuilder sb = new();
        while (true)
        {
            string? line = await _stdout!.ReadLineAsync();
            if (line == null)
            {
                break;
            }

            if (line.Contains(Sentinel))
            {
                break;
            }
            
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private async Task<string> RunDetachedAsync(string exe, string args)
    {
        string actualExe  = SshPassAvailable && Info.Password != null ? "sshpass" : exe;
        string actualArgs = SshPassAvailable && Info.Password != null
            ? $"-p '{Info.Password}' {exe} {args}"
            : args;

        ProcessStartInfo startInfo = new()
        {
            FileName = actualExe,
            Arguments = actualArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process proc = new() { StartInfo = startInfo };
        proc.Start();

        Task<string> stdoutTask = proc.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = proc.StandardError.ReadToEndAsync();
        
        await Task.WhenAll(stdoutTask, stderrTask);
        await proc.WaitForExitAsync();

        Debug.WriteLine($"[{exe}] exit={proc.ExitCode} stderr='{stderrTask.Result}'");
        return stdoutTask.Result;
    }

    private string BuildSshArgs() =>
        $"-p {Info.Port} -o StrictHostKeyChecking=no {Info.Username}@{Info.Host}";
    
    private string BuildScpArgs(string extra) =>
        $"-P {Info.Port} -o StrictHostKeyChecking=no {extra}";

    private static bool CheckCommandAvailable(string command)
    {
        try
        {
            Process proc = new()
            {
                StartInfo = new()
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            proc.Start();
            proc.WaitForExit();

            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeRemotePath(string path) =>
        $"'{path.Replace("'", "'\\''")}'";

    private static List<RemoteItem> ParseLsOutput(string output, string basePath)
    {
        List<RemoteItem> items = [];
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("total "))
            {
                continue;
            }

            Match m = LsRegex.Match(line);
            if (!m.Success)
            {
                continue;
            }

            string perms = m.Groups[1].Value;
            long size = long.TryParse(m.Groups[2].Value, out long s) ? s : 0;
            string date = $"{m.Groups[3].Value} {m.Groups[4].Value}";
            string name = m.Groups[5].Value.TrimEnd();
            bool isDir = perms[0] == 'd';
            bool isLink = perms[0] == 'l';

            int arrow = name.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0)
            {
                name = name[..arrow];
            }

            if (name is "." or "..")
            {
                continue;
            }

            DateTime.TryParse(date, out DateTime modified);
            string fullPath = basePath.TrimEnd('/') + '/' + name;

            items.Add(new RemoteItem(name, fullPath, isDir || isLink, size, modified, perms));
        }

        return items;
    }
}