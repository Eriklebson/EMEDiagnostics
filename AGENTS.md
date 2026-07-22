# E.M.E Diagnostics — Diretrizes para agentes

## Idioma

Toda comunicação, documentação e texto de interface deve ser escrito em português do Brasil. Identificadores de código permanecem em inglês.

## Arquitetura

- Preserve Clean Architecture, SOLID, MVVM e separação de responsabilidades.
- `Core` não depende de UI nem infraestrutura.
- `Hardware`, `Services` e `Reporting` dependem apenas de contratos necessários.
- A UI não acessa LibreHardwareMonitor, WMI, DirectX ou arquivos diretamente.
- Motores gráficos futuros implementam `IGpuStressEngine`; a UI não conhece DirectX, Vulkan ou OpenGL.
- Não use PowerShell, processos externos, cache JSON ou arquivos temporários para coleta de hardware.

## Fluxo obrigatório

1. Leia `AGENTS.md` e a documentação relevante em `docs/`.
2. Antes de corrigir um problema, diagnostique e confirme a causa raiz.
3. Faça alterações pequenas e verificáveis.
4. Compile e execute os testes disponíveis.
5. Atualize a documentação e `CHANGELOG_AI.md`.
6. Nunca faça operações Git sem autorização explícita do usuário.

## Performance

- Prefira operações assíncronas, cancelamento e eventos.
- Não bloqueie a thread da UI.
- Colete sensores em background e publique snapshots imutáveis.
- Use a melhor fonte por dado; LHM para sensores dinâmicos e APIs do Windows para informações estáticas quando adequado.

## Escopo atual

Stress Test, Benchmark e Relatórios são telas estruturais. Não simule resultados nem implemente cargas reais até solicitação explícita.
