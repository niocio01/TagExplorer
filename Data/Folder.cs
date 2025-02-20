using System.ComponentModel.DataAnnotations;
using System.IO;

namespace TagExplorer.Data;

public class Folder : FolderBase
{
    public List<Folder>? Folders { get; set; } = [];

    public int ParentFolderId { get; private set; }
    public FolderBase ParentFolder { get; private set; }

    public override string Path
    {
        get => ParentFolder.Path + System.IO.Path.DirectorySeparatorChar + Name;
        set => throw new NotImplementedException();
    }


    public Folder()
    {
    }

    public Folder(string name, Folder parentFolder) : base(name)
    {
        ParentFolder = parentFolder;

    }

}