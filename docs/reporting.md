# Relatórios

## Fluxo

1. Stress test coleta snapshots via `StressDataCollector.AddSample(snapshot)`
2. Ao final, `StressDataCollector.SaveReportAsync(duration, status)` agrega min/max/avg por sensor
3. Salvo em SQLite via `ReportRepository`
4. Página de Relatórios lista via `GetAllReportsAsync()`
5. "Exportar PDF" → `ReportService.ExportPdfAsync()` → QuestPDF

## Modelos

```csharp
enum ReportTestType { Cpu, Gpu, Memory, Storage, Combined }

record StressReportSummary(long Id, DateTime CreatedAt, ReportTestType TestType,
    TimeSpan Duration, string Status, int EntryCount);

record StressReportDetail(long Id, DateTime CreatedAt, ReportTestType TestType,
    TimeSpan Duration, string Status, string? CpuName, string? GpuName,
    double MemoryTotalGb, string? StorageName, IReadOnlyList<ReportEntry> Entries);

record ReportEntry(string Component, string SensorName, string Unit,
    double? MinValue, double? MaxValue, double? AvgValue);
```

## PDF (QuestPDF)

Gerado em A4 com Community License. Inclui:
- Header com tipo de teste e data
- Resumo do hardware (CPU, GPU, RAM, Storage)
- Tabelas de sensores agrupados por componente (CPU, GPU, RAM, etc.)

## UI (Relatórios)

- A tabela apresenta ID, teste, data, duração, pico térmico, status e ações.
- Um clique na linha abre ou fecha o painel integrado de detalhes.
- O painel expandido mostra seis métricas resumidas e o registro cronológico do teste.
- O botão PDF é independente do collapse e exporta o documento para `Documents\EMEDiagnostics`.
- Em janelas estreitas, a tabela conserva as colunas e disponibiliza rolagem horizontal.
