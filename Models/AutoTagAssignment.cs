using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagExplorer.Data;

namespace TagExplorer.Models;

internal class AutoTagAssignment
{
    public int? Id { get; set; }
    public Tag? Tag { get; private set; }
    public AutoAssignmentRule? AutoAssignmentRule { get; private set; }

    public AutoTagAssignment(int id)
    {
        Id = id;
    }

    public AutoTagAssignment(int id, Tag tag, AutoAssignmentRule rule)
    {
        Id = id;
        Tag = tag;
        AutoAssignmentRule = rule;
    }
}