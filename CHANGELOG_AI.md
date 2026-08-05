# Relatório de desenvolvimento

## 2026-08-05 — v1.4.0 — telemetria avançada e Stress Test

- Corrigida a atualização visual do botão combinado para `Parar todos` e adicionada a duração `Ilimitado`.
- O teste combinado ganhou seleção de duração com opção personalizada e botão único que alterna entre Executar todos e Parar todos.
- VRAM agrupada em um único bloco compacto no Stress Test; valores foram removidos das legendas dos gráficos e os cards passaram a usar as cores das séries.
- Removidas as métricas artificiais Atual/Média/Pico do Stress Test; cada card agora mostra telemetria real específica e as mesmas informações em linhas individuais nos gráficos.
- Os cartões do Stress Test agora identificam CPU, GPU, memória e disco reais no lugar de nomes como Prime95, FurMark e MemTest.
- Corrigido o raio excessivo da barra de capacidade do armazenamento; o preenchimento agora usa raio fixo de 2 px.
- O card inferior de Armazenamento agora mostra capacidade real do disco do sistema: espaço usado, livre e total.
- Restaurado o preenchimento sombreado em gradiente sob cada série dos gráficos multissérie do Dashboard.
- Os gráficos do Dashboard passaram a suportar múltiplas linhas e valores por série: uso/CPU/PKG, uso/temperatura da GPU e temperatura/leitura/escrita do SSD.
- O quarto cartão do Dashboard deixou de repetir a maior temperatura e passou a apresentar o SSD, com temperatura, leitura e escrita em tempo real.
- Corrigida a distinção térmica da CPU: CCD/Tdie é apresentado como `CPU`, enquanto Package/Tctl/Tdie é apresentado como `PKG`; números térmicos agora mudam entre verde, âmbar e vermelho por faixa.
- Dashboard atualizado para exibir temperaturas ao lado do percentual de uso: `CPU | PKG` no cartão da CPU e temperatura principal no cartão da GPU.
- Documentação de continuidade adicionada com visão geral da versão 1.3.0, mapa funcional, arquivos de entrada, checklist de atualização e descrição completa do módulo de rede.
- Implementados os breakpoints responsivos do layout de referência no Dashboard, Stress Test e Hardware; Relatórios preserva a tabela com rolagem horizontal em telas estreitas.
- Substituídos os glifos quebrados de memória e PDF por símbolos nativos do WinUI.

## 2026-08-05 — v1.3.0 — redesign visual e responsividade

- Rodapé de versão fixado na base da sidebar e exibição alterada para o formato Windows de quatro partes (`v1.3.0.0`); a release SemVer permanece corretamente em `1.3.0`.
- Substituído o raio de cápsula dos indicadores “Coletando” e de status por raios fixos de 8 px e 6 px, evitando deformação visual no WinUI.
- Corrigido clique duplo no collapse de Relatórios removendo a sobreposição entre os eventos da linha e do chevron.
- Indicadores “Coletando” e “Aprovado”, botão PDF e chevron ajustados às dimensões, padding, cores e bordas medidos na referência web.
- Collapse de Relatórios redesenhado conforme a referência: linha clicável, métricas em grade 2x3, registro de eventos, chevron e exportação PDF independente.
- Removido o limite central de 1280 px que gerava margens excessivas em monitores largos; o corpo das páginas agora usa toda a largura disponível.
- Corrigida falha de inicialização `0xc000027b`: o `ProgressBar` do armazenamento solicitava o recurso WinUI ausente `TabViewScrollButtonBackground`; a barra agora é desenhada pelo aplicativo.
- Exceções fatais capturáveis da interface passam a ser registradas em `%LOCALAPPDATA%\EMEDiagnostics\ui_crash.log`.

- Corrigida falha de inicialização `0xc000027b`: o `ProgressBar` do armazenamento solicitava o recurso WinUI ausente `TabViewScrollButtonBackground`; a barra agora é desenhada pelo aplicativo.
- Exceções fatais capturáveis da interface passam a ser registradas em `%LOCALAPPDATA%\EMEDiagnostics\ui_crash.log`.

