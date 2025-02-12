using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

public abstract class TagAssignment
{
    public enum RelationType
    {
        Folder,
        File,
        Parent,
        Tag,
    }

    public int? Id { get; set; }
    public RelationType? Type { get; protected set; }
    public int? TagId { get; protected set; }
    public int? RelationId { get; protected set; }
}

public class FolderTagAssignment : TagAssignment
{
    public FolderTagAssignment(int tagId, int folderId)
    {
        TagId = tagId;
        RelationId = folderId;
        Type = RelationType.Folder;
    }

    public FolderTagAssignment(int id, int tagId, int folderId) : this(tagId, folderId)
    {
        Id = id;
    }
}
