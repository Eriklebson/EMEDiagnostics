# Visão geral do sistema

Documento de entrada para manutenção e próximas atualizações do E.M.E Diagnostics.

## Estado atual

- Versão SemVer: `1.4.0`.
- Versão Windows e instalador: `1.4.0.0`.
- Plataforma: Windows x64, .NET 8 e WinUI 3.
- Release pública: `v1.4.0`.
- Branch principal: `main`.
- Interface: tema Cyber Dark, sidebar fixa de 256 px e páginas responsivas.

## Objetivo

O aplicativo monitora hardware, executa testes controlados de CPU, GPU, memória e armazenamento, salva resultados, gera PDFs e compartilha relatórios entre máquinas da mesma rede local.

## Mapa funcional

| Página | Responsabilidade | Estado |
|---|---|---|
| Dashboard | Resumo de CPU, GPU, memória, temperatura, gráficos e armazenamento | Funcional |
| Stress Test | Testes individuais e combinado, métricas e gráficos em tempo real | Funcional |
| Hardware | Inventário e sensores detectados | Funcional |
| Relatórios | Histórico local, detalhes expansíveis, exclusão e PDF | Funcional |
| Rede | Modo principal/cliente, máquinas conectadas e relatórios recebidos | Funcional |
| Configurações | Preferências futuras | Placeholder |

## Componentes principais

| Projeto | Responsabilidade |
|---|---|
| `EME.Diagnostics.App` | Interface WinUI, navegação, bindings e composição visual |
| `EME.Diagnostics.Core` | Modelos e contratos independentes de infraestrutura |
| `EME.Diagnostics.Hardware` | Coleta e normalização de sensores com LibreHardwareMonitor |
| `EME.Diagnostics.Services` | Motores de stress, coleta e persistência de relatórios |
| `EME.Diagnostics.Reporting` | Geração de PDF com QuestPDF |
| `EME.Diagnostics.Networking` | Descoberta LAN, heartbeat, servidor HTTP e envio de PDFs |
| `EME.Diagnostics.GpuEngine` | Backend nativo DirectX 11 para stress de GPU |
| `EME.HardwareDatabase` | Catálogo SQLite e resolução de perfis de hardware |
| `EME.Diagnostics.Shared` | Nome e versão do produto |

## Arquivos de entrada para alterações

- Shell e páginas: `src/EME.Diagnostics.App/MainWindow.xaml.cs`.
- Estado e comandos: `src/EME.Diagnostics.App/ViewModels/MainViewModel.cs`.
- Injeção de dependência e inicialização: `src/EME.Diagnostics.App/App.xaml.cs`.
- Cores e medidas globais: `src/EME.Diagnostics.App/Theme/DesignTokens.cs`.
- Gráficos: `Controls/CompactAreaChart.cs` e `Controls/TelemetryChart.cs`.
- Versão: `src/EME.Diagnostics.Shared/ProductInfo.cs`.
- Empacotamento: `installer.iss`.

## Responsividade da versão 1.3.0

- Dashboard: 4, 2 ou 1 coluna conforme a largura; gráficos e painéis inferiores são empilhados quando necessário.
- Stress Test: 2 colunas a partir de 960 px úteis; abaixo disso, 1 coluna.
- Hardware: 3 colunas no desktop, 2 em largura intermediária e 1 em janela estreita.
- Relatórios: mantém as 7 colunas e usa rolagem horizontal em telas menores.
- Sidebar: permanece com 256 px, conforme a referência aprovada.

## Dados e arquivos locais

- Banco de relatórios: gerenciado por `ReportRepository` em armazenamento local do aplicativo.
- Relatórios recebidos: `%LOCALAPPDATA%\EMEDiagnostics\network_reports`.
- Índice dos relatórios de rede: `reports_index.json` dentro da pasta acima.
- Diagnóstico de rede: `%LOCALAPPDATA%\EMEDiagnostics\network_trace.log`.
- Falhas capturáveis da interface: `%LOCALAPPDATA%\EMEDiagnostics\ui_crash.log`.
- PDFs exportados: `%USERPROFILE%\Documents\EMEDiagnostics`.

## Checklist para próximas atualizações

1. Ler `AGENTS.md`, `docs/README.md` e os documentos relacionados à área alterada.
2. Confirmar a causa do problema antes de modificar o código.
3. Preservar MVVM e as fronteiras entre Core, UI e infraestrutura.
4. Atualizar a documentação e `CHANGELOG_AI.md`.
5. Encerrar o aplicativo antes do build.
6. Compilar em Release x64 e exigir 0 erros e 0 avisos.
7. Copiar o resultado para `release/` e reabrir o app como administrador.
8. Só alterar versão, criar commit, push ou GitHub Release mediante autorização expressa.

## Próximos pontos naturais

- Implementar a página Configurações.
- Dividir `MainWindow.xaml.cs` em controles/páginas menores mantendo o comportamento visual.
- Criar testes automatizados para persistência de relatórios e protocolo de rede.
- Revisar acessibilidade, escalonamento de DPI e navegação por teclado.
- Validar a responsividade em diferentes escalas do Windows, além da largura da janela.
