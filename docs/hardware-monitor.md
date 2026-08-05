# Coleta de Hardware

Além dos sensores do LibreHardwareMonitor, a camada de hardware coleta a capacidade da unidade do sistema pelo Windows e publica `StorageUsedGb`, `StorageFreeGb` e `StorageTotalGb` no `HardwareSnapshot`.

## LibreHardwareMonitor

Wrapper em `EME.Diagnostics.Hardware.LibreHardwareMonitorService` que implementa `IHardwareMonitor`.

### Inicialização

1. `EnsureHardwareDatabase()` — cria/verifica banco em `C:\ProgramData\EME\HardwareDatabase\`
2. `Computer.Open()` — inicializa LHM com CPU, GPU, Memory, Motherboard, Storage habilitados
3. `CpuSensorMappingResolver.Load()` — carrega mapeamento de nomes de sensores (DB + JSON + hardcoded)

### Captura (CaptureAsync)

- Enumera todos os hardwares recursivamente
- Chama `hardware.Update()` em cada um
- Detecta CPU via `DetectCpu(cpu.Name)` — resolve vendor via DB ou JSON config ou hardcoded
- Monta `HardwareSnapshot` imutável com CPU, GPU, RAM, Storage, Fans, dispositivos brutos

### Sensor Mapping

O `CpuSensorMappingResolver` resolve nomes de sensores com fallback em cascata:
1. Arquitetura específica (JSON `cpu-sensors-mapping.json`)
2. Vendor default (JSON `vendor_defaults`)
3. Hardcoded (AMD: Core (Tctl/Tdie) + CPU Package; Intel: CPU Package + CPU Package)
4. Final fallback: "Core (Tctl/Tdie)" para temp, "CPU Package" para power

### PawnIO Driver

LHM 0.9.6+ requer PawnIO driver instalado no sistema para acesso ring 0 (SMU/MSR).
Sem PawnIO: Load e Voltagem funcionam, mas Temperatura/Potência/Clock retornam 0.
PawnIO é instalado pelo `PawnIO_setup.exe` incluso no instalador (executado via `CurStepChanged` no Inno Setup).

Arquivo de configuração: `config/cpu-sensors-mapping.json` (28 arquiteturas, fallbacks por vendor).

## HardwareSnapshot

```csharp
record HardwareSnapshot(
    DateTimeOffset CapturedAt,
    ComponentMetric Cpu,        // Name, Usage, Temperature, Clock, Power
    ComponentMetric Gpu,        // Name, Usage, Temperature, Clock, Power
    double MemoryUsedGb,
    double MemoryTotalGb,
    double? MemoryTemperature,
    double? StorageTemperature,
    double? StorageLoad,
    double? StorageReadMBs,
    double? StorageWriteMBs,
    IReadOnlyList<FanMetric> Fans,
    IReadOnlyList<HardwareDeviceSnapshot> Devices);
```