- Criado backup externo completo do estado anterior em `C:\laragon\www\EMEDiagnostics_Backups` antes das alterações.
- Atualizada a fundação Cyber Dark com fundo, sidebar, cartões, bordas e superfícies mais próximos da referência visual aprovada.
- Sidebar ampliada para 256 px, com item ativo destacado por superfície elevada e marcador verde.
- Cabeçalhos passaram a exibir estado do sistema, indicador de coleta e separador visual.
- Dashboard reorganizado com quatro cartões principais: CPU, GPU, memória e temperatura.
- Cores dos gráficos harmonizadas: verde para CPU, azul para GPU e superfícies internas mais escuras.
- Conteúdo principal centralizado em uma coluna de 1280 px, igual à proporção desktop da referência.
- Criado `CompactAreaChart`, controle WinUI para gráficos compactos com grade, linha e preenchimento em gradiente.
- Dashboard reconstruído com gráficos CPU/GPU/temperatura e painel de armazenamento.
- Stress Test reconstruído como grade 2x2 de cartões com métricas Atual/Média/Pico, gráficos integrados e controles funcionais.
- Hardware reconstruído como grade 3x2 de cartões de inventário com dados reais disponíveis.
- Relatórios reconstruído como tabela expansível com badges de status e exportação PDF por linha.
- Corrigida falha de inicialização `0xc000027b`: o `ProgressBar` do armazenamento solicitava o recurso WinUI ausente `TabViewScrollButtonBackground`; a barra agora é desenhada pelo aplicativo.
- Exceções fatais capturáveis da interface passam a ser registradas em `%LOCALAPPDATA%\EMEDiagnostics\ui_crash.log`.
- Preservadas as funções e alterações locais preexistentes durante o redesign.

## 2026-07-30 — v1.2.0 — Rede LAN, modo Servidor/Cliente, auto-envio de PDF

- **Adicionado** novo projeto `EME.Diagnostics.Networking` com `ServerService`, `ClientService`.
- **Servidor**: ao clicar "Tornar Principal", abre servidor HTTP `:+8500` com endpoints REST (`/api/reports`, `/api/clients`, `/api/ping`). Anuncia-se na rede via UDP broadcast na porta 8432.
- **Cliente**: escuta UDP broadcast, detecta servidor automaticamente, conecta e envia heartbeat a cada 5s.
- **Auto-envio**: após cada teste de estresse, se conectado ao servidor, o PDF é enviado automaticamente via `POST /api/reports`.
- **UI**: nova página "Rede" na sidebar com: botão Tornar Principal/Parar servidor, status de conexão, lista de clientes online, relatórios recebidos.
- **REST compatível com mobile**: endpoints GET para listar e baixar PDFs — preparado para futuro app de celular.
- **Sem dependências externas**: tudo com `HttpListener`, `UdpClient`, `HttpClient` (BCL do .NET).
- **Build** 0 erros, 0 warnings.

## 2026-07-30 — v1.1.0 — PASS/RECUSADO por throttling

- **Adicionado** `StressTestResult` enum: Pass, RecusadoCpu, RecusadoGpu, RecusadoCpuGpu.
- **Adicionado** `Result` field em `StressReportSummary` e `StressReportDetail`.
- **Adicionado** `ComputeThrottlingResult()` no `StressDataCollector` — analisa clock de CPU e GPU durante o teste. Se clock cair >25% na segunda metade vs primeira metade, é throttling.
- **Gestão visual**: resultado exibido no card de relatório (verde para PASS, vermelho para RECUSADO).
- **Detalhes**: badge de resultado no collapse com fundo semi-transparente verde/vermelho.
- **PDF**: carimbo no final do relatório com rotação diagonal (15-25°) e posição semi-aleatória para efeito realista de carimbo.
- **Banco**: coluna `Result` na tabela `Reports` com migração automática para DBs existentes.
- **Corrigido** GPU stress bloqueado — `EME.Diagnostics.GpuEngine.dll` ausente no `release/` por build com `SkipGpuEngine=true`. DLL restaurada do release v1.0.0.0.
- **Build** 0 erros, 0 warnings.

