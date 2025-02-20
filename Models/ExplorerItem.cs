using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;


public static class ExplorerItemProperties
{
    public static Dictionary<string, string> Symbols = new()
    {
        { "exe", "Application" },
        { "txt", "FileDocument" },
        { "docx", "FileWordBox" },
        { "pdf", "FilePdfBox"},
        { "jpg", "FileJpgBox" },
        { "png", "FilePngBox" },
        { "mp4", "Video" },
        { "mp3", "MusicBox" },
        { "wav", "MusicBox" },
        { "zip", "ZipBox" },
        { "rar", "ZipBox" },
    };
}

public abstract class ExplorerItem
{
    public string Name { get; init; }
    public string IconString { get; init; }
}

public class Folder : ExplorerItem
{
    public Folder(string name)
    {
        Name = name;
        IconString = "FolderOutline";
    }
}

public class File : ExplorerItem
{
    public File(string name, string fileExtension)
    {
        Name = name;

        ExplorerItemProperties.Symbols.TryGetValue(fileExtension, out string? symbol);

        IconString = symbol ?? "help";
    }
}