using CmdMenu;

namespace FileExplorer.FileTypes;

public static class AudioFile
{
    public static readonly string[] AudioExtensions =
    [
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".oga", ".wma",
        ".m4a", ".alac", ".aiff", ".aif", ".opus", ".pcm", ".amr",
        ".mid", ".midi", ".caf", ".dsd",
    ];
    
    public static void OnClick(MenuContext context, MenuItem sender)
    {
        ImageFile.OnClick(context, sender);
    }
    
    public static bool IsAudio(string path)
    {
        return AudioExtensions.Contains(Path.GetExtension(path).ToLower());
    }
}