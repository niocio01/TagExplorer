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
        { "xlsx", "FileExcelBox"},
        { "pdf", "FilePdfBox"},
        { "jpg", "FileJpgBox" },
        { "png", "FilePngBox" },
        { "svg", "FilePngBox" },
        { "mp4", "Video" },
        { "mp3", "MusicBox" },
        { "wav", "MusicBox" },
        { "zip", "ZipBox" },
        { "rar", "ZipBox" },
        { "stl", "printer3D" },
        { "3mf", "printer3D" },
        { "sldprt", "FileCadBox" },
        { "sldasm", "FileCadBox" },
        { "git", "gitignore" },
        { "md", "languageMarkdown"},
        { "json", "codeJson" },
        { "tex", "FormatText" },
    };
}

public abstract class ExplorerItem
{
    public string Name { get; init; }
    public string IconString { get; init; }

    public string Path { get; }
    // If null, this item is a root folder
    public Folder? ParentFolder { get; init; }
    public List<ExplorerItem> Children { get; init; } = new List<ExplorerItem>();
}

public class Folder : ExplorerItem
{
    // If null, this item not a root folder
    private string? _path;

    public bool HasParentFolder => ParentFolder != null;

    // root folder
    public Folder(string rootPath, string name)
    {
        _path = rootPath;
        Name = name;
        IconString = "FolderOutline";
        ParentFolder = null;
    }

    // sub folder
    public Folder(string name, Folder? parentFolder)
    {
        Name = name;
        IconString = "FolderOutline";
        ParentFolder = parentFolder;
    }

    public string Path
    {
        get
        {
            // sub folder
            if (_path == null)
            {
                return ParentFolder!.Path + System.IO.Path.DirectorySeparatorChar + Name;
            }
            // root folder
            else
            {
                return _path;
            }
        }
    }
}

public class File : ExplorerItem
{
    public File(string name, string fileExtension)
    {
        Name = name;
        try
        {
            ExplorerItemProperties.Symbols.TryGetValue(fileExtension[1..].ToLower(), out string? symbol);
            IconString = symbol ?? "help";
        }
        catch (Exception e)
        {
            IconString = "help";
        }
        
    }
}