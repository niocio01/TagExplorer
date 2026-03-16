using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

public class FileType
{
    public string Extension { get; init; }
    public string IconString { get; init; }
    public string DisplayString { get; set; }
    private string[] SearchStrings { get; init; }

    public FileType(string extension, string iconString, string displayString, string[] searchStrings)
    {
        Extension = extension;
        IconString = iconString;
        DisplayString = displayString;
        SearchStrings = searchStrings;
    }

}

public static class FileTypes
{
    public static List<FileType> Types =
    [
        new("exe", "Application", "Executable", ["exe", "executable", "app", "application", "program"]),
        new("txt", "FileDocument", "Text File", ["txt", "text"]),
        new("docx", "FileWordBox", "Word Document", ["docx", "word", "document"]),
        new("xlsx", "FileExcelBox", "Excel Spreadsheet", ["xlsx", "excel", "spreadsheet", "table", "tabular"]),
        new("pptx", "FilePowerpointBox", "PowerPoint Presentation", ["pptx", "powerpoint", "presentation"]),
        new("csv", "FileExcelBox", "CSV File", ["csv", "spreadsheet"]),
        new("pdf", "FilePdfBox", "PDF Document", ["pdf", "document"]),
        new("jpg", "FileJpgBox", "JPEG Image", ["jpg", "jpeg", "image", "photo"]),
        new("png", "FilePngBox", "PNG Image", ["png", "image", "photo"]),
        new("svg", "Image", "SVG Image", ["svg", "vector", "image", "graphic"]),
        new("mp4", "Video", "MP4 Video", ["mp4", "video", "movie"]),
        new("mp3", "MusicBox", "MP3 Audio", ["mp3", "audio", "music", "song"]),
        new("wav", "MusicBox", "WAV Audio", ["wav", "audio", "music", "song"]),
        new("zip", "ZipBox", "ZIP Archive", ["zip", "rar", "archive", "compressed"]),
        new("rar", "ZipBox", "RAR Archive", ["rar", "zip", "archive", "compressed"]),
        new("stl", "Printer3D", "STL Model", ["stl", "3d", "model"]),
        new("3mf", "Printer3D", "3MF Model", ["3mf", "3d", "model"]),
        new("gcode", "Printer3D", "G-code", ["gcode", "3d", "cnc", "slicer"]),
        new("sldprt", "FileCadBox", "SolidWorks Part", ["sldprt", "cad", "solidworks", "part"]),
        new("sldasm", "FileCadBox", "SolidWorks Assembly", ["sldasm", "cad", "solidworks", "assembly"]),
        new("git", "Git", "Git Ignore", ["git", "gitignore"]),
        new("md", "LanguageMarkdown", "Markdown", ["md", "markdown", "readme"]),
        new("json", "CodeJson", "JSON File", ["json", "data", "config"]),
        new("tex", "FormatText", "LaTeX Document", ["tex", "latex", "pdf", "document"]),
        new("xml", "FileXmlBox", "XML File", ["xml", "markup", "data"]),
        new("html", "LanguageHTML5", "HTML File", ["html", "web", "markup", "website"]),
        new("php", "LanguagePhp", "PHP Script", ["php", "web", "script"]),
        new("css", "LanguageCss3", "CSS File", ["css", "stylesheet", "style"]),
        new("js", "LanguageJavascript", "JavaScript File", ["js", "javascript", "script", "web"]),
        new("ts", "LanguageTypescript", "TypeScript File", ["ts", "typescript", "script", "web"]),
        new("cpp", "LanguageCpp", "C++ Source", ["cpp", "c++", "source", "code", "embedded", "programming", "language"]),
        new("cs", "LanguageCSharp", "C# Source", ["cs", "csharp", "source", "code", "programming", "language"]),
        new("java", "LanguageJava", "Java Source", ["java", "source", "code", "programming", "language"]),
        new("py", "LanguagePython", "Python Script", ["py", "python", "script", "programming", "language"]),
        new("sql", "DatabaseSearch", "SQL Query", ["sql", "database", "query"]),
        new("log", "FileDocument", "Log File", ["log", "text", "record"]),
        new("bat", "FileCode", "Batch File", ["bat", "batch", "script"]),
        new("sh", "FileCode", "Shell Script", ["sh", "shell", "script"]),
        new("ini", "FileCode", "INI File", ["ini", "config", "init", "settings"]),
        new("yml", "FileCode", "YAML File", ["yml", "yaml", "config"]),
        new("yaml", "FileCode", "YAML File", ["yaml", "yml", "config"]),
        new("psd", "Image", "Photoshop File", ["psd", "photoshop", "image"]),
        new("ai", "Image", "Illustrator File", ["ai", "illustrator", "vector"]),
        new("bmp", "Image", "Bitmap Image", ["bmp", "bitmap", "image"]),
        new("ico", "Image", "Icon File", ["ico", "icon", "image"]),
        new("avi", "Video", "AVI Video", ["avi", "video", "movie"]),
        new("mov", "Video", "MOV Video", ["mov", "video", "movie"]),
        new("flv", "Video", "FLV Video", ["flv", "video", "flash"]),
        new("mkv", "Video", "MKV Video", ["mkv", "video", "movie"]),
        new("ogg", "MusicBox", "OGG Audio", ["ogg", "audio", "music"]),
        new("flac", "MusicBox", "FLAC Audio", ["flac", "audio", "music"]),
        new("aac", "MusicBox", "AAC Audio", ["aac", "audio", "music"]),
        new("m4a", "MusicBox", "M4A Audio", ["m4a", "audio", "music"]),

    ];

    public static readonly IReadOnlyDictionary<string, FileType> ByExtension =
        Types.ToDictionary(t => t.Extension, StringComparer.OrdinalIgnoreCase);
}

public abstract class ExplorerItem
{
    public string Name { get; init; }
    public string IconString { get; init; }

    public string Path { get; }
    // If null, this item is a root folder
    public Folder? ParentFolder { get; init; }
}

public class Folder : ExplorerItem
{
    // If null, this item not a root folder
    private string? _path;

    public bool HasParentFolder => ParentFolder != null;

    public List<ExplorerItem> Children { get; init; } = new List<ExplorerItem>();

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
    public string Extension { get; }
    public string? FullPath { get; }

    public File(string name, string fileExtension, string? fullPath = null)
    {
        Name = name;
        Extension = fileExtension.TrimStart('.').ToLowerInvariant();
        FullPath = fullPath;

        IconString = FileTypes.ByExtension.TryGetValue(Extension, out var type)
            ? type.IconString
            : "help";
    }
}