# #18 — Key the compiled-template cache by path

## Change

Compiled file templates are now cached by normalized full path, so rendering a file template
does not hash its full body on every call. Inline templates remain cached by body. `PreCompileTheme`
populates the path cache; `Initialize` and `PreCompileTheme` both clear compiled-template and body
caches, so a serve rebuild recompiles changed templates.

## Benchmark

Release build benchmark on Linux x86_64 with .NET SDK 11.0.100-rc.1.26425.128. Generated a site
with 1,000 dated posts using one shared 512 KiB file template (the bulk of which is a Liquid
comment). Compared #17 (`f038ce3`) with the #18 working tree using five alternating runs.
Fanstatic's reported Create phase, in milliseconds:

| Revision | Runs (ms) | Median |
|---|---:|---:|
| Before (#17) | 88, 81, 86, 86, 84 | 86 ms |
| After (#18) | 81, 80, 81, 78, 79 | 80 ms |

This intentionally stresses repeated full-body hashing; the median Create phase was 7.0% lower.
The benchmark is synthetic and its large template body is not representative of typical themes.

## Output comparison

Built all 15 buildable `.TestSites` fixtures and the docs-site working-tree snapshot
(`MASS4ORG/Fanstatic-site`, including pre-existing local changes) with #17 and #18 into separate
output directories, then ran `diff -r`.

- Three fixture trees were byte-identical; 12 differed only in sitemap URL ordering.
- The docs-site output differed only in sitemap URL ordering.
- After sorting sitemap `<url>` blocks for comparison, all 15 fixture trees and the docs-site
  output were identical. No other generated output differences remained.

## Verification

- `./build.sh test`: 544 passed, 0 failed.
- `dotnet format --no-restore --include Fanstatic/TemplateEngine/FluidTemplateEngine.cs Fanstatic.Test/TemplateEngine/FluidTemplateEngineTests.cs`
- Added tests for path-cache reuse, body-keyed inline templates, and recompilation after
  `Initialize` observes a changed file.
