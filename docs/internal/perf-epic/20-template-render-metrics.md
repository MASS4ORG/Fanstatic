# #20 — Per-template render metrics

## Change

Added the build-only `--template-metrics` option. When enabled, template renders are aggregated
concurrently by normalized file path, with call count, summed render time, maximum render time,
and compiled-template cache hits. Inline renders are grouped under `(inline template)`. The table
is sorted by total render time and printed after the usual build report.

When disabled, rendering follows the existing code path without timestamps, counters, or metrics
allocations. The option defaults to off and is not exposed by `serve`.

## Benchmark

Release builds on Linux x86_64 with .NET SDK 11.0.100-rc.1.26425.128, building the docs site
(84 parsed Markdown files, 183 generated pages). `/usr/bin/time` measured five fresh CLI
invocations per case; process startup and output I/O are included:

| Build | Runs (seconds) | Median |
|---|---:|---:|
| Before #20 (`12554d4`) | 0.38, 0.37, 0.37, 0.37, 0.38 | 0.37 s |
| After #20, metrics off | 0.37, 0.38, 0.38, 0.37, 0.37 | 0.37 s |
| After #20, metrics on | 0.37, 0.38, 0.38, 0.38, 0.37 | 0.38 s |

The timer reports 10 ms precision. The before and after medians with metrics disabled are equal,
so this workload shows no measurable disabled-mode overhead. The enabled median is within one
timer increment of the disabled result.

The enabled docs-site report listed ten file-template paths and the inline-template aggregate;
for example, `_default/baseof.html` had 130 calls and 130 cache hits, while inline templates had
242 calls and 200 cache hits. Summed render durations can exceed build wall time because page
rendering is parallel.

## Output comparison

Compared the pre-#20 build output, post-#20 default build output, and post-#20 metrics-enabled
output with `diff -r`. All differences were RSS `<lastBuildDate>` timestamps; page content,
membership, and ordering were unchanged.

## Verification

- `./build.sh test`: 549 passed, 0 failed.
- `./build.sh compile`: succeeded with zero warnings.
- `dotnet format --no-restore --include` on the touched C# files.
- Ran `build --template-metrics` against the docs site and verified that the metrics table follows
  the standard build report and is sorted by total render time.
