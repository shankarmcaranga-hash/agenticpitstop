using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AgenticPITSTOP.Data
{
    /// <summary>
    /// Database helper that provides basic async ADO.NET operations using Microsoft.Data.SqlClient.
    /// </summary>
    public class DBHelper
    {
        private readonly string _connectionString;

        public DBHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' not found.");
        }

        public string GetConnectionString() => _connectionString;

        public async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[]? parameters)
        {
            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<object?> ExecuteScalarAsync(string sql, params SqlParameter[]? parameters)
        {
            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            await conn.OpenAsync();
            return await cmd.ExecuteScalarAsync();
        }

        /// <summary>
        /// Execute query and return rows as a list of dictionaries (column name -> value).
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, params SqlParameter[]? parameters)
        {
            var results = new List<Dictionary<string, object?>>();
            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    var value = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                    row[name] = value;
                }
                results.Add(row);
            }

            return results;
        }

        /// <summary>
        /// Execute query and return results as a DataTable.
        /// </summary>
        public async Task<DataTable> ExecuteDataTableAsync(string sql, params SqlParameter[]? parameters)
        {
            var table = new DataTable();
            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            table.Load(reader);
            return table;
        }
    }
}
