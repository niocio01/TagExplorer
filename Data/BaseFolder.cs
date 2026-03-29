using System.ComponentModel.DataAnnotations;

namespace TagExplorer.Data;

public class BaseFolder
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; }

    [Required]
    [MaxLength(255)]
    public string Path { get; set; }

    public int DirectoryCount { get; set; }

    public int FileCount { get; set; }

    public int MaxDepth { get; set; }

    public DateTime? StatsTimestampUtc { get; set; }

    public BaseFolder()
    {
    }
    

    public BaseFolder(
        string path,
        string name,
        int directoryCount = 0,
        int fileCount = 0,
        int maxDepth = 0,
        DateTime? statsTimestampUtc = null)
    {
        Name = name;
        Path = path;
        DirectoryCount = directoryCount;
        FileCount = fileCount;
        MaxDepth = maxDepth;
        StatsTimestampUtc = statsTimestampUtc;
    }
}