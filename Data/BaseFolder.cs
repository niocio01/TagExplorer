namespace TagExplorer.Models;

public class BaseFolder 
{
    public int? Id { get; set; }
    
    public String? Name { get; set; }

    public String? Path { get; set; }

    public bool Selected { get; set; } = false;

    public BaseFolder(int id, string path, string name) : this(path, name)
    {
        Id = id;
    }

    public BaseFolder(int id)
    {
        Id = id;
    }

    public BaseFolder(string path, string name)
    {
        Name = name;
        Path = path;
    }
}