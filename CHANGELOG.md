# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

- Added: configurable taxonomies for tags, categories, and custom terms #24 #25
- Changed: logging defaults to Information; `-v` / `--verbose` enables Debug-level logs
- Fixed: paginated taxonomy archives accept compact URLs such as `/tags/release/2`
- Fixed: serving a site reuses its port when only previous connections in `TIME_WAIT` remain
- Fixed: client disconnects while serving no longer appear as unexpected listener errors
- Changed: materialize cached page collections after page processing #14
- Breaking: require `IPage` implementations to provide cached `Plain` and `WordCount` values #15
- Changed: cache rendered page content once per page and report recursive content cycles #16
- Breaking: add cached date, last-modified, weight, and title views to `IPage` #17
- Changed: key compiled file templates by normalized path #18
- Changed: parse content files in parallel and stabilize equal-weight ordering by source path #19
- Added: optional per-template render metrics with `build --template-metrics` #20
- Added: benchmark target with a 25% performance regression gate #21
- Changed: invalid front matter, duplicate permalink and unresolved `ref` errors are reported on one line with the absolute file path (clickable in IDEs), and front matter errors without a stack trace
- Fixed: `ref`/`relref` fall back to the default-language page, in the requested output format, when a translation is missing

## [7.1.1] - 2026-10-04

- Fixed: report parsed files and generated pages separately #12
- Fixed: copy static folders recursively #11
- Fixed: report template errors with location and fail the build #3

## [7.1.0] - 2026-10-03

- Added: Winget and Windows Installer

## [7.0.2] - 2026-09-19

## [7.0.1] - 2026-09-18

## [7.0.0] - 2026-09-18

- First Commit!

[Unreleased]: https://github.com/MASS4ORG/Fanstatic/compare/v7.1.1...HEAD
[7.1.1]: https://github.com/MASS4ORG/Fanstatic/compare/v7.1.0...v7.1.1
[7.1.0]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.2...v7.1.0
[7.0.2]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.1...v7.0.2
[7.0.1]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.0...v7.0.1
[7.0.0]: https://github.com/MASS4ORG/Fanstatic/compare/v6.9.0...v7.0.0
[1.0.0]: https://github.com/MASS4ORG/Fanstatic/tree/v7.0.0
