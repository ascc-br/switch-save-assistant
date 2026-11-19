# Dependências NuGet

Inventário coletado em 18 de novembro de 2026 na etapa 13.7B.

A solução contém 15 pares de pacote e versão, em 18 referências por projeto.

| Pacote | Versão | Licença declarada | Projetos consumidores |
| --- | --- | --- | --- |
| coverlet.collector | 6.0.4 | MIT | Tests |
| MediaDevices | 2.0.0 | MIT | App, Core, Probe, Tests |
| Microsoft.CodeCoverage | 17.14.1 | MIT | Tests |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Tests |
| Microsoft.TestPlatform.ObjectModel | 17.14.1 | MIT | Tests |
| Microsoft.TestPlatform.TestHost | 17.14.1 | MIT | Tests |
| Newtonsoft.Json | 13.0.3 | MIT | Tests |
| xunit | 2.9.3 | Apache-2.0 | Tests |
| xunit.abstractions | 2.0.3 | URL legada. Projeto declara Apache-2.0¹ | Tests |
| xunit.analyzers | 1.18.0 | Apache-2.0 | Tests |
| xunit.assert | 2.9.3 | Apache-2.0 | Tests |
| xunit.core | 2.9.3 | Apache-2.0 | Tests |
| xunit.extensibility.core | 2.9.3 | Apache-2.0 | Tests |
| xunit.extensibility.execution | 2.9.3 | Apache-2.0 | Tests |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | Tests |

¹ O pacote xunit.abstractions 2.0.3 registra um endereço legado no campo licenseUrl. A identificação Apache-2.0 é corroborada pela declaração de licença do projeto oficial de abstrações. O valor original coletado foi preservado no CSV da auditoria.

Fonte oficial da licença de xunit.abstractions disponível em [license.txt](https://github.com/xunit/abstractions.xunit/blob/main/license.txt).

## Aplicação desktop

MediaDevices 2.0.0 é a dependência NuGet presente no grafo da App. Ela é referenciada diretamente pelo Core e pelo Probe e chega à App e aos testes por referência transitiva.

Os demais 14 pacotes foram encontrados no projeto Tests. O relatório registra o grafo de restauração. A composição final do instalador deve ser conferida na saída de publicação.

## Distribuição autocontida

Uma publicação autocontida também contém os componentes de runtime do .NET e do Windows Desktop. Os textos LICENSE e THIRD-PARTY-NOTICES fornecidos pelos respectivos pacotes de runtime devem acompanhar a distribuição aplicável. Essa conferência integra a etapa de empacotamento.

Os avisos de MediaDevices estão em [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## Atualização do inventário

Atualize este inventário e os avisos quando uma referência ou uma versão de dependência mudar. O cache NuGet mantém os manifests .nuspec dos pacotes restaurados, com suas declarações de licença.

Consulta por pacote e versão disponível na [galeria NuGet](https://www.nuget.org/packages).
