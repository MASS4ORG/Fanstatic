# Build automation

NUKE targets shared by local development and CI: restore, compilation, tests, packaging (Windows installer,
Debian package, Winget manifests) and release publishing.

From the repository root, run `./build.sh compile` to restore and build, `./build.sh Pack` to produce the release
archives in `artifacts/`, or `./build.sh --help` to list every target.