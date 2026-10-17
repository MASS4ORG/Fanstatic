# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [7.3.1] - 2026-10-17

- Fixed: out-of-range crash in `RegisteredPageRequest.Check`

## [7.3.0] - 2026-10-16

- Added: shortcodes from `shortcodes/` in the site and theme #36
- Added: shortcode parameter schemas, list and map arguments, `:::` directives, and the `{% shortcode %}` template tag #36
- Added: built-in `details`, `figure`, `highlight`, `instagram`, `param`, `qr`, `vimeo`, `x` and `youtube` shortcodes
- Added: the `test` build target also emits a TRX report.
- Fixed: render `_index.md` branch bundles as list pages
- Changed: `ref` and `relref` are now built-in regular shortcodes #36

## [7.2.0] - 2026-10-06

- Added: configurable taxonomy #24 #25
- Changed: performance foundation #13 #14 #15 #16 #17 #18 #19 #20 #21

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

[Unreleased]: https://github.com/MASS4ORG/Fanstatic/compare/v7.3.1...HEAD
[7.3.1]: https://github.com/MASS4ORG/Fanstatic/compare/v7.3.0...v7.3.1
[7.3.0]: https://github.com/MASS4ORG/Fanstatic/compare/v7.2.0...v7.3.0
[7.2.0]: https://github.com/MASS4ORG/Fanstatic/compare/v7.1.1...v7.2.0
[7.1.1]: https://github.com/MASS4ORG/Fanstatic/compare/v7.1.0...v7.1.1
[7.1.0]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.2...v7.1.0
[7.0.2]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.1...v7.0.2
[7.0.1]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.0...v7.0.1
[7.0.0]: https://github.com/MASS4ORG/Fanstatic/compare/v6.9.0...v7.0.0
[1.0.0]: https://github.com/MASS4ORG/Fanstatic/tree/v7.0.0
