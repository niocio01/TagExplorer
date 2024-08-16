using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

public class Folder : ITableObject
{
    public int? Id { get; set; }
    public string? Name { get; private set; }
    public Folder? ParentFolder { get; private set; }

    public Folder(string name, Folder parentFolder)
    {
        Name = name;
        ParentFolder = parentFolder;
    }

    public Folder(int id)
    {
        Id = id;
    }

    public Folder(int id, string name, Folder parentFolder) : this(name, parentFolder)
    {
        Id = id;
    }

}