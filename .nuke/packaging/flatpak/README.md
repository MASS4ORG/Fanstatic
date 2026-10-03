# Flatpak

Build a local bundle with `./build.sh Flatpak --configuration Release`; the single-file bundle lands in
`artifacts/Fanstatic-<version>-x86_64.flatpak`.

The manifest uses the .NET 10 Flatpak SDK extension and publishes the generator framework-dependent, so the
runtime comes from the SDK extension instead of the bundle. Unlike Turian, Fanstatic has no GUI: the desktop
entry launches the CLI in a terminal, and `flatpak run org.MASS4.Fanstatic <source-directory>` is the normal
entry point. Networking is granted because site builds fetch remote data; the home directory is granted because
sites are built in place.

It is intentionally not in CI until the Flathub submission policy is finalized.