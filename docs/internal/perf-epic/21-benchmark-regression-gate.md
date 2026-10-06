# #21 — Benchmark target and regression gate

## Change

Added the `Benchmark` Nuke target. It publishes the current source in Release configuration, creates temporary
synthetic blogs with 1,000, 2,000, or 4,000 posts and three tags per post, then builds each site with and without
a recent-posts sidebar. The sidebar reads the date-sorted page view and renders the ten newest posts.

Each of the six scenarios runs three times. The median is compared with the checked-in
`.nuke/benchmark-baseline.json`; the target fails when a scenario is more than 25% slower. Benchmark builds use
two logical processors for consistent parallelism. Raw runs, medians, baseline comparisons, and pass/fail status
are written to `artifacts/benchmark.json`. CI runs the target in its own job and uploads that JSON even when the
regression gate fails.

## Benchmark

Release benchmark on Linux x64 with .NET runtime 10.0.12. The baseline was refreshed with
`./build.sh Benchmark --update-benchmark-baseline --configuration Release --runtime-identifier linux-x64`;
then the documented default command `./build.sh Benchmark` was run against it.

| Scenario | Three runs (ms) | Median (ms) | Baseline (ms) | Regression |
|---|---:|---:|---:|---:|
| 1,000 posts, sidebar off | 347.1, 345.3, 346.9 | 346.9 | 349.0 | -0.6% |
| 1,000 posts, sidebar on | 355.4, 355.6, 354.2 | 355.4 | 353.5 | +0.6% |
| 2,000 posts, sidebar off | 394.8, 401.0, 397.1 | 397.1 | 395.7 | +0.3% |
| 2,000 posts, sidebar on | 405.5, 404.0, 402.7 | 404.0 | 403.1 | +0.2% |
| 4,000 posts, sidebar off | 506.2, 509.3, 503.7 | 506.2 | 508.4 | -0.4% |
| 4,000 posts, sidebar on | 515.0, 516.1, 516.4 | 516.1 | 517.0 | -0.2% |

All scenarios passed. The complete machine-readable results are in `artifacts/benchmark.json`.

To verify the gate, temporarily set all six baseline medians to 1 ms and ran `./build.sh Benchmark`; every
scenario was marked `FAIL` and the Nuke target exited unsuccessfully with the 25% regression error. The measured
baseline was then restored and the default benchmark passed.

## Verification

- `./build.sh Benchmark`: completed with all scenarios passing.
- Artificially slow baseline: confirmed the target fails and records per-scenario failures in the JSON report.
- Published binary built in Release configuration; benchmark fixture and output directories are temporary.
- No dependency or `.gitignore` changes.
