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
public abstract class Table(string tableName, List<DbAttribute> attributes)
{
    /// <summary>
    /// The name of the table in the database
    /// </summary>
    public string TableName { get; protected set; } = tableName;
    public List<DbAttribute> Attributes { get; protected set; } = attributes;
    public ObservableCollection<ITableObject> Data { get; protected set; } = new ObservableCollection<ITableObject>();

    public async Task<bool> TableExists()
    {
        var result = new List<bool>();
        var conn = DBConnector.CreateConnection();
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"SELECT EXISTS( SELECT FROM pg_tables WHERE tablename = '{TableName}');", conn))
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
            query += $"{attribute.Name} {attribute.DbType}, ";
        }
        query = query.Remove(query.Length - 2);
        query += ");";
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Get the data for the provided table with id
    /// </summary>
    /// <param name="idList"> List of Objects with their ID's filled, to get the data for</param>
    public abstract void GetData(List<ITableObject> idList);

    /// <summary>
    /// Get the full list of data from the table
    /// </summary>
    /// <returns>The filled out List of ITableObjects this Table</returns>
    public abstract List<ITableObject> GetList();

    /// <summary>
    /// Store a list of ITableObjects in the table and get the ID's for them filled back in.
    /// </summary>
    /// <param name="data">List of ITableObjects to store</param>
    public abstract void StoreNewData(List<ITableObject> data);
}