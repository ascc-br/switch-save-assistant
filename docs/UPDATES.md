# Atualizações do aplicativo

As Configurações incluem a opção **Verificar e notificar atualização disponível**, ativada por padrão inclusive em configurações anteriores. A alteração é persistida ao clicar em **Salvar**. A consulta automática ocorre uma vez por abertura, em segundo plano. Falhas de rede não impedem o uso do aplicativo. O botão **Verificar atualizações** funciona mesmo com a consulta automática desativada.

O aviso na tela principal abre as Configurações. A consulta usa somente as Releases públicas de `ascc-br/switch-save-assistant`, sem token nem login. Um commit ou uma tag sem Release não oferece atualização. A edição estável considera apenas versões estáveis; a edição preview considera previews e estáveis, comparadas por versão semântica, inclusive `preview.10 > preview.2`.

## Aplicação de uma atualização

- **Instalada pelo Setup:** o botão Atualizar solicita confirmação, baixa e verifica o pacote, abre o instalador e encerra o aplicativo. O Setup mantém o AppId e o diretório de instalação. A instalação permanece interativa; cancelar antes de iniciar a instalação mantém a cópia atual.
- **Portable ou executada no projeto:** o botão baixa e verifica o ZIP e abre a pasta. Extraia em uma nova pasta e abra o novo executável. O diretório com arquivos em uso não é substituído.

Os dois modos preservam configurações e perfis em `%LOCALAPPDATA%\SwitchSaveAssistant`. O download é colocado em uma subpasta exclusiva de `Updates` nesse diretório. O arquivo só é disponibilizado após verificar o tamanho e o SHA-256 fornecido pela Release; usa o digest do GitHub ou, quando ausente, a entrada correspondente em `SHA256SUMS.txt`. Arquivos incompletos ou corrompidos são removidos. Fechar as Configurações cancela a operação em andamento.

## Publicação de versões

1. Atualize `Version` e `FileVersion` em `SwitchSaveAssistant.App.csproj`. A versão deste pacote é `0.1.0-preview.2`, com FileVersion `0.1.0.1`. O instalador recebe a versão numérica diretamente do projeto.
2. Execute `build/Package-Windows.ps1` no Windows. Ele exige aprovação do build Release e de toda a suíte antes de publicar o executável autocontido e compilar o Setup.
3. Confira abertura do ZIP, instalação, reinstalação sobre a versão anterior e desinstalação. Configurações e perfis devem permanecer. Registre e envie as alterações ao Git antes de publicar a Release correspondente.
4. No GitHub, crie uma Release com a tag `v0.1.0-preview.2`, marcando **pre-release** para esta versão. Anexe os três arquivos produzidos na mesma execução:

   - `SwitchSaveAssistant-Setup-0.1.0-preview.2-win-x64.exe`
   - `SwitchSaveAssistant-Portable-0.1.0-preview.2-win-x64.zip`
   - `SHA256SUMS.txt`

5. Publique a Release depois de terminar todos os uploads. Mantenha os nomes dos pacotes: o atualizador seleciona o arquivo compatível pela versão e edição. Não substitua binários de uma versão publicada; gere uma nova versão.

Enquanto não houver Release com pacote compatível e hash disponível, a verificação informa essa condição. A atualização real entre duas versões publicadas e a interação com o Setup exigem validação no Windows; os testes HTTP simulados cobrem a seleção, o download e a integridade sem publicar versões artificiais.
