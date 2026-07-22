# Arquitetura proposta

```text
EME.Diagnostics.App
  ├── EME.Diagnostics.Services
  ├── EME.Diagnostics.Hardware
  ├── EME.Diagnostics.Reporting
  ├── EME.Diagnostics.Core
  └── EME.Diagnostics.Shared

EME.Diagnostics.Services ──► EME.Diagnostics.Core
EME.Diagnostics.Hardware ──► EME.Diagnostics.Core
EME.Diagnostics.Reporting ─► EME.Diagnostics.Core
```

O domínio define `IHardwareMonitor`, `IGpuStressEngine` e `IReportService`. A infraestrutura implementa esses contratos. A aplicação resolve tudo por injeção de dependência e apresenta snapshots prontos.

O futuro motor 3D será um projeto separado e poderá oferecer backends DirectX 11, DirectX 12, Vulkan e OpenGL. A UI conhecerá apenas `IGpuStressEngine`.

## Snapshot de hardware

`IHardwareMonitor` devolve um snapshot imutável contendo os resumos de CPU/GPU e a árvore normalizada de dispositivos. A implementação LHM percorre recursivamente hardwares e sub-hardwares e transforma cada sensor em `SensorMetric`, preservando tipo, valor, mínimo, máximo, unidade e identificador. A Dashboard apenas apresenta esse snapshot e não conhece a biblioteca de coleta.
