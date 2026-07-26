using Microsoft.Data.Sqlite;
using EME.HardwareDatabase.Contracts;
using EME.HardwareDatabase.Detection;
using EME.HardwareDatabase.Models;

namespace EME.HardwareDatabase.Services;

public sealed class MotherboardRepository : IMotherboardRepository
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public MotherboardRepository(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<Motherboard?> FindAsync(MotherboardDetectionIdentity identity, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var conn = _connectionFactory.CreateReadOnlyConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT m.Id, m.ManufacturerId, m.Name, m.SuperIoId, m.Chipset, m.FormFactor " +
                              "FROM Motherboards m WHERE $name LIKE '%' || m.Name || '%' LIMIT 1";
            cmd.Parameters.AddWithValue("$name", identity.Name);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new Motherboard
            {
                Id = reader.GetString(0),
                ManufacturerId = reader.GetString(1),
                Name = reader.GetString(2),
                SuperIoId = reader.IsDBNull(3) ? null : reader.GetString(3),
                Chipset = reader.IsDBNull(4) ? null : reader.GetString(4),
                FormFactor = reader.IsDBNull(5) ? null : reader.GetString(5)
            };
        }, ct);
    }

    public Task<List<MotherboardFanMapping>> GetFanMappingsAsync(string motherboardId, CancellationToken ct = default)
    {
        return GetMappingsAsync<MotherboardFanMapping>(
            "MotherboardFanMappings", motherboardId, ReadFanMapping, ct);
    }

    public Task<List<MotherboardTemperatureMapping>> GetTemperatureMappingsAsync(string motherboardId, CancellationToken ct = default)
    {
        return GetMappingsAsync<MotherboardTemperatureMapping>(
            "MotherboardTemperatureMappings", motherboardId, ReadTemperatureMapping, ct);
    }

    public Task<List<MotherboardVoltageMapping>> GetVoltageMappingsAsync(string motherboardId, CancellationToken ct = default)
    {
        return GetMappingsAsync<MotherboardVoltageMapping>(
            "MotherboardVoltageMappings", motherboardId, ReadVoltageMapping, ct);
    }

    private Task<List<T>> GetMappingsAsync<T>(string table, string motherboardId,
        Func<SqliteDataReader, T> factory, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            using var conn = _connectionFactory.CreateReadOnlyConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table} WHERE MotherboardId = $id";
            cmd.Parameters.AddWithValue("$id", motherboardId);
            var list = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(factory(reader));
            return list;
        }, ct);
    }

    private static MotherboardFanMapping ReadFanMapping(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        MotherboardId = r.GetString(1),
        RawName = r.GetString(2),
        MappedName = r.GetString(3),
        Category = r.GetString(4)
    };

    private static MotherboardTemperatureMapping ReadTemperatureMapping(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        MotherboardId = r.GetString(1),
        RawName = r.GetString(2),
        MappedName = r.GetString(3)
    };

    private static MotherboardVoltageMapping ReadVoltageMapping(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        MotherboardId = r.GetString(1),
        RawName = r.GetString(2),
        MappedName = r.GetString(3)
    };
}
