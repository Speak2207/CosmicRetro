# Cosmic Retro V0.1

Aplicativo Windows em C#/.NET 10 e Avalonia UI. A aplicação mantém uma biblioteca local em SQLite e inicia uma instalação do RetroArch escolhida pelo usuário.

## Requisitos

- Windows x64 para uso com RetroArch.
- .NET 10 SDK para compilar e executar a partir do código.
- Acesso à internet para a instalação opcional do RetroArch/cores e para metadados online.
- ROMs que o usuário tenha direito de usar.

O Cosmic Retro não distribui jogos nem BIOS. A instalação de RetroArch e cores é iniciada pelo usuário e baixa arquivos da buildbot oficial Libretro. A presença de um sistema na lista indica que o catálogo pode indexar extensões conhecidas; **não certifica a compatibilidade de execução de cada core ou jogo**. Validação real em Windows com cada core ainda é necessária antes de declarar suporte funcional.

## Compilar e testar

```powershell
dotnet restore cosmicretro/CosmicRetro.slnx
dotnet test cosmicretro/CosmicRetro.slnx -c Release
dotnet run --project cosmicretro/src/CosmicRetro.App/CosmicRetro.App.csproj
```

## Primeiro uso

1. Abra **Configurações** e use **Instalar RetroArch e cores oficiais**, ou localize uma instalação existente. O app instala RetroArch 1.22.2 e tenta obter Mesen, Snes9x, FBNeo e Genesis Plus GX da buildbot oficial; falhas individuais são informadas.
2. Na **Biblioteca**, use **Importar biblioteca por pastas de sistema** para selecionar uma pasta com subpastas `NES`, `SNES`, `NEOGEO` e `SEGA`. A varredura é recursiva dentro de cada sistema; arquivos ZIP são mantidos sem extração. Também é possível importar uma pasta de um sistema manualmente.
3. Configure uma pasta de sistema do RetroArch se necessário. O Cosmic Retro não fornece BIOS; a importação destaca se `neogeo.zip` não estiver na pasta de sistema configurada.
4. Selecione um jogo para favoritar ou executar. Para metadados online, informe sua chave TheGamesDB nas configurações e acione **Buscar capa e descrição**; somente o título e o sistema são consultados, nunca a ROM.
5. Todas as fontes registradas são reindexadas ao iniciar o aplicativo; você pode removê-las em **Configurações** sem apagar os jogos já catalogados.

O banco, logs e capas baixadas ficam em `%LOCALAPPDATA%\CosmicRetro`. As ROMs são apenas indexadas e executadas do caminho original; o aplicativo não as copia, altera ou envia. Capas/descrições do TheGamesDB dependem de chave e disponibilidade externa.

## Arquitetura

- `src/CosmicRetro.Core`: catálogo de plataformas, scanner, contratos e modelo de comando.
- `src/CosmicRetro.Infrastructure`: SQLite, persistência de histórico/configurações e inicialização de processos.
- `src/CosmicRetro.App`: interface Avalonia, MVVM e composição da aplicação.
- `tests/CosmicRetro.Tests`: testes de scanner, persistência e argumentos de execução.

Consulte [DEPENDENCIES.md](DEPENDENCIES.md) para versões e licenças declaradas das dependências diretas.

## Geração Windows x64

A workflow [cosmicretro-v0.1.yml](../.github/workflows/cosmicretro-v0.1.yml) compila e testa no Windows e publica um artefato ZIP self-contained com o executável, dependências e avisos exportados dos pacotes NuGet. O artefato não é um instalador assinado e não inclui emuladores nem conteúdo de jogos.
