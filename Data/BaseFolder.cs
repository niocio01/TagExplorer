using System.ComponentModel.DataAnnotations;

namespace TagExplorer.Data;

public class BaseFolder : FolderBase
{
    [Required]
    [MaxLength(255)]
    public override String Path { get; set; }

    public BaseFolder()
    {
    }
    

    public BaseFolder(string path, string name)
    {
        Name = name;
        Path = path;
    }
}