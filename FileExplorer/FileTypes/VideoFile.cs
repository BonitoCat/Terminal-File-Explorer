using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.Context;

namespace FileExplorer.FileTypes;

public static class VideoFile
{
    public static readonly string[] VideoExtensions =
    [
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".mpeg", ".mpg",
        ".m4v", ".webm", ".3gp", ".ts", ".mts", ".m2ts", ".f4v", ".ogv",
        ".rm", ".rv", ".vob", ".mxf", ".swf", ".drc", ".gif",
    ];
    
    public static void OnClick(MenuContext context, CmdLabel sender)
    {
        ImageFile.OnClick(context, sender);
    }
    
    public static bool IsVideo(string path)
    {
        return VideoExtensions.Contains(Path.GetExtension(path).ToLower());
    }
}