# Banco de Dados

## Hardware Database (SQLite)

Localização: `C:\ProgramData\EME\HardwareDatabase\eme-hardware.db`

### Schema

Tabelas principais:
- `CpuModels`, `CpuArchitectures`, `CpuFamilies` — modelos de CPU
- `GpuModels`, `GpuArchitectures` — modelos de GPU
- `MemoryModels`, `MemoryStandards` — módulos de memória
- `HardwareAliases` — aliases de busca para detecção
- `Motherboards`, `MotherboardFanMappings`, `MotherboardTemperatureMappings`, `MotherboardVoltageMappings` — mapeamento de sensores por placa-mãe
- `CpuSensorMappings`, `GpuSensorMappings` — mapeamento de sensores por modelo
- `DatabaseMetadata`, `DatabaseMigrations` — controle de versão do schema
- `Manufacturers` — fabricantes
- `StorageControllers`, `StorageDevices`, `StorageSensorMappings`
- `PowerSupplies`, `PsuSensorMappings`
- `NetworkDevices`, `NetworkSensorMappings`
- `SuperIoChips`, `Monitors`, `KnownIssues`

### Versões

- SchemaVersion: `1.0.0`
- DataVersion: `2026.07.001`
- MinimumCoreVersion: `2.26.0`
- MinimumDiagnosticsVersion: `1.0.0`

### Seed

O banco é populado pelo `EME.HardwareDatabase.Seeder` (console app standalone).
Os dados vêm de:
- Importadores de rede (RightNowGpuImporter, TechApiCpuImporter)
- Seeders locais (CpuSensorMappingSeeder, GpuSensorMappingSeeder, etc.)

### Reports Database (SQLite)

Localização: `%LOCALAPPDATA%\EMEDiagnostics\reports.db`

Tabelas:
- `Reports` — id, createdAt, testType, duration, status, cpuName, gpuName, memoryTotalGb, storageName
- `ReportEntries` — id, reportId, component, sensorName, unit, minValue, maxValue, avgValue
