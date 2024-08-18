using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

public abstract class TagAssignment : ITableObject
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

public class FolderTagAssignmentTable() : Table<FolderTagAssignment>
("TagAssignment",
[
    new DbAttribute(0, "tag_assignment_id", "tag_assignment_id SERIAL"),
    new DbAttribute(1, "relation_type", "relation_type INT"),
    new DbAttribute(2, "fk_tag", $"CONSTRAINT fk_tag FOREIGN KEY(tag_id) REFERENCES {TableNames.Tags}(tag_id)"),
    new DbAttribute(3, "fk_folder",
        $"CONSTRAINT fk_folder FOREIGN KEY(folder_id) REFERENCES {TableNames.Folders}(folder_id)"),
])
{
    public override void GetMissingData()
    {
        throw new NotImplementedException();
    }

    public override void GetList()
    {
        throw new NotImplementedException();
    }

    public override void StoreNewData()
    {
        throw new NotImplementedException();
    }

    public override FolderTagAssignment GetObject(int id)
    {
        throw new NotImplementedException();
    }
}