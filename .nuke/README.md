# Build automation

NUKE targets shared by local development and CI: restore, compilation, tests, packaging (portable archives,
Windows installer, Winget manifests, Debian package, Flatpak bundle, container image) and release publishing.

From the repository root, run `./build.sh compile` to restore and build, `./build.sh Pack` to produce the release
archives in `artifacts/`, or `./build.sh --help` to list every target.

Every format is documented under [`packaging/`](packaging), including how to submit it to Winget or Flathub.

## Released runtime identifiers

`linux-x64`, `linux-arm64`, `linux-musl-x64`, `win-x64` and `osx-x64`. On GitHub, `publish-on-tag.yml` builds each
of them on the matching runner; on GitLab, the macOS archive comes from the optional `build-macos` job because the
shared runners are Linux-only.