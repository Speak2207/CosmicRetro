# Cosmic Retro — status do projeto

**Atualizado em:** 24/11/2026

**Branch de trabalho:** `cosmicretro-v0.1`

**Estado:** implementação local em desenvolvimento; não é uma release certificada.

## Escopo e arquitetura

Aplicativo desktop para Windows, escrito em C#/.NET 10 com Avalonia UI e MVVM. A aplicação está separada em:

- `cosmicretro/src/CosmicRetro.Core`: modelos, catálogo de plataformas, interpretação de nomes, varredura de bibliotecas, contratos de persistência e integração.
- `cosmicretro/src/CosmicRetro.Infrastructure`: SQLite, integração com processos, configuração/instalação do RetroArch e provider de metadados.
- `cosmicretro/src/CosmicRetro.App`: interface Avalonia, view model e composição do aplicativo.
- `cosmicretro/tests/CosmicRetro.Tests`: testes automatizados de scanner, persistência e argumentos.
- `.github/workflows/cosmicretro-v0.1.yml`: CI para Windows, testes, publicação `win-x64` e pacote ZIP de artefato.

O SQLite, o log e o cache de capas ficam em `%LOCALAPPDATA%\CosmicRetro`. As dependências NuGet estão versionadas nos projetos e lockfiles. Versões e licenças declaradas estão em [cosmicretro/DEPENDENCIES.md](cosmicretro/DEPENDENCIES.md).

## Funcionalidades implementadas

### Interface e biblioteca

- Interface escura em português brasileiro, com páginas Início, Biblioteca, Favoritos, Histórico e Configurações.
- Pesquisa por título, filtro de sistema, seleção de jogo, painel com informações, estatísticas da biblioteca e jogos recentes.
- Favoritos e capas escolhidas de arquivos locais.
- Sistemas catalogados: NES, SNES, Mega Drive, Game Boy, Game Boy Advance, PlayStation, fliperamas e Neo Geo. A presença no catálogo permite indexar formatos; não certifica a execução em um emulador.
- Reindexação de fontes de ROM cadastradas ao iniciar. Fontes podem ser removidas sem excluir da base os jogos já indexados.

### Importação e metadados

- Importação de uma pasta atribuída a um sistema ou de uma raiz que contenha pastas com nomes de sistemas reconhecidos.
- Varredura recursiva, deduplicação por caminho, detecção conservadora de sistema para arquivos soltos e identificação de formatos não reconhecidos.
- Arquivos compactados permanecem no local original e não são extraídos. ZIPs inválidos, vazios ou incompatíveis geram avisos; o arquivo original não é apagado.
- Interpretação de nome, região e revisão em nomes comuns de ROMs.
- Detecção de `neogeo.zip` como firmware, não como jogo; a BIOS não é instalada nem distribuída.
- Busca opcional de descrição/capa pelo TheGamesDB. O usuário fornece a chave em sessão; a consulta usa título e sistema e baixa a capa do CDN do serviço. Capas locais podem ser associadas manualmente.

### Emuladores

- Perfil manual de caminho do RetroArch e core `.dll` por sistema; a execução usa argumentos de processo separados, sem shell.
- Instalação opcional, iniciada pelo usuário, do RetroArch estável 1.22.2 para Windows x64, obtido via HTTPS da buildbot oficial Libretro.
- Tentativa de instalar cores oficiais para NES (Mesen), SNES (Snes9x), Mega Drive (Genesis Plus GX), fliperamas e Neo Geo (FBNeo). Resultados e falhas individuais são apresentados.
- Extração de arquivos com limites de tamanho/quantidade e validação de caminhos; instalações existentes são preservadas.
- Nenhum executável, ROM ou BIOS do RetroBat é incorporado ao aplicativo.

## Testes e verificações

Na validação local mais recente desta etapa:

- `dotnet restore cosmicretro/CosmicRetro.slnx --locked-mode`: concluído.
- `dotnet test cosmicretro/CosmicRetro.slnx --configuration Release --no-restore`: **10 aprovados, 0 falhas**.
- `dotnet build cosmicretro/src/CosmicRetro.App/CosmicRetro.App.csproj --configuration Release --no-restore`: concluído sem warnings ou erros.
- `dotnet format cosmicretro/CosmicRetro.slnx --verify-no-changes --no-restore`: concluído sem diferenças.
- `dotnet list cosmicretro/CosmicRetro.slnx package --vulnerable --include-transitive`: nenhuma dependência vulnerável reportada pelas fontes NuGet consultadas.

Os testes cobrem importação por sistema e raiz, tratamento de ZIPs e formatos desconhecidos, persistência SQLite e migração de colunas, favoritos/capas/metadados/histórico, fontes de ROM e construção dos argumentos do RetroArch. Não substituem testes de integração no Windows com emuladores reais.

## Limitações conhecidas

- A execução do RetroArch, a instalação de RetroArch/cores e o comportamento gráfico ainda não foram validados de ponta a ponta numa máquina Windows 10/11.
- Game Boy, Game Boy Advance e PlayStation têm extensões e perfis configuráveis, mas seus cores não fazem parte do instalador automático atual.
- A configuração automática baixa componentes oficiais quando solicitada; não instala BIOS, firmware, ROMs ou conteúdo comercial.
- A consulta TheGamesDB depende de chave e rede; não há testes automatizados contra o serviço remoto.
- A remoção de uma fonte interrompe a reindexação dela, mas não apaga da biblioteca entradas antigas, mesmo que os arquivos já não existam.
- A interface não possui ainda navegação por controle nem instalador MSI assinado. O workflow gera ZIP self-contained, não um instalador tradicional.
- O ZIP `CosmicRetro-V0.1-Windows-x64.zip` que já existe na raiz foi gerado antes das funcionalidades recentes de configuração automática/importação e, portanto, não deve ser considerado artefato desta revisão. Ele fica fora do Git.
- O projeto original RetroBat tem condições de licença próprias. Licenças de RetroArch, cores e demais componentes devem ser verificadas individualmente antes de qualquer redistribuição.

## Próximas etapas

1. Executar a compilação, a suíte e o fluxo de instalação em Windows 10 e 11 x64.
2. Testar com instalações oficiais de RetroArch/cores e arquivos de jogos que o usuário tenha direito de usar; confirmar início/encerramento e histórico por sistema antes de anunciar suporte.
3. Adicionar testes isolados do serviço de instalação, especialmente cancelamento, downloads incompletos, ZIP/7z inválidos, redirecionamentos e falhas por core.
4. Adicionar testes do provider TheGamesDB usando respostas simuladas, sem depender de chamadas reais.
5. Definir política para arquivos de ROM removidos, fontes inacessíveis, atualizações/migrações da base e recuperação de sessões interrompidas.
6. Completar inventário de licenças/SBOM dos artefatos e avisos necessários para os componentes externos instalados.
7. Só depois de validação, produzir um novo ZIP Windows correspondente ao código e avaliar instalador assinado e suporte ampliado a controles.

## Regras de dados e distribuição

Não versionar nem distribuir ROMs, BIOS/firmware, chaves de API, dados pessoais ou arquivos de build. A aplicação deve indexar e iniciar arquivos fornecidos pelo usuário, sem copiar ou enviar ROMs. Configurações e segredos de usuário não devem ser incluídos em commits nem em logs.
