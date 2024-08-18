using Npgsql;

namespace TagExplorer.Models;

public class Tag : ITableObject
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Color? Color { get; set; }
    public string? Icon { get; set; }
    public bool? SystemTag { get; set; }

    public Tag(int id, string name, string? description, Color? color, string? icon, bool systemTag = false)
    {
        Id = id;
        Name = name;
        Description = description;
        Color = color;
        Icon = icon;
        SystemTag = systemTag;
    }

    public Tag(string name, string? description, Color? color, string? icon, bool systemTag = false)
    {
        Name = name;
        Description = description;
        Color = color;
        Icon = icon;
        SystemTag = systemTag;
    }

    public Tag(int id)
    {
        Id = id;
    }
}

public class TagTable() : Table<Tag>
(TableNames.Tags,
[
    new DbAttribute(0, "tag_id", "tag_id INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY", false),
    new DbAttribute(1, "name", "name VARCHAR"),
    new DbAttribute(2, "description", "description TEXT"),
    new DbAttribute(3, "system_tag", "system_tag BOOLEAN"),
    new DbAttribute(4, "icon_name", "icon_name VARCHAR(20)"),
    new DbAttribute(5, "color_id",
        $"color_id INT, CONSTRAINT fk_color FOREIGN KEY(color_id) REFERENCES {TableNames.Colors}(color_id)"),
])
{
    public override void GetMissingData()
    {
        if (DataIsFilled)
        {
            return;
        }

        throw new NotImplementedException();
    }

    public override void GetList()
    {
        Data.Clear();
        var conn = DBConnector.CreateConnection();
        conn.Open();
        string query = $"SELECT * FROM {TableName}";
        using var cmd = new NpgsqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Data.Add(new Tag(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                Main.ColorTable.GetObject(reader.GetInt32(5)),
                reader.GetString(4),
                reader.GetBoolean(3)
            ));
        }

        DataIsFilled = true;
    }

    public override void StoreNewData()
    {
        var conn = DBConnector.CreateConnection();
        conn.Open();
        foreach (Tag obj in Data.Where(x => x.Id is null))
        {
            string query = $"INSERT INTO {TableName} (";
            foreach (var attribute in Attributes.Where(x => x.WriteProperty))
            {
                query += $"{attribute.Name}, ";
            }

            query = query.Remove(query.Length - 2);
            query += ") VALUES (";
            foreach (var attribute in Attributes.Where(x => x.WriteProperty))
            {
                query += $"@{attribute.Name}, ";
            }

            query = query.Remove(query.Length - 2);
            query += ") RETURNING tag_id";
            using var cmd = new NpgsqlCommand(query, conn);

            cmd.Parameters.AddWithValue("name", obj.Name);
            cmd.Parameters.AddWithValue("description", obj.Description);
            cmd.Parameters.AddWithValue("color_id", obj.Color.Id);
            cmd.Parameters.AddWithValue("icon_name", obj.Icon);
            cmd.Parameters.AddWithValue("system_tag", obj.SystemTag);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                obj.Id = reader.GetInt32(0);
            }
        }
    }

    public override Tag GetObject(int id)
    {
        var existing = Data.Where(x => x.Id == id);
        if (existing.Any())
        {
            return existing.First();
        }

        Data.Add(new Tag(id));
        DataIsFilled = false;
        return Data.Last();
    }
}

public static class SystemTags
{
    public static void AddSystemTags()
    {
        Color orange = Main.ColorTable.Data.First(x => x.Name == "Orange");
        Main.TagTable.Data.Add(new Tag("Priority", "", orange, "Priority", true));
        Main.TagTable.Data.Add(new Tag("High", "High Priority", orange, "Priority", true));
        Main.TagTable.Data.Add(new Tag("Medium", "Medium Priority", orange, "Priority", true));
        Main.TagTable.Data.Add(new Tag("Low", "Low Priority", orange, "Priority", true));

        Color blue = Main.ColorTable.Data.First(x => x.Name == "Blue");
        Main.TagTable.Data.Add(new Tag("Status", "", blue, "Rule", true));
        Main.TagTable.Data.Add(new Tag("Not Started", "Not Started Status", blue, "Pending", true));
        Main.TagTable.Data.Add(new Tag("In Progress", "In Progress Status", blue, "FastForward", true));
        Main.TagTable.Data.Add(new Tag("Waiting", "In Progress, but waiting Status", blue, "Hourglass", true));
        Main.TagTable.Data.Add(new Tag("Completed", "Completed Status", blue, "Check", true));

        Main.TagTable.StoreNewData();
    }
}