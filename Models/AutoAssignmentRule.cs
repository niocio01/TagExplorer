using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

internal class AutoAssignmentRule : ITableObject
{
    public int? Id { get; set; }

    public int AssignmentType { get; private set; }

    public AutoAssignmentRule(int id)
    {
        Id = id;
    }
}

internal class FolderAssignmentRule : AutoAssignmentRule
{
    public Folder? Folder { get; protected set; }

    public FolderAssignmentRule(int id) : base(id) { }

    public FolderAssignmentRule(int id, Folder folder) : base(id)
    {
        Folder = folder;
    }
}