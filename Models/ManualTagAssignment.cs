using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

internal class ManualTagAssignment : ITableObject
{
    public int? Id { get; set; }
    public Tag? Tag { get; private set; }
    public Folder? Folder { get; private set; }

    public ManualTagAssignment(int id)
    {
        Id = id;
    }

    public ManualTagAssignment(Tag tag, Folder folder)
    {
        Tag = tag;
        Folder = folder;
    }

    public ManualTagAssignment(int id, Tag tag, Folder folder)
    {
        Id = id;
        Tag = tag;
        Folder = folder;
    }
}