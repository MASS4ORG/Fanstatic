# Build automation

NUKE targets shared by local development and CI: restore, compilation, tests, packaging (portable archives,
Windows installer, Winget manifests, Debian package, Flatpak bundle, container image) and release publishing.

From the repository root, run `./build.sh compile` to restore and build, `./build.sh Pack` to produce the release
archives in `artifacts/`, or `./build.sh --help` to list every target.

## Performance benchmark

Run `./build.sh Benchmark` on the benchmark runner to build the synthetic 1,000-, 2,000-, and 4,000-post sites
with and without the recent-posts sidebar. Each scenario is built three times; the median is compared with
`.nuke/benchmark-baseline.json` and fails the target if it is more than 25% slower. Benchmark builds run with
two logical processors for repeatable parallelism across runners. The full run data is written to
`artifacts/benchmark.json`.

To intentionally refresh the checked-in baseline, run `./build.sh Benchmark --update-benchmark-baseline` on the
same Ubuntu runner and architecture used by CI, inspect the measured medians, and commit the baseline update with
the related performance change. Do not refresh the baseline merely to make a regression pass.

Every format is documented under [`packaging/`](packaging), including how to submit it to Winget or Flathub.

## Released runtime identifiers

`linux-x64`, `linux-arm64`, `linux-musl-x64`, `win-x64` and `osx-x64`. On GitHub, `publish-on-tag.yml` builds each
of them on the matching runner; on GitLab, the macOS archive comes from the optional `build-macos` job because the
shared runners are Linux-only.