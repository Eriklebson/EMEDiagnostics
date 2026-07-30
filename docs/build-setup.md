# Build e Setup

## Build seguro

NUNCA executar build enquanto o `EME.Diagnostics.App.exe` estiver rodando.

1. Verificar processo: `Get-Process -Name "EME.Diagnostics.App" -ErrorAction SilentlyContinue`
2. Matar: `taskkill /F /IM "EME.Diagnostics.App.exe"` + aguardar 3s
3. Build: `dotnet build src\EME.Diagnostics.App\EME.Diagnostics.App.csproj -c Release -p:SkipGpuEngine=true -p:Platform=x64 --self-contained false`
4. Verificar 0 erros, 0 warnings

## Release

```
dotnet build ... -c Release ...
xcopy /y src\...\bin\x64\Release\net8.0-...\win-x64\* release\
```

## Instalador (Inno Setup)

```
& "C:\Users\erikl\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer.iss
```

Flags do instalador:
- `PrivilegesRequired=admin` — requer admin
- `SolidCompression=yes` — compressão LZMA2/ultra64
- Instala PawnIO_setup.exe automaticamente ao final (`CurStepChanged(ssPostInstall)`)
- Cria diretório em `{commonappdata}\EME\HardwareDatabase` com permissão `users-modify`

Output: `installer\EMEDiagnostics_v{VERSAO}_Setup.exe`

## GitHub Release

```
gh release create v{VERSION} --title "v{VERSION}" --notes "notas"
gh release upload v{VERSION} "installer\EMEDiagnostics_v{VERSION}_Setup.exe" --clobber
gh release upload v{VERSION} "installer\EMEDiagnostics_v{VERSION}.zip" --clobber
```
