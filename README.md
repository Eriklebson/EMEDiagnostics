# E.M.E Diagnostics

**Suíte profissional de diagnóstico, monitoramento e teste de estresse para PCs Windows** — com a identidade visual Cyber Dark do ecossistema E.M.E.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp)](https://learn.microsoft.com/dotnet/csharp/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4?logo=windows)](https://learn.microsoft.com/windows/apps/winui/)
[![C++](https://img.shields.io/badge/C++-20-00599C?logo=cplusplus)](https://isocpp.org/)
[![DirectX 11](https://img.shields.io/badge/DirectX-11-00B4EF)](https://learn.microsoft.com/windows/win32/direct3d11/)
[![SQLite](https://img.shields.io/badge/SQLite-local-003B57?logo=sqlite)](https://www.sqlite.org/)
[![License](https://img.shields.io/badge/Licença-MIT-green)](LICENSE)

---

## Funcionalidades

| Recurso | Descrição | Status |
|---|---|---|
| **Dashboard** | Monitoramento em tempo real de CPU, GPU, RAM, disco, rede, fans e periféricos | ✅ |
| **Teste de CPU** | Estresse multithread com seletor de duração, gráfico de temperatura e uso, botão Parar | ✅ |
| **Teste de RAM** | Alocação progressiva em chunks de 256 MB até 100% da RAM disponível, com proteção de working set | ✅ |
| **Teste de GPU** | Motor nativo C++/DirectX 11 com cena procedural medieval (PBR, sombras, bloom) | ✅ |
| **Teste de Armazenamento** | Escrita e leitura com `FILE_FLAG_NO_BUFFERING`, WriteThrough, 16 streams paralelos | ✅ |
| **Teste Combinado** | Dispara CPU + GPU + RAM + Storage (leitura) simultaneamente | ✅ |
| **Gráficos de Telemetria** | Chart em tempo real com valores reais (MB/s, °C, %) por sensor | ✅ |
| **Relatórios** | Banco SQLite local, exportação PDF (QuestPDF) com min/méd/máx por sensor | ✅ |
| **Tema Cyber Dark** | Paleta escura com acento verde-menta, inspirada no E.M.E Core | ✅ |

---

## Projetos da Solução

```
EME.Diagnostics.App             → WinUI 3, MVVM, DI (Principal)
├── EME.Diagnostics.Core          → Modelos e contratos de domínio
├── EME.Diagnostics.Hardware      → Coleta de sensores (LibreHardwareMonitor, WMI, API nativa)
├── EME.Diagnostics.Services      → Orquestração e casos de uso
├── EME.Diagnostics.Reporting     → Geração de relatórios PDF (QuestPDF)
├── EME.Diagnostics.GpuEngine     → Motor nativo C++/DirectX 11 (DLL)
├── EME.Diagnostics.Shared        → Constantes e tipos compartilhados
├── EME.HardwareDatabase          → Catálogo SQLite de hardware + mapeamento de sensores
└── EME.HardwareDatabase.Seeder   → Seeders (PCPartPicker, RightNow GPU, TechAPI CPU, referência)
```

## Instalação

### Pré-requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.422)
- Windows 10 (build 17763+) ou Windows 11
- Visual Studio 2022 (opcional, para compilar)

### Build

```powershell
# Clone
git clone https://github.com/Eriklebson/EMEDiagnostics.git
cd EMEDiagnostics

# Build completo (sem motor GPU)
dotnet build EMEDiagnostics.sln -c Release -p:Platform=x64 -p:SkipGpuEngine=true -p:AppxGeneratePriEnabled=false -p:MakePriExeFullPath=""

# Build com motor GPU (requer SDK do Windows 10 26100)
dotnet build EMEDiagnostics.sln -c Release -p:Platform=x64
```

> A build com `SkipGpuEngine=true` desativa o backend DirectX 11, mantendo apenas os testes de CPU, RAM e armazenamento.

---

## Arquitetura

Clean Architecture com 3 camadas + MVVM + SOLID.

```
EME.Diagnostics.App (WinExe - WinUI 3)
  ├── EME.Diagnostics.Core        (Class Library)
  ├── EME.Diagnostics.Hardware    (Class Library)
  ├── EME.Diagnostics.Services    (Class Library)
  ├── EME.Diagnostics.Reporting   (Class Library)
  ├── EME.Diagnostics.GpuEngine   (C++ DLL - DirectX 11)
  ├── EME.Diagnostics.Shared      (Class Library)
  ├── EME.HardwareDatabase        (Class Library)
  └── EME.HardwareDatabase.Seeder (Console App)
```

### Princípios

- **Core** não depende de UI nem infraestrutura.
- **Hardware**, **Services** e **Reporting** dependem apenas dos contratos em Core.
- A UI não acessa LibreHardwareMonitor, WMI, DirectX ou arquivos diretamente.
- Motores gráficos futuros implementam `IGpuStressEngine`; a UI não conhece DirectX, Vulkan ou OpenGL.
- Uso de `GlobalMemoryStatusEx` (P/Invoke) para precisão de RAM, sem depender de LHM.
- Coleção de sensores em background com snapshots imutáveis.

### Motor GPU

O backend DirectX 11 (`DirectX11GpuStressEngine`) renderiza uma cena procedural medieval completa:
- Castelo, torres, casas, estrada, árvores, poço, carroça animada
- Terreno gerado por heightmap (Perlin noise)
- Iluminação PBR Cook-Torrance com IBL (cubemap sunset)
- Shadow map PCF 3×3, bloom, ACES tonemapping, neblina por altura
- Janela dedicada 1600×900 com self-hosting Win32
- Proteção térmica (desarme em 90 °C) e cancelamento seguro

---

## Bancos de Dados

### Reports (`%LOCALAPPDATA%\EMEDiagnostics\reports.db`)

| Tabela | Descrição |
|--------|-----------|
| Reports | Relatórios de teste (tipo, duração, timestamp) |
| ReportEntries | Sensores coletados (mínimo, médio, máximo por sessão) |

### Hardware Database (`%PROGRAMDATA%\EME\HardwareDatabase\eme-hardware.db`)

Catálogo local com +105 mil registros de hardware para mapeamento de sensores:

| Tabela | Registros |
|--------|-----------|
| Motherboards | 5.046 |
| Memories | 3.527 |
| StorageDevices | 1.918 |
| PsuDevices | 2.129 |
| NetworkDevices | 517 |
| CpuSensorMappings | 21.597 |
| GpuSensorMappings | 1.208 |
| MotherboardSensorMappings | 1.561 |
| StorageSensorMappings | 7.672 |
| PsuSensorMappings | 40.451 |
| NetworkSensorMappings | 3.822 |

> Banco populado via seeders (RightNow GPU, TechAPI CPU, PCPartPicker dataset, dados de referência).

---

## Relatórios

Os resultados dos testes de estresse são salvos automaticamente ao finalizar e podem ser exportados em PDF (QuestPDF) com:
- Metadados do teste (tipo, duração, data/hora)
- Hardware detectado no momento do teste
- Tabela de sensores com valores mínimos, máximos e médios

---

## Sistema Visual

Padrão Cyber Dark herdado do ecossistema E.M.E:

| Papel | Cor |
|---|---|
| Fundo | `#0A0B0D` |
| Cartão | `#2A2D31` |
| Texto principal | `#E8E9EB` |
| Acento | `#4CCBA0` |
| Aviso | `#E6A030` |
| Perigo | `#E84D4D` |

---

## Histórico de Versões

### v1.1.0 (2026-07-30) — PASS/RECUSADO por throttling

| Tipo | Mudança |
|------|---------|
| ✨ Feature | Sistema PASS/RECUSADO — detecta throttling de CPU/GPU durante stress test |
| ✨ Feature | Resultado exibido em verde (PASS) ou vermelho (RECUSADO) nos cards e detalhes |
| ✨ Feature | Carimbo centralizado no PDF com resultado (borda colorida, semi-bold) |
| 🔧 Interno | `StressTestResult` enum com Pass, RecusadoCpu, RecusadoGpu, RecusadoCpuGpu |
| 🔧 Interno | `ComputeThrottlingResult()` — analisa clock da 1ª metade vs 2ª metade |
| 🗄️ Banco | Coluna `Result` na tabela `Reports` com migração automática |
| ⚙️ Build | 0 erros, 0 warnings. v1.1.0 |

### v1.0.2 (2026-07-30) — Documentação, instalador, PawnIO

| Tipo | Mudança |
|------|---------|
| 📚 Docs | Criada estrutura completa de documentação em `docs/` (8 arquivos + índice) |
| 🛠️ Build | AGENTS.md revisado com regras de build seguro, versionamento SemVer e README |
| 📦 Instalação | Banco SQLite (31MB) incluso no instalador, extraído em `%PROGRAMDATA%` |
| 📦 Instalação | PawnIO driver instalado automaticamente ao final da instalação |
| 🔧 Interno | Adicionado `DiagnosticLogger.cs` para logging de diagnóstico |
| 🔧 Interno | Permissão `users-modify` no diretório do banco de dados no instalador |
| ⚙️ Build | 0 erros, 0 warnings |

### v1.0.1 (2026-07-26) — Correção de detecção de hardware

| Tipo | Mudança |
|------|---------|
| 🐛 Correção | `InvalidCastException` ao ler propriedades `int?` do SQLite no `CpuRepository.GetByIdAsync<T>()` — `long` não era convertível para `Nullable<int>` via `Convert.ChangeType` |
| 🔒 Segurança | `app.manifest` alterado de `asInvoker` para `requireAdministrator` — LHM precisa de admin para enumerar sensores completos |
| 🧹 Limpeza | Removido logging diagnóstico em `%TEMP%` |
| ⚙️ Build | 0 erros, 0 warnings. Instalador v1.0.1.0 |

### v1.0.0 (2026-07-25) — Primeira release

| Tipo | Mudança |
|------|---------|
| ✨ Feature | Dashboard com monitoramento em tempo real de CPU, GPU, RAM, disco, fans |
| ✨ Feature | Teste de estresse multithread para CPU com gráfico de temperatura |
| ✨ Feature | Teste de RAM com alocação progressiva até 100% |
| ✨ Feature | Motor GPU nativo C++/DirectX 11 com cena procedural medieval (PBR, sombras, bloom) |
| ✨ Feature | Teste de armazenamento com `FILE_FLAG_NO_BUFFERING` e 16 streams paralelos |
| ✨ Feature | Teste combinado (CPU + GPU + RAM + Storage simultâneo) |
| ✨ Feature | Gráficos de telemetria em tempo real |
| ✨ Feature | Relatórios com banco SQLite local e exportação PDF (QuestPDF) |
| ✨ Feature | Banco de dados de hardware SQLite com seeders (GPUs, CPUs, sensor mappings) |
| ✨ Feature | Integração com dataset PCPartPicker (~105k registros: motherboards, RAM, storage, PSUs, network) |
| ✨ Feature | Mapeamento de sensores por SuperIO para motherboard |
| 🎨 UI | Tema Cyber Dark com paleta escura e acento verde-menta |

---

## Licença

MIT © 2026 Eriklebson
