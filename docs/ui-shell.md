# Shell e Navegação

## Layout

Sidebar fixa de 256px à esquerda. A área principal ocupa toda a largura restante da janela, com padding interno de 32px; não há margem externa nem limite central de largura.
Tudo construído em código C# (sem XAML de layout) em `MainWindow.xaml.cs`.

## Sidebar

5 itens de navegação:
| Item | Rota | Status |
|------|------|--------|
| Dashboard | `Dashboard()` | ✅ Funcional |
| Stress Test | `StressTest()` | ✅ Funcional |
| Hardware | `Hardware()` | ✅ Funcional |
| Relatórios | `ReportsPageAsync()` | ✅ Funcional |
| Configurações | `Placeholder()` | ❌ Placeholder |

## Theme (DesignTokens)

Cores do tema escuro:
- Fundo: `#0D0F10`
- Sidebar: `#080A0B`
- Cartão: `#17191A`
- Área interna: `#111314`
- Texto principal: `#F1F2F2`
- Texto secundário: `#8B9093`
- Acento principal: `#42D286`
- Informação/gráficos de GPU: `#43A8E5`
- Alerta térmico: `#FFB21C`
- Borda: `#2A2D2F`

O item ativo da sidebar recebe fundo elevado, texto claro e marcador verde na borda esquerda. Os cabeçalhos exibem o estado online/coleta, divisória e hierarquia tipográfica inspirada em painéis de telemetria.

O rodapé da sidebar permanece fixado na base e exibe a versão Windows com quatro partes, por exemplo `v1.2.0.0 • Release`.

## Páginas

### Dashboard
Abre com quatro cartões compactos de telemetria (CPU, GPU, memória e disco SSD) e três gráficos multissérie. O gráfico da CPU apresenta uso, CPU/CCD e PKG; o gráfico da GPU apresenta uso e temperatura; o gráfico do disco apresenta temperatura, leitura e escrita. Cada série possui cor, legenda, valor atual e preenchimento em gradiente sob a linha. O card inferior de armazenamento apresenta a capacidade real da unidade do sistema, dividida em espaço usado, livre e total.

Nos cartões de CPU e GPU, a linha principal distribui o percentual de uso à esquerda e as temperaturas em tipografia menor à direita. Em CPUs AMD com sensores equivalentes, `CPU` usa a leitura do CCD/Tdie e `PKG` usa Package ou Tctl/Tdie; em outros modelos são usados Core Average/Core e Package com fallback seguro. A GPU exibe sua temperatura principal. Sensores indisponíveis são representados por `—`.

Somente os valores térmicos recebem cor por faixa: verde abaixo de 60 °C, âmbar entre 60 °C e 79 °C e vermelho a partir de 80 °C.

### Stress Test
Grade 2x2 de cartões para CPU, GPU, memória e disco. Cada cartão apresenta identificação, ações iniciar/parar, métricas reais específicas do componente, gráfico multissérie e estado de execução. CPU mostra uso/CPU/PKG; GPU mostra uso/temperatura e agrupa VRAM total, usada e livre em um único bloco compacto; memória mostra usada/total/livre; disco mostra uso/leitura/escrita. Os textos dos blocos usam as cores das respectivas linhas, enquanto a legenda do gráfico evita repetir os valores. A ação global “Executar todos” aciona o teste combinado.

A legenda multissérie da GPU permanece em uma única linha. Cada card possui seletor de duração independente e cronômetro no formato `decorrido/limite`, incluindo suporte ao limite ilimitado (`--:--:--`).

O cabeçalho do Stress Test inclui duração global (30 s, 1 min, 5 min, 10 min, 30 min, 1 h, ilimitado ou minutos personalizados). Durante o teste combinado, o mesmo botão muda para `Parar todos` e o seletor de duração é bloqueado.

O subtítulo de cada cartão identifica o hardware real detectado, e não ferramentas de benchmark de referência: modelo da CPU, modelo da GPU, módulo de memória físico e unidade de armazenamento.

### Hardware
Grade 3x2 de inventário: Processador, Placa de vídeo, Memória, Placa-mãe, Armazenamento e Térmico. Cada cartão apresenta pares chave/valor provenientes da telemetria real e os atualiza continuamente. O card da placa-mãe agrega os sensores do Super I/O ou controlador embarcado associado ao modelo identificado.

### Relatórios
Tabela única com colunas ID, Teste, Data, Duração, Pico térmico, Status e ações. A própria linha pode ser expandida e revela um painel integrado com seis métricas (carga média, clock médio, temperatura média, consumo de pico, throttling e erros) e um registro cronológico dos eventos. O botão PDF permanece independente.

### Rede

Quando a instalação atua como servidor principal, cada máquina remota possui um único card com estado online/histórico e seus relatórios recebidos aninhados. Máquinas offline permanecem consultáveis enquanto houver histórico salvo. O botão `PDF` gera a cópia em Documentos na primeira abertura e reutiliza o arquivo nas seguintes.

## Responsividade

- Dashboard: telemetria em 4 colunas no desktop, 2 em larguras intermediárias e 1 em janelas estreitas; gráficos e painéis inferiores também são empilhados.
- Stress Test: 2 colunas a partir de 960 px de área útil e 1 coluna abaixo disso.
- Hardware: 3 colunas no desktop, 2 a partir de 448 px e 1 em larguras menores.
- Relatórios: a tabela preserva as 7 colunas e oferece rolagem horizontal em janelas estreitas.
- Os ícones de memória e PDF usam símbolos nativos do WinUI para evitar glifos ausentes exibidos como quadrados.
