# Switch Save Assistant

Aplicativo desktop para Windows destinado a visualizar, comparar, fazer backup e sincronizar saves de Nintendo Switch entre as fontes atualmente suportadas.

> **Estado:** pré-lançamento público. O instalador e o pacote portátil para Windows x64 estão disponíveis nos links acima.

Projeto independente, **sem afiliação, patrocínio ou endosso da Nintendo** ou dos desenvolvedores dos emuladores suportados.

[English README](README.md)

## Download

**[Baixar o instalador para Windows x64](https://github.com/ascc-br/switch-save-assistant/releases/download/v0.1.0-preview.2/SwitchSaveAssistant-Setup-0.1.0-preview.2-win-x64.exe)**

Última versão disponível: **0.1.0-preview.2 (pré-lançamento)**.

[ZIP portátil](https://github.com/ascc-br/switch-save-assistant/releases/download/v0.1.0-preview.2/SwitchSaveAssistant-Portable-0.1.0-preview.2-win-x64.zip) · [Notas da versão e hashes SHA-256](https://github.com/ascc-br/switch-save-assistant/releases/tag/v0.1.0-preview.2) · [Todas as versões](https://github.com/ascc-br/switch-save-assistant/releases)

Os pacotes incluem o runtime .NET. Você pode usar o aplicativo sem instalar o SDK.

## Fontes suportadas

| Fonte | Escopo atual |
| --- | --- |
| Nintendo Switch via DBI/MTP | Descoberta de saves e fluxo Switch ↔ Folder |
| Pasta local (Folder) | Repositório de saves escolhido pelo usuário |
| Emulador Eden | Descoberta de perfis e fluxo Eden ↔ Folder |
| Outros emuladores | Implementação adiada |

## Funcionalidades

- Listagem e comparação de saves por jogo e perfil, quando a fonte fornece as informações necessárias.
- Sincronização explícita entre origens/destinos compatíveis, com ferramentas de segurança e restauração.
- **Perfis lógicos:** diferentes identidades físicas (por exemplo, nome no Switch e ID estável no Eden) podem pertencer ao mesmo jogador lógico.
- **Reconciliação de aliases:** quando duas pastas representam o mesmo jogador e jogo, conteúdos divergentes exigem escolha manual da versão completa. Não há mesclagem silenciosa de arquivos divergentes.
- Backups de segurança em operações compatíveis.
- Interface com idiomas português brasileiro, inglês e espanhol.

> **Atenção:** uma sincronização pode sobrescrever arquivos no destino. Confira a prévia, mantenha backups independentes e não use apenas a data de modificação para decidir qual save é o correto.

## Compilação a partir do código-fonte

Requisitos: **Windows** e **SDK .NET 10**. No PowerShell, a partir da pasta do projeto:

```powershell
dotnet restore .\SwitchSaveAssistant.slnx
dotnet build .\SwitchSaveAssistant.slnx --no-restore
dotnet test .\SwitchSaveAssistant.Tests\SwitchSaveAssistant.Tests.csproj --no-restore
dotnet run --project .\SwitchSaveAssistant.App\SwitchSaveAssistant.App.csproj
```

Build Release validado no Windows: **102 testes aprovados, zero avisos de compilação e zero erros**. Ainda são necessários testes manuais de integração com fontes reais.

## Organização do código

| Projeto | Finalidade |
| --- | --- |
| `SwitchSaveAssistant.App` | Interface WPF e idiomas |
| `SwitchSaveAssistant.Core` | Regras de negócio e integrações |
| `SwitchSaveAssistant.Probe` | Ferramenta de diagnóstico para desenvolvimento |
| `SwitchSaveAssistant.Tests` | Testes automatizados |

Veja também a [arquitetura](docs/ARCHITECTURE.md).

## Privacidade e dados locais

Configurações e vínculos de perfil usam arquivos como `settings.json` e `profiles.json` em `%LOCALAPPDATA%\SwitchSaveAssistant`. Backups de segurança podem ser produzidos em operações específicas. A pasta de saves e a instalação do emulador são selecionadas ou descobertas separadamente.

**Não publique** saves reais, perfis do console/emulador, identificadores pessoais, logs sensíveis, caminhos privados ou backups em issues e pull requests. Para testes públicos, use dados sintéticos.

## Próximas etapas

O instalador e o pacote portátil Windows x64 são publicados nas Releases do GitHub com hashes SHA-256. O aplicativo verifica atualizações pelas Configurações. O instalador oferece atualização/reparo e desinstalação, com remoção opcional de perfis e configurações salvas. A compatibilidade com outros emuladores permanece adiada.

## Licença

Switch Save Assistant é distribuído sob a [Licença MIT](LICENSE). Os componentes de terceiros seguem suas respectivas licenças e avisos. Consulte os [avisos de terceiros](THIRD-PARTY-NOTICES.md) e o [inventário de dependências](docs/DEPENDENCIES.md).
