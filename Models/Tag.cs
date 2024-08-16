using Npgsql;

namespace TagExplorer.Models;

public class Tag : ITableObject
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? ParentTagId { get; set; }
    public int? ColorId { get; set; }

    public Tag(int id, string name, string? description, int? parentTagId, int? colorId)
    {
        Id = id;
        Name = name;
        Description = description;
        ParentTagId = parentTagId;
        ColorId = colorId;
    }

    public Tag(string name, string? description, int? parentTagId, int? colorId)
    {
        Name = name;
        Description = description;
        ParentTagId = parentTagId;
        ColorId = colorId;
    }

    public Tag(int id)
    {
        Id = id;
    }
}

public class TagTable() : Table("tagTable", [
    new DbAttribute(0, "Id", typeof(int?), "INT"),
    new DbAttribute(1, "Name", typeof(string), "VARCHAR"),
    new DbAttribute(2, "Description", typeof(string), "TEXT"),
    new DbAttribute(3, "ParentTagId", typeof(int), "INT"),
    new DbAttribute(4, "ColorId", typeof(int), "INT")
])
{

    public override void GetData(List<ITableObject> idList)
    {
        throw new NotImplementedException();
    }

    public override List<ITableObject> GetList()
    {
        throw new NotImplementedException();
    }

    public override void StoreNewData(List<ITableObject> data)
    {
        throw new NotImplementedException();
    }
}