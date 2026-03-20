namespace FileExplorer.Tui.RemotePaths;

public record RemoteItem(string Name, string FullPath, bool IsDirectory, long Size, DateTime Modified, string Permissions);

public record RemoteConnectionInfo(RemoteProtocol Protocol, string Host, int Port, string Username, string? Password = null);

public enum RemoteProtocol { Sftp, Ftp, Smb }

public interface IRemoteConnection : IDisposable
{
    RemoteConnectionInfo Info { get; }
    bool IsConnected { get; }
    string CurrentPath { get; }

    Task ConnectAsync();
    Task DisconnectAsync();

    Task<List<RemoteItem>> ListDirectoryAsync(string path);
    Task<string> ReadFileAsync(string path);
    Task WriteFileAsync(string path, Stream data);
    Task DeleteAsync(string path, bool isDirectory);
    Task RenameAsync(string oldPath, string newPath);
    Task CreateDirectoryAsync(string path);
    Task DownloadAsync(string remotePath, string localPath, IProgress<double>? progress = null);
    Task UploadAsync(string localPath, string remotePath, IProgress<double>? progress = null);

    static IRemoteConnection Create(RemoteConnectionInfo info) => info.Protocol switch
    {
        RemoteProtocol.Sftp => new SftpConnection(info),
        RemoteProtocol.Ftp => new FtpConnection(info),
        RemoteProtocol.Smb => new SmbConnection(info),
        _ => throw new NotSupportedException($"Protocol {info.Protocol} is not supported"),
    };
}