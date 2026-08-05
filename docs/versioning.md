# Versionamento

## SemVer

Este projeto segue Semantic Versioning: `MAJOR.MINOR.PATCH`

## Fonte da verdade

`src/EME.Diagnostics.Shared/ProductInfo.cs`:

```csharp
public const string Version = "1.3.0";
public const string WindowsVersion = Version + ".0";
```

## Arquivos para atualizar

| Arquivo | Localização | Formato | Exemplo |
|---------|-------------|---------|---------|
| `src/EME.Diagnostics.Shared/ProductInfo.cs` | `Version` | 3-part | `1.3.0` |
| `src/EME.Diagnostics.Shared/ProductInfo.cs` | `WindowsVersion` | 4-part | `1.3.0.0` |
| `src/EME.Diagnostics.App/app.manifest` | `assemblyIdentity version` | 4-part | `1.3.0.0` |
| `installer.iss` | `AppVersion` + `OutputBaseFilename` | 4-part | `1.3.0.0` |
| `CHANGELOG_AI.md` | Tabela de versões | 3-part | `1.3.0` |
| `README.md` | Tabela de versões no final | 3-part | `1.3.0` |

`Version` identifica a release SemVer. `WindowsVersion` acrescenta a revisão formal `0` e é usada na sidebar e nos artefatos do Windows.

## Quando incrementar

**PATCH** (3º dígito): bugs, performance, pequenas melhorias.
**MINOR** (2º dígito): novas features, novos módulos, novas integrações.
**MAJOR** (1º dígito): breaking changes, grandes redesigns, reescritas.
