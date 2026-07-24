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

O motor de carga da GPU está isolado em `EME.Diagnostics.GpuEngine`, uma DLL nativa C++ com backend inicial DirectX 11 Compute. `DirectX11GpuStressEngine` implementa `IGpuStressEngine` e concentra a interoperabilidade; a UI conhece apenas o contrato. Backends DirectX 12, Vulkan e OpenGL podem ser adicionados sem alterar a UI.

O backend executa um compute shader em memória dedicada, mede dispatches e frame time, detecta remoção do dispositivo pelo driver e respeita cancelamento. A aplicação aplica proteção térmica de 90 °C usando o snapshot do monitor de hardware.

O mesmo backend cria uma janela Win32 dedicada com swap chain DirectX 11. A cena medieval em 1600×900 é produzida por ray marching em pixel shader e combina cidade procedural, castelo, materiais ruidosos, iluminação PBR, sombras suaves, ambient occlusion, múltiplas luzes, materiais emissivos, nuvens, neblina e câmera cinematográfica. O loop de mensagens, a renderização e a carga compute permanecem na thread nativa; a UI WinUI apenas inicia, cancela e recebe métricas.

## Snapshot de hardware

`IHardwareMonitor` devolve um snapshot imutável contendo os resumos de CPU/GPU e a árvore normalizada de dispositivos. A implementação LHM percorre recursivamente hardwares e sub-hardwares e transforma cada sensor em `SensorMetric`, preservando tipo, valor, mínimo, máximo, unidade e identificador. A Dashboard apenas apresenta esse snapshot e não conhece a biblioteca de coleta.
