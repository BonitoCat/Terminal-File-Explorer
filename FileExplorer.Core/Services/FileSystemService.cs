using FileExplorer.Core.Entries;
using FileExplorer.Core.Interfaces;
using FileExplorer.Core.Utilities;

namespace FileExplorer.Core.Services;

public class FileSystemService
{
    public IEnumerable<IFileSystemEntry> EnumerateFolderEntries(string path, CancellationToken token = default)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }
        
        foreach (string dirPath in Directory.EnumerateDirectories(path))
        {
            token.ThrowIfCancellationRequested();
            
            FolderEntry folder = new()
            {
                FullName = Path.GetFileName(dirPath),
                FullPath = dirPath,
            };

            UpdateAttributes(folder);
            yield return folder;
        }

        foreach (string filePath in Directory.EnumerateFiles(path))
        {
            token.ThrowIfCancellationRequested();
            
            FileEntry file = new()
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Extension = Path.GetExtension(filePath),
                FullName = Path.GetFileName(filePath),
                FullPath = filePath,
            };

            UpdateAttributes(file);
            PopulateFileMetadataFast(file);
            
            yield return file;
        }
    }

    private static void UpdateAttributes(IFileSystemEntry entry)
    {
        switch (entry)
        {
            case FileEntry file:
            {
                FileInfo info = new(file.FullPath);

                file.IsHidden = (info.Attributes & FileAttributes.Hidden) != 0;
                file.IsEncrypted = (info.Attributes & FileAttributes.Encrypted) != 0;
                file.IsCompressed = (info.Attributes & FileAttributes.Compressed) != 0;
                file.IsReadOnly = info.IsReadOnly;
                file.IsSystem = (info.Attributes & FileAttributes.System) != 0;

                file.CreationTime = info.CreationTime;
                file.LastAccessTime = info.LastAccessTime;
                file.LastWriteTime = info.LastWriteTime;
                
                file.FileSize = info.Length;
                
                break;
            }

            case FolderEntry folder:
            {
                DirectoryInfo info = new(folder.FullPath);

                folder.IsHidden = (info.Attributes & FileAttributes.Hidden) != 0;
                folder.IsEncrypted = (info.Attributes & FileAttributes.Encrypted) != 0;
                folder.IsCompressed = (info.Attributes & FileAttributes.Compressed) != 0;
                folder.IsReadOnly = (info.Attributes & FileAttributes.ReadOnly) != 0;
                folder.IsSystem = (info.Attributes & FileAttributes.System) != 0;

                folder.CreationTime = info.CreationTime;
                folder.LastAccessTime = info.LastAccessTime;
                folder.LastWriteTime = info.LastWriteTime;
                
                break;
            }
        }
    }
    
    private static void UpdateFolder(string path)
    {
        
    }

    private static void PopulateFileMetadataFast(FileEntry file)
    {
        if (FileUtils.IsFileExecutable(file.FullPath))
        {
            file.FileType = FileType.Executable;
            return;
        }

        string? mime = MimeUtils.GetMimeTypeFast(file.FullPath);
        if (mime is null)
        {
            file.NeedsAccurateTypeCheck = true;
            file.FileType = FileType.Unknown;
            
            return;
        }

        file.FileType = mime switch
        {
            { } s when s.StartsWith("text/") => FileType.Text,
            { } s when s.StartsWith("image/") => FileType.Image,
            { } s when s.StartsWith("audio/") => FileType.Audio,
            { } s when s.StartsWith("video/") => FileType.Video,
            "application/vnd.debian.binary-package" => FileType.Deb,
            _ => FileType.Unknown,
        };

        if (file.FileType == FileType.Unknown)
        {
            file.NeedsAccurateTypeCheck = true;
        }
    }

    private static void PopulateFileMetadataAccurate(FileEntry file)
    {
        string? mime = MimeUtils.GetMimeTypeAccurate(file.FullPath);
        switch (mime)
        {
            case { } s when s.StartsWith("text/"):
                file.FileType = FileType.Text;
            break;
            
            case { } s when s.StartsWith("image/"):
                file.FileType = FileType.Image;
            break;
            
            case { } s when s.StartsWith("audio/"):
                file.FileType = FileType.Audio;
            break;
            
            case { } s when s.StartsWith("video/"):
                file.FileType = FileType.Video;
            break;
            
            case "application/vnd.debian.binary-package":
                file.FileType = FileType.Deb;
            break;
            
            default:
                file.FileType = FileType.Unknown;
            break;
        }

        file.IsAccurateType = true;
    }
}