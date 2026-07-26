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
2. **ESTUDE antes de qualquer alteração**: Antes de modificar qualquer código, leia o código existente, identifique todos os problemas visuais/técnicos, pesquise referências e abordagens, e só então proponha um plano.
3. Antes de corrigir um problema, diagnostique e confirme a causa raiz.
4. Faça alterações pequenas e verificáveis.
5. Compile e execute os testes disponíveis.
6. Atualize a documentação e `CHANGELOG_AI.md`.
7. GIT: Só faça commit e push quando o usuário disser expressamente "pode subir", "sobe", "push" ou "commit". Caso contrário, não toque no Git.

## Performance

- Prefira operações assíncronas, cancelamento e eventos.
- Não bloqueie a thread da UI.
- Colete sensores em background e publique snapshots imutáveis.
- Use a melhor fonte por dado; LHM para sensores dinâmicos e APIs do Windows para informações estáticas quando adequado.

## Versionamento

Este projeto segue Semantic Versioning (SemVer).

### Formato de versão Windows Assembly

Manifestos Windows exigem versões de 4 partes: MAJOR.MINOR.PATCH.REVISION

A parte REVISION é sempre 0 a menos que especificamente necessário.

Exemplo: SemVer `1.0.1` vira assembly version `1.0.1.0`.

### Fonte da verdade

A versão SemVer (3 partes) é definida em `src/EME.Diagnostics.Shared/ProductInfo.cs`:

```csharp
public const string Version = "1.0.0";
```

### Arquivos para atualizar quando a versão mudar

| Arquivo | Localização | Formato | Exemplo |
|---------|-------------|---------|---------|
| `src/EME.Diagnostics.Shared/ProductInfo.cs` | `Version` | 3-part | `1.0.1` |
| `src/EME.Diagnostics.App/app.manifest` | `assemblyIdentity version` | 4-part | `1.0.1.0` |
| `installer.iss` | `AppVersion` + `OutputBaseFilename` | 4-part | `1.0.1.0` |
| `CHANGELOG_AI.md` | Tabela de versões | 3-part | `1.0.1` |

O display da sidebar (`MainWindow.xaml.cs`) usa `$"v{ProductInfo.Version}  •  Release"` e reflete automaticamente.

### Regras

**PATCH** (incrementa 3º dígito):
- Correções de bugs
- Correções de performance
- Pequenas melhorias internas

Exemplo: `1.0.0` → `1.0.1`

**MINOR** (incrementa 2º dígito, zera 3º e 4º):
- Novas features
- Novos módulos
- Novas integrações

Exemplo: `1.0.1` → `1.1.0`

**MAJOR** (incrementa 1º dígito, zera demais):
- Breaking changes
- Grandes mudanças de arquitetura
- Reescrevias completas
- Redesigns grandes

Exemplo: `1.9.4` → `2.0.0`

## Escopo atual

Stress Test, Benchmark e Relatórios são telas estruturais. Não simule resultados nem implemente cargas reais até solicitação explícita.

## Build e validação

### Build seguro

NUNCA executar build enquanto o `EME.Diagnostics.App.exe` estiver rodando.

1. Verificar se o processo existe: `Get-Process -Name "EME.Diagnostics.App" -ErrorAction SilentlyContinue`
2. Se existir, matar: `taskkill /F /IM "EME.Diagnostics.App.exe"` e aguardar 3s
3. Executar build
4. Verificar 0 erros

### Workflow pós-build

1. Matar processo antigo se existir
2. Buildar solução
3. Verificar 0 erros
4. Copiar output para `release/`
5. Compilar installer com ISCC
6. (Opcional) Lançar app para testar

### Inno Setup Installer

O instalador é compilado com:
```
& "C:\Users\erikl\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer.iss
```

Output em: `installer\EMEDiagnostics_v{VERSAO}_Setup.exe`

### GitHub Release

- `gh release create v{VERSION} --title "v{VERSION}" --notes "notas"`
- Upload assets: `EMEDiagnostics_v{VERSION}_Setup.exe` + `EMEDiagnostics_v{VERSION}.zip`
- Atualizar release existente: deletar assets antigos, subir novos
