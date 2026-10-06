# #17 — Cache sorted page views

## Decision

Chose explicit cached properties (approach B). Fluid materializes enumerables as `ArrayValue`s,
so a sort-filter cache keyed by collection identity is not reliable from Liquid. Added stable,
thread-safe cached views ordered ascending by date, last modification, weight, and title for page
and output collections. Updated the docs theme's single-key sorts; `reverse` remains a template
filter, and its composite two-key sidebar sort is unchanged.

## Benchmark

Release build benchmark on Linux x86_64 with .NET SDK 11.0.100-rc.1.26425.128. Generated a site
with 1,000 dated posts. Each post's template sorts all regular pages and displays the 10 most
recent titles. Compared #16 (`42d99c2`) with the #17 working tree using five alternating runs.
Fanstatic's reported Create phase, in milliseconds:

| Workload | Runs (ms) | Median |
|---|---:|---:|
| Before, sorted sidebar | 233, 226, 223, 229, 232 | 229 ms |
| After, cached sorted sidebar | 92, 107, 103, 103, 105 | 103 ms |
| After, no sidebar | 69, 70, 70, 68, 72 | 70 ms |

The cached sidebar reduced median Create time by 55.0% against the previous implementation. The
post-change sidebar build took 1.47× the no-sidebar baseline (103 / 70), meeting the issue's 2×
limit.

## Output comparison

Built all 15 buildable `.TestSites` fixtures and a snapshot of the docs-site working tree
(`MASS4ORG/Fanstatic-site`, HEAD `f982d44`, including its pre-existing local changes) using #16
and #17 into separate output directories, then ran `diff -r`.

- Three fixture trees were byte-identical; 12 differed only in sitemap URL ordering.
- The docs-site output differed only in sitemap URL ordering.
- After sorting sitemap `<url>` blocks for comparison, all 15 fixture trees and the docs-site
  output were identical. No other generated output differences remained.

## Verification

- `./build.sh test`: 541 passed, 0 failed.
- `dotnet format --no-restore --include Fanstatic/Models/IPage.cs Fanstatic/Models/Page.cs Fanstatic/Models/SiteOutput.cs Fanstatic.Test/Models/SiteTests.cs`
- Sorted-view tests verify ordering and that repeated accesses return the same cached list.
