import argparse
import json
import re
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path, PurePosixPath
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_LOCKFILE = ROOT / "src/CosmicRetro.App/packages.lock.json"
DEFAULT_PACKAGES = Path.home() / ".nuget/packages"


def safe_filename(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]+", "_", value)


def package_license(package_file: ZipFile) -> tuple[str, set[str]]:
    nuspec_path = next(name for name in package_file.namelist() if name.endswith(".nuspec"))
    nuspec = ET.fromstring(package_file.read(nuspec_path))
    metadata = next(node for node in nuspec if node.tag.endswith("metadata"))
    license_node = next((node for node in metadata if node.tag.endswith("license")), None)
    license_url_node = next((node for node in metadata if node.tag.endswith("licenseUrl")), None)

    license_text = "License metadata not declared"
    declared_files: set[str] = set()
    if license_node is not None:
        license_text = license_node.text or "License metadata not declared"
        if license_node.attrib.get("type") == "file":
            declared_files.add(license_text)
    elif license_url_node is not None:
        license_text = license_url_node.text or license_text

    archive_names = package_file.namelist()
    for name in archive_names:
        basename = PurePosixPath(name).name.lower()
        if basename.startswith(("license", "copying", "notice")):
            declared_files.add(name)

    return license_text, declared_files


def copy_license_files(package_file: ZipFile, names: set[str], destination: Path, prefix: str) -> list[str]:
    copied: list[str] = []
    for name in sorted(names):
        relative = PurePosixPath(name)
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError(f"Caminho de licença inseguro no pacote: {name}")
        if name not in package_file.namelist():
            raise FileNotFoundError(f"Arquivo de licença declarado não encontrado no pacote: {name}")

        filename = safe_filename(f"{prefix}-{relative.name}")
        output_path = destination / filename
        output_path.write_bytes(package_file.read(name))
        copied.append(filename)
    return copied


def main() -> None:
    parser = argparse.ArgumentParser(description="Exporta avisos de licença dos pacotes NuGet da aplicação.")
    parser.add_argument("--lockfile", type=Path, default=DEFAULT_LOCKFILE)
    parser.add_argument("--packages-directory", type=Path, default=DEFAULT_PACKAGES)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    lock = json.loads(args.lockfile.read_text(encoding="utf-8"))
    packages: dict[str, str] = {}
    for dependencies in lock["dependencies"].values():
        for package_id, metadata in dependencies.items():
            if metadata.get("type") == "Project":
                continue
            resolved = metadata.get("resolved")
            if resolved is None:
                raise ValueError(f"Versão resolvida ausente para dependência: {package_id}")
            previous_version = packages.get(package_id)
            if previous_version is not None and previous_version != resolved:
                raise ValueError(f"Mais de uma versão de {package_id} está no lockfile.")
            packages[package_id] = resolved

    args.output.mkdir(parents=True, exist_ok=True)
    licenses_directory = args.output / "licenses"
    licenses_directory.mkdir(exist_ok=True)
    notice_lines = [
        "# Avisos de terceiros",
        "",
        "Licenças declaradas pelos pacotes NuGet restaurados para o Cosmic Retro. Os avisos disponíveis nos pacotes foram copiados para `licenses/`.",
        "",
    ]

    for package_id, version in sorted(packages.items(), key=lambda item: item[0].casefold()):
        package_directory = args.packages_directory / package_id.lower() / version.lower()
        package_path = next(package_directory.glob("*.nupkg"), None)
        if package_path is None:
            raise FileNotFoundError(f"Pacote NuGet não encontrado no cache: {package_id} {version}")

        prefix = safe_filename(f"{package_id}-{version}")
        with ZipFile(package_path) as package_file:
            license_text, license_files = package_license(package_file)
            copied_files = copy_license_files(package_file, license_files, licenses_directory, prefix)

        notice_lines.extend(
            [
                f"## {package_id} {version}",
                "",
                f"- Licença declarada: `{license_text}`",
                f"- Página do pacote: https://www.nuget.org/packages/{package_id}/{version}",
            ]
        )
        if copied_files:
            notice_lines.append(f"- Arquivos de licença/aviso: {', '.join(f'`licenses/{name}`' for name in copied_files)}")
        notice_lines.append("")

    (args.output / "THIRD-PARTY-NOTICES.md").write_text("\n".join(notice_lines), encoding="utf-8")
    shutil.copy2(ROOT / "DEPENDENCIES.md", args.output / "DEPENDENCIES.md")


if __name__ == "__main__":
    main()