- **Adicionado** `AGENTS.md` completo com regras de idioma, consulta obrigatória, performance, versionamento, build seguro e README.
- **Criada** estrutura de documentação em `docs/` com 8 arquivos: architecture, hardware-monitor, database, reporting, ui-shell, stress-test, build-setup, known-issues, versioning.
- **Instalador**: banco SQLite de 31MB incluso no setup (extraído para `%PROGRAMDATA%\EME\HardwareDatabase`).
- **Instalador**: `PawnIO_setup.exe` incluso e executado silenciosamente ao final da instalação (`CurStepChanged(ssPostInstall)`).
- **Adicionado** `DiagnosticLogger.cs` para logging de diagnóstico.
- **Instalador**: permissão `users-modify` no diretório do banco de dados.
- Build 0 erros, 0 warnings.

## 2026-07-26 — v1.0.1

- **Corrigido** `InvalidCastException` no `CpuRepository.GetByIdAsync<T>()` ao converter `long` (SQLite INTEGER) para `int?` via `Convert.ChangeType` — adicionado suporte a `Nullable<T>` com `Nullable.GetUnderlyingType`.
- **Removido** logging temporário em `%TEMP%\EME_LHM.log` que violava regra do AGENTS.md (arquivos temporários para coleta de hardware).
- **app.manifest**: alterado de `asInvoker` para `requireAdministrator` para permitir que o LibreHardwareMonitor enumere sensores de hardware completos.
- Build 0 erros, 0 warnings.
- Instalador compilado para v1.0.1.0.

## 2026-07-21 — Estrutura inicial

- Criada a solução inicial em camadas.
- Documentado o padrão visual herdado conceitualmente do E.M.E Core.
- Preparadas navegação, injeção de dependência e telas estruturais.
- Implementada coleta real inicial via LibreHardwareMonitor, sem PowerShell.
- Preparada abstração para motores gráficos futuros.

## 2026-07-21 — Dashboard detalhada de hardware

- A Dashboard passou a apresentar um card vertical de largura total para cada hardware detectado.
- Todos os hardwares e sub-hardwares do LibreHardwareMonitor são percorridos recursivamente.
- Cada card lista os sensores disponíveis com tipo, valor atual, mínimo, máximo, unidade e identificador interno.
- Componentes sem sensores dinâmicos continuam visíveis e são identificados claramente.
- Mantida coleta direta em C#, sem PowerShell ou arquivos intermediários.
- Corrigido crash `0xc000027b` no compositor WinUI: a árvore visual deixou de ser reconstruída a cada segundo; agora somente os textos dos sensores são atualizados, e os cards são recriados apenas quando a estrutura de hardware muda.

## 2026-07-22 — Renderizador real com Sponza, PBR, bloom e sombras (em andamento)

