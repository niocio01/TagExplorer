using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

public class BaseFolder : ITableObject
{
    public int? Id { get; set; }

    public String? Name { get; set; }

    public String? Path { get; set; }

    public bool Selected { get; set; } = false;

    public BaseFolder(int id, string path, string name) : this(path, name)
    {
        Id = id;
    }

    public BaseFolder(int id)
    {
        Id = id;
    }

    public BaseFolder(string path, string name)
    {
        Name = name;
        Path = path;
    }
}

public class BaseFolderTable() : Table<BaseFolder>("base_folders", [
    new DbAttribute(0, "base_folder_Id", "base_folder_Id INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY", false),
    new DbAttribute(1, "path", "path TEXT"),
    new DbAttribute(2, "name", "name VARCHAR(50)")
])
{
    public override void GetMissingData()
    {
        if (DataIsFilled)
        {
            return;
        }

        foreach (BaseFolder baseFolder in Data.Where(x => x.Path is null))
        {
            if (baseFolder.Id is null)
            {
                continue;
            }

            var conn = DBConnector.CreateConnection();
            conn.Open();
            string query = $"SELECT * FROM {TableName} WHERE base_folder_Id = {baseFolder.Id}";
            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                baseFolder.Path = reader.GetString(1);
                baseFolder.Name = reader.GetString(2);
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
            Data.Add(new BaseFolder(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        DataIsFilled = true;
    }

    public override void StoreNewData()
    {
        var conn = DBConnector.CreateConnection();
        conn.Open();
        foreach (BaseFolder obj in Data.Where(x => x.Id is null))
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
            query += ") RETURNING base_folder_Id";
            using var cmd = new NpgsqlCommand(query, conn);

            cmd.Parameters.AddWithValue("Path", obj.Path);
            cmd.Parameters.AddWithValue("Name", obj.Name);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                obj.Id = reader.GetInt32(0);
            }
        }
    }

    public override BaseFolder GetObject(int id)
    {
        var existing = Data.Where(x => x.Id == id);
        if (existing.Count() == 1)
        {
            return existing.First();
        }

        Data.Add(new BaseFolder(id));
        DataIsFilled = false;
        return Data.Last();
    }
}