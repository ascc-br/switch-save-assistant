# 13.6C — checklist de regressão manual (antes do primeiro release)

Este checklist não foi executado automaticamente. Registre data, resultado e evidências **sem divulgar saves reais**.

## Estado básico

- [ ] Abre com Switch desconectado e apresenta estado compreensível.
- [ ] Abre com Eden fechado e configurações válidas.
- [ ] Trata pasta Folder inexistente/sem permissão sem corromper o estado.
- [ ] Trata instalação Eden não encontrada e saves ausentes.
- [ ] Persiste e recupera idioma, tema e filtros após reiniciar.
- [ ] Não faz operações de escrita só por navegar/visualizar comparações.

## Comparações

- [ ] Switch ↔ Folder: listar, comparar e validar escolha de direção.
- [ ] Eden ↔ Folder: listar, comparar e validar escolha de direção.
- [ ] Save presente só na origem ou só no destino é identificado corretamente.
- [ ] Saves divergentes exigem confirmação explícita e prévia adequada.
- [ ] Device/shared não é associado automaticamente a um jogador normal.
- [ ] Dados em um perfil Eden não aparecem como pertencentes a outro perfil.

## Perfis lógicos

- [ ] Criar, selecionar e excluir perfil lógico (somente cenário descartável).
- [ ] Vincular/desvincular perfil Eden por ID estável (cenário descartável).
- [ ] Vincular/desvincular identidade Switch por nome (cenário descartável).
- [x] Eden ↔ Folder oferece resolução direta de colisão de aliases (validado 18/11/2026).
- [x] Vincular Eden após colisão faz o reparador abrir automaticamente (validado 18/11/2026).
- [ ] Conteúdos idênticos consolidam sem perda, em teste sintético.
- [ ] Conteúdos divergentes exigem escolha manual da versão completa.
- [ ] Cancelar conflito preserva dados e mantém sincronização perigosa bloqueada.

## Backups, integridade, falhas

- [ ] Backup antes de sobrescrita validado em diretório temporário.
- [ ] Restauração validada em cenário temporário com SHA-256.
- [ ] Interrupção/cancelamento deixa estado consistente ou restaura backup.
- [ ] Falhas de IO, espaço/permissão e arquivos bloqueados são tratadas.
- [ ] Nenhum cenário manual usa dados de produção sem cópia independente.

## Empacotamento e publicação

- [ ] Rebuild Release no Windows x64.
- [ ] Teste de portable em máquina sem SDK .NET.
- [ ] Teste de instalação e desinstalação sem terminal.
- [ ] Verificação de licença, dependências e avisos de terceiros.
- [ ] Revisão final do conteúdo a ser commitado e das configurações locais.
- [ ] CI reproduz build e testes em runner Windows.
- [ ] Release notes, SHA-256 e teste de download.

**Resultado atual:** build com zero avisos/erros e 71/71 testes; o restante deste checklist exige evidência específica antes de ser marcado como concluído.