- Baixado modelo Sponza glTF (25 malhas, centro ≈ (−0,5; 5,2; −0,3) e raio ≈ 14,9).
- Criado `RealSceneRenderer`: C++ com shaders embutidos HLSL 5.0.
- Implementados PBR Cook‑Torrance (GGX, Smith, Fresnel‑Schlick), shadow map PCF 3×3, ACES tonemapping, bloom com bright‑pass + Gaussiano separável, passada de composição final.
- Adicionados `QualitySettings` e níveis Low/Medium/High/Ultra com resolução de shadow map e bloom liga/desliga.
- Expandido `GpuStressEngine` para encaminhar qualityLevel, unbind UAV compute antes dos passes de render e modo contínuo (sem limite de tempo).
- C#: `GpuStressOptions.QualityLevel`, `DirectX11GpuStressEngine.StartAsync` aceita `TimeSpan.Zero` como contínuo, `MainViewModel` expõe qualidade, `MainWindow` com ComboBox de qualidade e status "CONTÍNUO".
- Adicionado log em arquivo (`gpu_stress_debug.log`) no `RealSceneRenderer` e `GpuStressEngine`.
- **Corrigido** `FrontCounterClockwise = TRUE → FALSE` no rasterizer da cena principal (Sponza usa winding CW, compatível com DirectX).
- **Corrigido** shadow pass: agora seta `IASetInputLayout` e `IASetPrimitiveTopology` antes de desenhar (antes usava estado da frame anterior).
- **Corrigido** ordem de execução: constant buffer atualizado **antes** do shadow pass (antes o shadow pass usava `cbScene_` não inicializado na primeira frame).
- Compilação bem‑sucedida de toda a solução (C++ via MSBuild, C# via VS MSBuild).

- Criado `EME.Diagnostics.GpuEngine`, backend nativo C++/DirectX 11 Compute.
- Implementada carga compute controlada sem dependência do FurMark.
- Adicionadas métricas de tempo, progresso, dispatches por segundo, frame time, VRAM reservada e erros.
- Adicionados cancelamento, detecção de remoção da GPU pelo driver e proteção térmica em 90 °C.
- Ativado o cartão de GPU na tela Stress Test, mantendo `IGpuStressEngine` desacoplado da UI.
- Adicionada janela 3D DirectX 11 dedicada, com cena procedural do núcleo E.M.E, três anéis animados, iluminação, estrelas e cores azul/amarela/roxa.
- Fechar a janela 3D agora encerra o teste de GPU de forma controlada.
- Substituída a demonstração do núcleo por uma cinematic medieval procedural mais pesada, com cidade, castelo, torres, telhados, lua, estrelas, tochas, janelas emissivas, sombras suaves, ambient occlusion e neblina.
- Elevada a cena para 1600×900 e adicionados materiais procedurais de pedra/telha/calçamento, BRDF PBR, fogo visível, nuvens animadas, partículas de brasa e tone mapping ACES.
- Adicionada estrada circular de terra com sulcos de rodas, pedras e variação de solo, além de uma carroça animada percorrendo a cidade.
- Baixado e auditado o Medieval Village MegaKit CC0 para a futura substituição da geometria procedural por malhas e texturas reais.

## 2026-07-22 — Terreno procedural, cena medieval na superfície, IBL com sky cubemap, sunset

- Substituído o piso plano por um **terreno gerado por heightmap** (257×257) com Perlin noise multioitava, produzindo colinas suaves e um vale central para a vila.
- **BuildTerrainMesh**: malha com subdivisão uniforme, normais calculadas por diferenças centrais no heightmap, UVs para texturização.
- **Casa, igreja e árvores** agora amostram a altura do terreno (`SampleHM`) para assentar corretamente na superfície.
- **Cercas, postes de luz, poço, bancos e barracas** também seguem o relevo.
- **Estrada principal e transversal** convertidas de retângulos planos para malhas segmentadas que acompanham o terreno.
- **Câmera da cinematic** ajustada para percorrer o terreno com altura relativa ao heightmap (caminho spline agora tem `p.y += SampleHM(...)`).
- **IBL real**: removida a função `GetSky()` procedural do pixel shader; agora a iluminação difusa e especular ambiente usa `Sky.SampleLevel` no cubemap gerado.
- **Céu sunset**: `FillSkyFace` reescrito com gradiente poente (laranja/rosado no horizonte, azul escuro no zênite) + glow dourado + disco solar.
- **Névoa baseada em altura**: `heightFog` combinada com `distFog`, cor da névoa amostrada do cubemap do céu para integração visual.
- **Bloom**: kernel Gaussiano expandido de 6 taps para 8 taps com `exp(-j²·0.15)`, intensidade aumentada para 0.12.
- **Iluminação sunset**: direção da luz ajustada para `(-0.2, -0.85, 0.45)` com cor mais quente `(1.6, 1.1, 0.5)` e intensidade 2.5.
- **Member variables** `heightmap_`, `hmRes_`, `worldSize_` adicionados à classe `VillageScene` para acesso no método `Render`.

## 2026-07-25 — Release v1.0.0.0, README, instalador Inno Setup

- README.md completo com features, arquitetura, build e sistema visual
- `installer.iss` (Inno Setup) para gerar instalador do EMEDiagnostics
- `docs/logo.ico` convertido do logo EMECore para uso no instalador
- `app.manifest` atualizado para versão 1.0.0.0
- `.gitignore` atualizado com `release/`, `installer/`, `*.zip`
- Release v1.0.0.0 criada no GitHub com artefato ZIP
