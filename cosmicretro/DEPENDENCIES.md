# Dependências e licenças

Inventário das dependências diretas fixadas em `*.csproj`. As declarações SPDX abaixo foram conferidas nos metadados `.nuspec` dos pacotes restaurados.

| Pacote | Versão | Licença declarada |
|---|---:|---|
| Avalonia | 12.2.0 | MIT |
| Avalonia.Desktop | 12.2.0 | MIT |
| Avalonia.Themes.Fluent | 12.2.0 | MIT |
| CommunityToolkit.Mvvm | 8.4.2 | MIT |
| Microsoft.Data.Sqlite | 10.0.14 | MIT |
| SharpCompress | 0.50.2 | MIT |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| xunit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |

O restore também inclui dependências transitivas. Antes de publicar releases, gere e arquive o inventário transitivo da build e confirme os avisos/licenças de cada pacote, inclusive o bundle SQLite nativo. O conteúdo de licenças upstream não é substituído por este documento.

Nenhum binário, tema ou executável do RetroBat é incorporado ao build do Cosmic Retro. Nenhuma ROM ou BIOS é distribuída.

## Dependências transitivas restauradas

As versões abaixo foram obtidas por `dotnet list package --include-transitive`; as licenças foram verificadas nos metadados `.nuspec` ou no arquivo de licença do pacote. A dependência ANGLE declara um arquivo `LICENSE` incluído no `.nupkg`, cujo texto é BSD-3-Clause.

- **MIT:** Avalonia.BuildServices 12.0.0; Avalonia.FreeDesktop, Avalonia.FreeDesktop.AtSpi, Avalonia.HarfBuzz, Avalonia.Native, Avalonia.Remote.Protocol, Avalonia.Skia, Avalonia.Win32 e Avalonia.X11 12.2.0; HarfBuzzSharp e os pacotes NativeAssets Linux, macOS, WebAssembly e Win32 8.3.1.3; MicroCom.Runtime 0.11.7; SharpCompress 0.50.2; SkiaSharp e NativeAssets Linux, macOS, WebAssembly e Win32 3.119.4; Microsoft.CodeCoverage, Microsoft.TestPlatform.ObjectModel e Microsoft.TestPlatform.TestHost 17.14.1; Microsoft.Data.Sqlite.Core 10.0.14; Newtonsoft.Json 13.0.3.
- **Apache-2.0:** SQLitePCLRaw.bundle_e_sqlite3, core, lib.e_sqlite3 e provider.e_sqlite3 2.1.13; xunit.analyzers 1.18.0; xunit.assert, core, extensibility.core e extensibility.execution 2.9.3. xunit.abstractions 2.0.3 aponta para licença Apache-2.0 e contém referências a componentes importados sob MIT.
- **BSD-3-Clause:** Avalonia.Angle.Windows.Natives 2.1.27548.20260419.

Os pacotes de testes são dependências de desenvolvimento e não devem ser incluídos no artefato de publicação. Os pacotes nativos podem distribuir bibliotecas upstream com avisos próprios; uma release precisa preservar os avisos/licenças contidos nos pacotes e revisar o SBOM do artefato final.

RetroArch e os cores não são incorporados ao aplicativo. A configuração opcional baixa o RetroArch e os cores Mesen, Snes9x, FBNeo e Genesis Plus GX da buildbot oficial Libretro, por ação do usuário. Cada componente mantém seus próprios créditos e licença upstream; consulte as informações de licença publicadas pelos respectivos projetos antes de redistribuir qualquer um deles.
