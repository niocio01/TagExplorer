using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using TagExplorer.Models;

namespace TagExplorer;

public static class DBConnector
{
    public enum ConnectionState
    {
        Unknown,
        Connected,
        Failed,
    }

    public static ConnectionState CurrentConnectionState = ConnectionState.Unknown;


    private static string? _connectionString;

    public static bool CheckConnection(DBConnectionSettings_M settings)
    {
        _connectionString =
            $"Host={settings.Host}; Port = {settings.Port}; Database = postgres; User Id = {settings.Username}; Password = {settings.Password}; ";

        using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            connection.Open();
            if (connection.State == System.Data.ConnectionState.Open)
            {
                connection.Close();
                CurrentConnectionState = ConnectionState.Connected;
                return true;
            }
        }
        catch (Npgsql.PostgresException e)
        {
            CurrentConnectionState = ConnectionState.Failed;
            connection.Close();
            return false;
        }

        return false;
    }

    public static async Task<bool> TableExistsAsync(string tableName)
    {
        var result = new List<bool>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(
                         $"SELECT EXISTS( SELECT FROM pg_tables WHERE tablename = '{tableName}');", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                result.Add(reader.GetBoolean(0));
            }
        }

        return result[0];
    }

    public static async Task CreateBaseFolderTable()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(
                         "CREATE TABLE base_folders (path TEXT PRIMARY KEY, name VARCHAR(255));", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public static void SaveBaseFolders()
    {
        if (CurrentConnectionState == ConnectionState.Connected)
        {
            Main.BaseFolderTable.StoreNewData();
        }
    }

    public static NpgsqlConnection? CreateConnection()
    {
        if (CurrentConnectionState != ConnectionState.Connected)
        {
            return null;
            throw new Exception("Database is not connected");
        }

        return new NpgsqlConnection(_connectionString);
    }
}