using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

public class Color : ITableObject
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? HexCode { get; set; }

    public Color(int id, string name, string hexCode)
    {
        Id = id;
        Name = name;
        HexCode = hexCode;
    }

    public Color(string name, string hexCode)
    {
        Name = name;
        HexCode = hexCode;
    }

    public Color(int id)
    {
        Id = id;
    }
}

public class ColorTable() : Table<Color>
(TableNames.Colors,
[
    new DbAttribute(0, "color_id", "color_id INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY", false),
    new DbAttribute(1, "name", "name TEXT"),
    new DbAttribute(2, "hex_code", "hex_code VARCHAR(7)")
])
{
    public override void GetMissingData()
    {
        if (DataIsFilled)
        {
            return;
        }

        foreach (Color color in Data.Where(x => x.HexCode == null))
        {
            var conn = DBConnector.CreateConnection();
            conn.Open();
            string query = $"SELECT * FROM {TableName} WHERE color_id = {color.Id}";
            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                color.Name = reader.GetString(1);
                color.HexCode = reader.GetString(2);
            }
        }

        DataIsFilled = true;
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
            Data.Add(new Color(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        DataIsFilled = true;
    }

    public override void StoreNewData()
    {
        var conn = DBConnector.CreateConnection();
        conn.Open();
        foreach (Color obj in Data.Where(x => x.Id is null))
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
            query += ") RETURNING color_id";
            using var cmd = new NpgsqlCommand(query, conn);

            cmd.Parameters.AddWithValue("Name", obj.Name);
            cmd.Parameters.AddWithValue("hex_code", obj.HexCode);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                obj.Id = reader.GetInt32(0);
            }
        }
    }

    public override Color GetObject(int id)
    {
        var existing = Data.Where(x => x.Id == id);
        if (existing.Count() == 1)
        {
            return existing.First();
        }

        Data.Add(new Color(id));
        DataIsFilled = false;
        return Data.Last();
    }
}

public static class DefaultColors
{
    public static void AddDefaultColors(ColorTable table)
    {
        table.Data.Add(new Color("Red", "#F44336"));
        table.Data.Add(new Color("Pink", "#E91E63"));
        table.Data.Add(new Color("Purple", "#9C27B0"));
        table.Data.Add(new Color("DeepPurple", "#673AB7"));
        table.Data.Add(new Color("Indigo", "#3F51B5"));
        table.Data.Add(new Color("Blue", "#2196F3"));
        table.Data.Add(new Color("LightBlue", "#03A9F4"));
        table.Data.Add(new Color("Cyan", "#00BCD4"));
        table.Data.Add(new Color("Teal", "#009688"));
        table.Data.Add(new Color("Green", "#4CAF50"));
        table.Data.Add(new Color("LightGreen", "#8BC34A"));
        table.Data.Add(new Color("Lime", "#CDDC39"));
        table.Data.Add(new Color("Yellow", "#FFEB3B"));
        table.Data.Add(new Color("Amber", "#FFC107"));
        table.Data.Add(new Color("Orange", "#FF9800"));
        table.Data.Add(new Color("DeepOrange", "#FF5722"));
        table.Data.Add(new Color("Brown", "#795548"));
        table.Data.Add(new Color("Grey", "#9E9E9E"));
        table.Data.Add(new Color("BlueGrey", "#607D8B"));

        table.StoreNewData();
    }
}