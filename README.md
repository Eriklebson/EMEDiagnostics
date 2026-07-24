# E.M.E Diagnostics

Suíte profissional de diagnóstico, monitoramento e testes de estabilidade de computadores, com identidade visual Cyber Dark do ecossistema E.M.E.

## Estado atual

- Arquitetura em camadas com .NET 8 e WinUI 3
- Navegação e telas iniciais
- Dashboard alimentado por sensores reais do LibreHardwareMonitor quando disponíveis
- Stress real de CPU e cena 3D procedural com carga compute de GPU em backend nativo DirectX 11
- Abstração `IGpuStressEngine` preservada para backends DirectX 12, Vulkan e OpenGL futuros

## Projetos

- `EME.Diagnostics.App`: WinUI 3, MVVM, navegação e DI
- `EME.Diagnostics.Core`: modelos e contratos de domínio
- `EME.Diagnostics.Hardware`: coleta real de hardware
- `EME.Diagnostics.Services`: orquestração e casos de uso
- `EME.Diagnostics.GpuEngine`: motor nativo C++/DirectX 11 de carga compute
- `EME.Diagnostics.Reporting`: contratos e implementação futura de relatórios
- `EME.Diagnostics.Shared`: tipos e constantes compartilhados

## Compilar

```powershell
dotnet build EMEDiagnostics.sln -p:Platform=x64
```

Alguns sensores de placa-mãe e ventoinhas exigem execução como administrador.

O teste de GPU usa 15% da VRAM como teto e limita o buffer inicial a 64 MB por compatibilidade entre drivers. Ele publica métricas a cada 250 ms e é interrompido automaticamente se a temperatura monitorada alcançar 90 °C.

Ao iniciar o teste de GPU, uma janela DirectX 11 dedicada em 1600×900 apresenta uma cinematic procedural em uma cidade medieval: castelo central, torres, casas, materiais detalhados, iluminação PBR, lua, nuvens, neblina, tochas, janelas emissivas, sombras suaves e câmera animada. Fechar essa janela cancela o teste com segurança.
