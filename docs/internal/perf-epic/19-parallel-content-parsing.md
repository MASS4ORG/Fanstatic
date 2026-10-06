# #19 — Parse content files in parallel

## Change

Each content directory now parses its files into an ordered result array in parallel, then links
content sources sequentially in ordinal path order. Directory traversal and index selection are
also ordered. `_index.md` files are still parsed first, so their cascade is applied before the
directory's regular files are parsed. Taxonomy generation, page creation, and page-collection
tie-breaks now use stable source paths (and output-variant fields where needed).

## Benchmark

Release benchmark on Linux x86_64 with .NET SDK 11.0.100-rc.1.26425.128. Generated a site with
4,000 dated posts plus a cascading blog index. Five alternating builds compared #18 (`61ac014`)
with the #19 working tree. Fanstatic's reported Parse phase, in milliseconds:

| Revision | Runs (ms) | Median |
|---|---:|---:|
| Before (#18) | 196, 199, 198, 200, 199 | 199 ms |
| After (#19) | 62, 61, 62, 61, 64 | 62 ms |

The median Parse phase was 68.8% lower. All 4,001 Markdown files were parsed in each build.

## Output comparison and determinism

Ran `diff -r` for all 15 buildable `.TestSites` fixtures and the docs-site working tree
(`MASS4ORG/Fanstatic-site`, including pre-existing local changes) before and after #19. The new
stable path tie-breaks intentionally settle ordering that varied between old builds:

- Two fixture trees were byte-identical; 12 differed only in sitemap URL ordering. The taxonomy
  fixture also reordered equal-weight tagged posts by source path in its archive pages.
- The docs-site homepage cards and RSS items likewise now use stable path order. Membership and
  page content were unchanged.
- Five consecutive 4,000-post builds produced byte-identical output trees.
- Five docs-site builds were byte-identical except for RSS `<lastBuildDate>` timestamps when
  builds crossed a second; after normalizing only that dynamic field, the trees matched.

Across five #18 docs-site builds, sitemap URL order still varied after normalizing RSS timestamps.
The new explicit path tie-breaks make those URLs and equal-weight page listings stable.

## Verification

- `./build.sh test`: 545 passed, 0 failed.
- `dotnet format --no-restore --include Fanstatic/Models/Site.cs Fanstatic.Test/Models/SiteTests.cs`
- Added a regression test for ordinal linking order and inherited cascade values; it also verifies
  deterministic ordering of the generated tag archive.
