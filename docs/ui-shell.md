# Shell e Navegação

## Layout

Sidebar fixa de 224px à esquerda, conteúdo flexível à direita.
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
- Fundo: `#0A0B0D`
- Sidebar: `#161719`
- Cartão: `#2A2D31`
- Área interna: `#1B1D22`
- Texto principal: `#E8E9EB`
- Texto secundário: `#A8ABB0`
- Acento: `#4CCBA0`

## Páginas

### Dashboard
Lista todos os dispositivos detectados com sensores em grid (nome, tipo, atual, mínimo, máximo).

### Stress Test
Cards para CPU, GPU, RAM, Storage e Combined, cada um com botões iniciar/parar e gráfico em tempo real.

### Hardware
Cards de métricas resumidas: Processador, Placa de vídeo, Memória RAM, Ventoinhas.

### Relatórios
Lista de relatórios salvos com botões de exportar PDF, ver detalhes (collapse/expand) e excluir.
