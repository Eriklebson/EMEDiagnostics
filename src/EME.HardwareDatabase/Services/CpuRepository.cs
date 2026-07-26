using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Contracts;
using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Models;

namespace EME.HardwareDatabase.Services;

public sealed class CpuRepository : ICpuRepository
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public CpuRepository(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CpuArchitecture?> FindArchitectureAsync(CpuDetectionIdentity identity, CancellationToken ct = default)
    {
        var alias = await FindAliasAsync(identity.Name, "CpuArchitecture", ct);
        if (alias != null)
            return await GetByIdAsync<CpuArchitecture>("CpuArchitectures", alias.TargetId, ct);

        return await Task.Run(() => FindArchitectureByName(identity.Name), ct);
    }

    public async Task<CpuFamily?> FindFamilyAsync(CpuDetectionIdentity identity, CancellationToken ct = default)
    {
        var alias = await FindAliasAsync(identity.Name, "CpuFamily", ct);
        if (alias != null)
            return await GetByIdAsync<CpuFamily>("CpuFamilies", alias.TargetId, ct);

        return await Task.Run(() => FindFamilyByName(identity.Name), ct);
    }

    public async Task<CpuModel?> FindModelAsync(CpuDetectionIdentity identity, CancellationToken ct = default)
    {
        var alias = await FindAliasAsync(identity.Name, "CpuModel", ct);
        if (alias != null)
            return await GetByIdAsync<CpuModel>("CpuModels", alias.TargetId, ct);

        return await Task.Run(() => FindModelByName(identity.Name), ct);
    }

    public async Task<List<CpuSensorMapping>> GetSensorMappingsAsync(string? modelId, string? architectureId, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var conn = _connectionFactory.CreateReadOnlyConnection();
            using var cmd = conn.CreateCommand();

            if (modelId != null)
            {
                cmd.CommandText = "SELECT Id, CpuModelId, CpuArchitectureId, SensorType, PreferredName, Priority " +
                                  "FROM CpuSensorMappings WHERE CpuModelId = $id ORDER BY Priority";
                cmd.Parameters.AddWithValue("$id", modelId);
                var modelMappings = ReadMappings(cmd);
                if (modelMappings.Count > 0) return modelMappings;
            }

            if (architectureId != null)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = "SELECT Id, CpuModelId, CpuArchitectureId, SensorType, PreferredName, Priority " +
                                  "FROM CpuSensorMappings WHERE CpuArchitectureId = $id ORDER BY Priority";
                cmd.Parameters.AddWithValue("$id", architectureId);
                return ReadMappings(cmd);
            }

            return new List<CpuSensorMapping>();
        }, ct);
    }

    private CpuArchitecture? FindArchitectureByName(string cpuName)
    {
        using var conn = _connectionFactory.CreateReadOnlyConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT a.Id, a.ManufacturerId, a.Name, a.DisplayName, a.Segment, a.Released " +
                          "FROM CpuArchitectures a " +
                          "INNER JOIN HardwareAliases h ON h.TargetId = a.Id AND h.TargetType = 'CpuArchitecture' " +
                          "WHERE $name LIKE '%' || h.Alias || '%' " +
                          "ORDER BY LENGTH(h.Alias) DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$name", cpuName);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadArchitecture(reader) : null;
    }

    private CpuFamily? FindFamilyByName(string cpuName)
    {
        using var conn = _connectionFactory.CreateReadOnlyConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT f.Id, f.ArchitectureId, f.Name, f.DisplayName " +
                          "FROM CpuFamilies f " +
                          "INNER JOIN HardwareAliases h ON h.TargetId = f.Id AND h.TargetType = 'CpuFamily' " +
                          "WHERE $name LIKE '%' || h.Alias || '%' " +
                          "ORDER BY LENGTH(h.Alias) DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$name", cpuName);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadFamily(reader) : null;
    }

    private CpuModel? FindModelByName(string cpuName)
    {
        using var conn = _connectionFactory.CreateReadOnlyConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT m.Id, m.FamilyId, m.Name, m.Cores, m.Threads, m.BaseClock, m.BoostClock, m.Tdp " +
                          "FROM CpuModels m " +
                          "INNER JOIN HardwareAliases h ON h.TargetId = m.Id AND h.TargetType = 'CpuModel' " +
                          "WHERE $name LIKE '%' || h.Alias || '%' " +
                          "ORDER BY LENGTH(h.Alias) DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$name", cpuName);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadModel(reader) : null;
    }

    private async Task<HardwareAlias?> FindAliasAsync(string name, string targetType, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            using var conn = _connectionFactory.CreateReadOnlyConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, TargetType, TargetId, Alias, MatchMode FROM HardwareAliases " +
                              "WHERE TargetType = $tt AND $name LIKE '%' || Alias || '%' " +
                              "ORDER BY LENGTH(Alias) DESC LIMIT 1";
            cmd.Parameters.AddWithValue("$tt", targetType);
            cmd.Parameters.AddWithValue("$name", name);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new HardwareAlias
            {
                Id = reader.GetInt32(0),
                TargetType = reader.GetString(1),
                TargetId = reader.GetString(2),
                Alias = reader.GetString(3),
                MatchMode = reader.GetString(4)
            };
        }, ct);
    }

    private async Task<T?> GetByIdAsync<T>(string table, string id, CancellationToken ct) where T : new()
    {
        return await Task.Run(() =>
        {
            using var conn = _connectionFactory.CreateReadOnlyConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table} WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return default;

            var result = new T();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var prop = typeof(T).GetProperty(reader.GetName(i),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null && !reader.IsDBNull(i))
                {
                    var val = reader.GetValue(i);
                    prop.SetValue(result, Convert.ChangeType(val, prop.PropertyType));
                }
            }
            return result;
        }, ct);
    }

    private static List<CpuSensorMapping> ReadMappings(SqliteCommand cmd)
    {
        var list = new List<CpuSensorMapping>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CpuSensorMapping
            {
                Id = reader.GetInt32(0),
                CpuModelId = reader.IsDBNull(1) ? null : reader.GetString(1),
                CpuArchitectureId = reader.IsDBNull(2) ? null : reader.GetString(2),
                SensorType = reader.GetString(3),
                PreferredName = reader.GetString(4),
                Priority = reader.GetInt32(5)
            });
        }
        return list;
    }

    private static CpuArchitecture ReadArchitecture(SqliteDataReader r) => new()
    {
        Id = r.GetString(0),
        ManufacturerId = r.GetString(1),
        Name = r.GetString(2),
        DisplayName = r.GetString(3),
        Segment = r.GetString(4),
        Released = r.IsDBNull(5) ? null : r.GetInt32(5)
    };

    private static CpuFamily ReadFamily(SqliteDataReader r) => new()
    {
        Id = r.GetString(0),
        ArchitectureId = r.GetString(1),
        Name = r.GetString(2),
        DisplayName = r.IsDBNull(3) ? null : r.GetString(3)
    };

    private static CpuModel ReadModel(SqliteDataReader r) => new()
    {
        Id = r.GetString(0),
        FamilyId = r.GetString(1),
        Name = r.GetString(2),
        Cores = r.IsDBNull(3) ? null : r.GetInt32(3),
        Threads = r.IsDBNull(4) ? null : r.GetInt32(4),
        BaseClock = r.IsDBNull(5) ? null : r.GetDouble(5),
        BoostClock = r.IsDBNull(6) ? null : r.GetDouble(6),
        Tdp = r.IsDBNull(7) ? null : r.GetInt32(7)
    };
}
