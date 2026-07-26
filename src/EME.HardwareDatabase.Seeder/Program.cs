using EME.HardwareDatabase.Services;
using EME.HardwareDatabase.Seed;
using EME.HardwareDatabase.Shared;

Console.WriteLine("=== E.M.E Hardware Database Seeder ===");
Console.WriteLine($"Banco: {Constants.DatabasePath}");
Console.WriteLine();

try
{
    var updateService = new HardwareDatabaseUpdateService();
    Console.WriteLine("Inicializando banco de dados...");
    updateService.EnsureHardwareDatabase();
    Console.WriteLine("Banco inicializado com sucesso.");
    Console.WriteLine();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        Console.WriteLine("\nCancelando...");
        cts.Cancel();
        e.Cancel = true;
    };

    Console.WriteLine("Iniciando seed de dados de hardware...");
    Console.WriteLine("Isso pode levar alguns minutos (download de ~3.000 GPUs + ~4.000 CPUs).");
    Console.WriteLine();

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var summary = await updateService.SeedIfEmptyAsync(cts.Token);
    sw.Stop();

    Console.WriteLine();
    Console.WriteLine(summary?.GetReport() ?? "Nenhum resultado disponível.");
    Console.WriteLine();
    Console.WriteLine($"Tempo total: {sw.Elapsed.TotalSeconds:F1}s");

    Console.WriteLine();
    Console.WriteLine("Verificando banco...");
    updateService.ValidateIntegrity();
    Console.WriteLine("Integridade OK.");

    using var conn = new DatabaseConnectionFactory().CreateReadOnlyConnection();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM Manufacturers"; Console.WriteLine($"Fabricantes: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM GpuArchitectures"; Console.WriteLine($"Arquiteturas GPU: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM GpuModels"; Console.WriteLine($"Modelos GPU: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM CpuArchitectures"; Console.WriteLine($"Arquiteturas CPU: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM CpuFamilies"; Console.WriteLine($"Famílias CPU: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM CpuModels"; Console.WriteLine($"Modelos CPU: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM HardwareAliases"; Console.WriteLine($"Aliases: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM GpuSensorMappings"; Console.WriteLine($"GPU Sensor Mappings: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM CpuSensorMappings"; Console.WriteLine($"CPU Sensor Mappings: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM SuperIoChips"; Console.WriteLine($"SuperIO Chips: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM Motherboards"; Console.WriteLine($"Motherboards: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM MemoryStandards"; Console.WriteLine($"Memory Standards: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM MemoryModels"; Console.WriteLine($"Memory Models: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM StorageControllers"; Console.WriteLine($"Storage Controllers: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM StorageDevices"; Console.WriteLine($"Storage Devices: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM StorageSensorMappings"; Console.WriteLine($"Storage Sensor Mappings: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM MotherboardFanMappings"; Console.WriteLine($"Motherboard Fan Mappings: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM MotherboardTemperatureMappings"; Console.WriteLine($"Motherboard Temp Mappings: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM MotherboardVoltageMappings"; Console.WriteLine($"Motherboard Volt Mappings: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM KnownIssues"; Console.WriteLine($"Known Issues: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM PowerSupplies"; Console.WriteLine($"Power Supplies: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM PsuSensorMappings"; Console.WriteLine($"PSU Sensor Mappings: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM NetworkDevices"; Console.WriteLine($"Network Devices: {cmd.ExecuteScalar()}");
    cmd.CommandText = "SELECT COUNT(*) FROM NetworkSensorMappings"; Console.WriteLine($"Network Sensor Mappings: {cmd.ExecuteScalar()}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERRO: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

Console.WriteLine();
Console.WriteLine("Seed concluído com sucesso!");
return 0;
