# Packaging

Every package format Fanstatic ships is described here. The descriptors that belong to a format live next to
that format; generated packages land in `artifacts/`.

| Format | Descriptor | Target | Output |
| --- | --- | --- | --- |
| Portable archive | – | `Pack` | `artifacts/Fanstatic-<version>-<rid>.zip` |
| Windows installer | [`windows/`](windows) | `WindowsInstaller` | `artifacts/Fanstatic-<version>-win-x64-setup.exe` |
| Winget | [`winget/`](winget) | `WingetManifest` | `artifacts/winget/<version>/` |
| Debian | – | `DebianPackage` | `artifacts/fanstatic_<version>_<arch>.deb` |
| Flatpak | [`flatpak/`](flatpak) | `Flatpak` | `artifacts/Fanstatic-<version>-x86_64.flatpak` |
| Container | – | `ContainerBuild` / `ContainerPush` | OCI archive, then the registry |

## Runtime identifiers

`linux-x64`, `linux-arm64`, `linux-musl-x64`, `win-x64` and `osx-x64` are released, matching Turian.
Everything is published self-contained, so the target machine needs no .NET runtime and the Winget manifest
declares no dependencies.

## Adding a format

Create a `Build.Package.<Format>.cs` partial with one target that writes into `ArtifactsDirectory`, put the
descriptors in `.nuke/packaging/<format>/` with a README, and wire the target into the release pipeline when the
format is ready to be shipped by CI.