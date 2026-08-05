# Rede local

## Visão geral

O módulo `EME.Diagnostics.Networking` conecta instalações do E.M.E Diagnostics na mesma LAN. Uma máquina pode operar como principal e receber automaticamente os PDFs gerados pelas máquinas clientes.

## Descoberta e conexão

- Descoberta UDP: porta `8432`.
- Servidor HTTP: porta `8500`.
- Anúncio: `EMEDIAG_SERVER|{hostName}|{port}`.
- Heartbeat: a cada 5 segundos.
- Cliente considerado inativo após 15 segundos.
- O servidor anuncia em cada interface IPv4 válida usando o broadcast específico da sub-rede.

## Fluxo

1. `ClientService` escuta anúncios UDP ao iniciar o aplicativo.
2. A máquina principal inicia `ServerService` pela tela Rede.
3. O cliente define a URL HTTP do servidor encontrado e inicia o heartbeat.
4. Ao concluir um teste, `MainViewModel` gera o PDF e chama `SendReportAsync`.
5. O servidor principal salva o PDF em `%PROGRAMDATA%\EME\Diagnostics\network_reports` e atualiza `reports_index.json`.
6. A tela Rede agrupa os relatórios dentro do card da máquina que os enviou e permite reabri-los mesmo quando a máquina estiver offline.

Na primeira execução após a mudança de diretório, arquivos existentes em `%LOCALAPPDATA%\EMEDiagnostics\network_reports` são copiados automaticamente para o armazenamento compartilhado da máquina principal. O diretório em `ProgramData` torna o histórico independente do usuário Windows que abriu o servidor.

O botão `PDF` de um relatório recebido cria uma cópia determinística em `Documents\EMEDiagnostics` apenas quando ela ainda não existe e abre o visualizador padrão. Cliques seguintes apenas abrem o mesmo arquivo.

## Endpoints internos

| Método | Rota | Uso |
|---|---|---|
| GET | `/api/ping` | Verificação básica do servidor |
| GET | `/api/clients` | Lista de máquinas conectadas |
| GET | `/api/reports` | Lista de relatórios recebidos |
| POST | `/api/client/heartbeat` | Registro e atualização do cliente |
| POST | `/api/reports` | Envio do PDF e metadados do teste |

## Firewall e diagnóstico

O servidor tenta criar regras para TCP 8500 e UDP 8432. Como o aplicativo solicita privilégios administrativos, a criação normalmente ocorre na primeira execução. Falhas e eventos de conexão são registrados em `%LOCALAPPDATA%\EMEDiagnostics\network_trace.log`.

## Cuidados de manutenção

- O protocolo atual é destinado à rede local e usa HTTP sem TLS.
- Não expor as portas diretamente à internet.
- Manter `NetworkConstants` como fonte das portas e intervalos.
- Mudanças no formato dos anúncios ou endpoints devem ser compatíveis entre cliente e servidor.
- O índice persistido deve continuar tolerando arquivos removidos manualmente; entradas órfãs são descartadas na carga.
- Os cards são identificados por `MachineId`, evitando misturar computadores diferentes que tenham nomes iguais.
