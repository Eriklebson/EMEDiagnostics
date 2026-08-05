# Problemas Conhecidos

## Inicialização da interface

- Evitar controles que dependam de recursos internos de tema quando houver alternativa desenhada pelo aplicativo. O `ProgressBar` do armazenamento tentou resolver `TabViewScrollButtonBackground` e encerrou o processo com `0xc000027b`; ele foi substituído por uma barra própria.
- Exceções fatais capturáveis da interface são registradas em `%LOCALAPPDATA%\EMEDiagnostics\ui_crash.log`.

## PawnIO Driver

- LHM 0.9.6+ requer PawnIO driver para acesso ring 0 (SMU/MSR)
- Sem PawnIO: Load e Voltagem funcionam, mas Temperatura/Potência/Clock retornam 0
- Instalado automaticamente pelo instalador Inno Setup via `PawnIO_setup.exe`
- Se não instalou → executar manualmente `C:\Program Files\EME Diagnostics\PawnIO_setup.exe`

## LHM DeviceRemoved

- GPU stress engine detecta `Win32Exception: "The device was removed"` (0x887A0005)
- Engine entra em estado remoção e para, sinalizando `GpuStressResult.Removed`
- UI mostra badge "Proteção térmica" se temperatura passou de 90°C

## GPU Engine Native DLL

- `EME.Diagnostics.GpuEngine.dll` é nativa C++, compilada para x64
- Requer `D3D11_1.h` e DirectX 11 Runtime redistribuível já presente no Windows 10+
- Se falhar carregar → engine não inicia

## Banco de Dados

- Seed contém ~25 mil CPUs (incluindo laptop)
- Database de 31MB inclusa no instalador
- Arquiteturas Intel: Raptor Lake, Alder Lake, Meteor Lake, Arrow Lake, Lunar Lake
- Arquiteturas AMD: Zen 1-5, Dragon Range, Phoenix
