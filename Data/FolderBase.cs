using System.ComponentModel.DataAnnotations;

namespace TagExplorer.Data;

public abstract class FolderBase
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; }

    public List<Folder>? SubFolders { get; set; } = [];

    public abstract string Path { get; set; }

    protected FolderBase()
    {
    }

    protected FolderBase(string name)
    {
        Name = name;
    }
}
