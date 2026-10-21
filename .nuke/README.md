# Build automation

NUKE targets shared by local development and CI: restore, compilation, tests, packaging (portable archives,
Windows installer, Winget manifests, Debian package, Flatpak bundle, container image) and release publishing.

From the repository root, run `./build.sh compile` to restore and build, `./build.sh Pack` to produce the release
archives in `artifacts/`, or `./build.sh --help` to list every target.

## Performance benchmark

Run `./build.sh Benchmark` to build the synthetic 1,000-, 2,000-, and 4,000-post sites with and without the recent-posts sidebar. Each scenario is built five times and the Parse and Create medians are compared with the newest comparable release in `.nuke/benchmark-history.json`; the target fails if either phase is more than 25% slower. Benchmark builds run with two logical processors. The full run data and comparison mode are written to `artifacts/benchmark.json`.

On GitHub Actions, the target publishes the reference release from its `vX.Y.Z` tag using the current SDK and remeasures it on the same runner. Each scenario alternates reference/current and current/reference builds to distribute changes in runner load across both versions. The gate uses these fresh reference medians because different hosted VMs can differ by more than the regression limit. Checkout must include release tags and their commits. Missing tags or failed reference builds fail the target. Recorded release timings remain unchanged. Local runs compare against the saved timings for the same developer machine.

The history keeps one entry per release, with the version, date, runner, SDK and runtime. An entry is comparable
when it came from the same kind of runner (CI, or the same developer machine), runtime identifier, architecture,
processor count and number of runs. A different SDK or runtime does not block the comparison and only logs a
warning, so an SDK upgrade shows up in the history instead of failing the build. With no comparable entry nothing is
gated.

On each release, next to the CHANGELOG and version update, run `./build.sh Benchmark --record-benchmark` on the CI
runner and commit the new entry. It is recorded only when the gate passes, and an existing entry for the same
version and runner is never overwritten. Entries from a developer machine are kept but only gate that machine.

Every format is documented under [`packaging/`](packaging), including how to submit it to Winget or Flathub.

## Released runtime identifiers

`linux-x64`, `linux-arm64`, `linux-musl-x64`, `win-x64` and `osx-x64`. On GitHub, `publish-on-tag.yml` builds each
of them on the matching runner; on GitLab, the macOS archive comes from the optional `build-macos` job because the
shared runners are Linux-only.
