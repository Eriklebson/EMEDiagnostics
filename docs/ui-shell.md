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
Abre com quatro cartões compactos de telemetria (CPU, GPU, memória e maior temperatura atual), dois gráficos de área para CPU/GPU e uma faixa inferior com temperatura consolidada e armazenamento. CPU usa verde, GPU usa azul e temperatura usa âmbar.

### Stress Test
Grade 2x2 de cartões para CPU, GPU, memória e disco. Cada cartão apresenta identificação, ações iniciar/parar, métricas Atual/Média/Pico, gráfico de área e estado de execução. A ação global “Executar todos” aciona o teste combinado.

### Hardware
Grade 3x2 de inventário: Processador, Placa de vídeo, Memória, Placa-mãe, Armazenamento e Térmico. Cada cartão apresenta pares chave/valor provenientes da telemetria real disponível.

### Relatórios
Tabela única com colunas ID, Teste, Data, Duração, Pico térmico, Status e ações. A própria linha pode ser expandida e revela um painel integrado com seis métricas (carga média, clock médio, temperatura média, consumo de pico, throttling e erros) e um registro cronológico dos eventos. O botão PDF permanece independente.

## Responsividade

- Dashboard: telemetria em 4 colunas no desktop, 2 em larguras intermediárias e 1 em janelas estreitas; gráficos e painéis inferiores também são empilhados.
- Stress Test: 2 colunas a partir de 960 px de área útil e 1 coluna abaixo disso.
- Hardware: 3 colunas no desktop, 2 a partir de 448 px e 1 em larguras menores.
- Relatórios: a tabela preserva as 7 colunas e oferece rolagem horizontal em janelas estreitas.
- Os ícones de memória e PDF usam símbolos nativos do WinUI para evitar glifos ausentes exibidos como quadrados.
