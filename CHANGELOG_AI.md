# Relatório de desenvolvimento

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
