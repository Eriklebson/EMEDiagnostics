# Stress Test

Todos os motores implementam contratos em `EME.Diagnostics.Core.Services`:
- `ICpuStressEngine`
- `IGpuStressEngine`
- `IMemoryStressEngine`
- `IStorageStressEngine`

## CPU

`CpuStressEngine` — threads paralelas com operações matemáticas pesadas (PI computation, prime numbers, etc.).
Publica `CpuStressMetrics` via evento.

## GPU

`DirectX11GpuStressEngine` — compute shader em memória dedicada, mede dispatches e frame time.
Backend nativo C++ em `EME.Diagnostics.GpuEngine.dll`.
Detecta remoção do dispositivo pelo driver e respeita cancelamento.
Proteção térmica de 90°C usando snapshot do monitor de hardware.
O shader é incorporado ao motor nativo e não depende de modelos, cenas ou texturas externas.

## RAM

`MemoryStressEngine` — aloca chunks de 256 MB, preenche com padrões (0xAA, 0x55, 0xFF, 0x00, 0x69), verifica integridade.
Ao final, força GC e chama `SetProcessWorkingSetSize` para liberar.

## Storage

`StorageStressEngine` — leitura/escrita sequencial e aleatória em arquivo temporário.
Publica `StorageStressMetrics` com IOPS, throughput, latência.

## Combined

Executa CPU + GPU + RAM + Storage simultaneamente.
Usa `StressCatalogService` com `CancellationTokenSource` compartilhado.

Na página principal, o controle combinado usa um único botão que alterna entre `Executar todos` e `Parar todos`. A duração pode ser selecionada entre 30 segundos, 1, 5, 10 ou 30 minutos, 1 hora, execução ilimitada e um valor personalizado informado em minutos. O seletor fica desabilitado durante a execução.

Cada card de CPU, GPU, memória e disco também possui seu próprio seletor com as mesmas durações, permitindo executar testes separados com limites diferentes. Durante a execução, o card exibe tempo decorrido e limite no formato `00:13/30:00` ou `00:15/01:00:00`; testes ilimitados usam `--:--:--` no limite. O seletor individual fica bloqueado somente enquanto seu teste ou o teste combinado estiver ativo.
