using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer;

/// <summary>
/// Interface to provide common functionality for all tables in the database
/// </summary>
public abstract class Table<T>(string tableName, List<DbAttribute> attributes) where T : ITableObject
{
    /// <summary>
    /// The name of the table in the database
    /// </summary>
    public string TableName { get; protected set; } = tableName;

    public List<DbAttribute> Attributes { get; protected set; } = attributes;
    public ObservableCollection<T> Data { get; protected set; } = new ObservableCollection<T>();
    public bool DataIsFilled { get; protected set; } = true;

    public async Task<bool> TableExists()
    {
        var result = new List<bool>();
        var conn = DBConnector.CreateConnection();
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(
                         $"SELECT EXISTS( SELECT FROM pg_tables WHERE tablename = '{TableName}');", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                result.Add(reader.GetBoolean(0));
            }
        }

        return result[0];
    }

    /// <summary>
    /// Create the table in the database
    /// </summary>
    public virtual async void CreateTable()
    {
        var conn = DBConnector.CreateConnection();
        conn.Open();
        string query = $"CREATE TABLE {TableName} (";
        foreach (var attribute in Attributes)
        {
            query += $"{attribute.SetupString}, ";
        }

        query = query.Remove(query.Length - 2);
        query += ");";
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Get the missing data for the Data list
    /// </summary>
    public abstract void GetMissingData();

    /// <summary>
    /// Get the full list of data from the table
    /// </summary>
    public abstract void GetList();

    /// <summary>
    /// Store a list of ITableObjects in the table and get the ID's for them filled back in.
    /// </summary>
    public abstract void StoreNewData();

    /// <summary>
    /// Get a single object from the table by ID
    /// </summary>
    /// <param name="id"></param>
    /// <returns>The Object with the corresponding ID</returns>
    public abstract T GetObject(int id);
}