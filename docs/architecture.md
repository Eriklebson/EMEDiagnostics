# Arquitetura

## Clean Architecture

```
EME.Diagnostics.App        (UI — WinUI 3, MVVM)
  ├── EME.Diagnostics.Services   (Stress engines, report repository)
  ├── EME.Diagnostics.Hardware   (LibreHardwareMonitor wrapper)
  ├── EME.Diagnostics.Reporting  (QuestPDF generation)
  ├── EME.Diagnostics.Core       (Models, contracts/interfaces)
  ├── EME.Diagnostics.Shared     (ProductInfo, constants)
  ├── EME.Diagnostics.GpuEngine  (Native C++ DLL — DirectX 11 Compute)
  └── EME.HardwareDatabase       (SQLite hardware database)
```

## Regras

- `Core` não depende de UI nem infraestrutura.
- `Hardware`, `Services`, `Reporting` dependem apenas de contratos do Core.
- UI não acessa LHM, WMI, DirectX ou arquivos diretamente.
- Injeção de dependência via `App.xaml.cs`, todos singletons.

## Injeção de Dependência (App.xaml.cs)

```csharp
services.AddSingleton<IHardwareMonitor, LibreHardwareMonitorService>();
services.AddSingleton<ICpuStressEngine, CpuStressEngine>();
services.AddSingleton<IGpuStressEngine, DirectX11GpuStressEngine>();
services.AddSingleton<IMemoryStressEngine, MemoryStressEngine>();
services.AddSingleton<IStorageStressEngine, StorageStressEngine>();
services.AddSingleton<IReportRepository, ReportRepository>();
services.AddSingleton<StressDataCollector>();
services.AddSingleton<IReportService, ReportService>();
services.AddSingleton<StressCatalogService>();
services.AddSingleton<MainViewModel>();
services.AddSingleton<MainWindow>();
```
