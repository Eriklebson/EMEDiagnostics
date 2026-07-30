# Versionamento

## SemVer

Este projeto segue Semantic Versioning: `MAJOR.MINOR.PATCH`

## Fonte da verdade

`src/EME.Diagnostics.Shared/ProductInfo.cs`:

```csharp
public const string Version = "1.0.0";
```

## Arquivos para atualizar

| Arquivo | Localização | Formato | Exemplo |
|---------|-------------|---------|---------|
| `src/EME.Diagnostics.Shared/ProductInfo.cs` | `Version` | 3-part | `1.0.1` |
| `src/EME.Diagnostics.App/app.manifest` | `assemblyIdentity version` | 4-part | `1.0.1.0` |
| `installer.iss` | `AppVersion` + `OutputBaseFilename` | 4-part | `1.0.1.0` |
| `CHANGELOG_AI.md` | Tabela de versões | 3-part | `1.0.1` |
| `README.md` | Tabela de versões no final | 3-part | `1.0.1` |

## Quando incrementar

**PATCH** (3º dígito): bugs, performance, pequenas melhorias.
**MINOR** (2º dígito): novas features, novos módulos, novas integrações.
**MAJOR** (1º dígito): breaking changes, grandes redesigns, reescritas.
