using FileExplorer.Core.Models;

namespace FileExplorer.Core.Services;

public class FileSystemService
{
    public FolderContent GetFolderContent(string path)
    {
        FolderContent content = new()
        {
            Path = path,
        };
        
        
    }
}