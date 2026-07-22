# E.M.E Diagnostics

Suíte profissional de diagnóstico, monitoramento e testes de estabilidade de computadores, com identidade visual Cyber Dark do ecossistema E.M.E.

## Estado atual

- Arquitetura em camadas com .NET 8 e WinUI 3
- Navegação e telas iniciais
- Dashboard alimentado por sensores reais do LibreHardwareMonitor quando disponíveis
- Stress Test, Benchmark e Relatórios preparados estruturalmente, sem cargas reais nesta fase
- Abstração `IGpuStressEngine` preparada para backends futuros

## Projetos

- `EME.Diagnostics.App`: WinUI 3, MVVM, navegação e DI
- `EME.Diagnostics.Core`: modelos e contratos de domínio
- `EME.Diagnostics.Hardware`: coleta real de hardware
- `EME.Diagnostics.Services`: orquestração e casos de uso
- `EME.Diagnostics.Reporting`: contratos e implementação futura de relatórios
- `EME.Diagnostics.Shared`: tipos e constantes compartilhados

## Compilar

```powershell
dotnet build EMEDiagnostics.sln -p:Platform=x64
```

Alguns sensores de placa-mãe e ventoinhas exigem execução como administrador.
