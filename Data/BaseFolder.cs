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

    public BaseFolder()
    {
    }
    

    public BaseFolder(string path, string name)
    {
        Name = name;
        Path = path;
    }
}