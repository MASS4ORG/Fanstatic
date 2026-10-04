# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

- Added: `--continue-on-error` option to the `build` command
- Added: template errors report the file, line, column, and offending tag, and `build` fails on template errors
- Fixed: subfolders of `static/` are copied recursively instead of being flattened into the output root
- Fixed: build report "Parse" counts the Markdown files read and "Generate Pages" counts the pages created

## [7.1.0] - 2026-10-03

- Added: Winget and Windows Installer

## [7.0.2] - 2026-09-19

## [7.0.1] - 2026-09-18

## [7.0.0] - 2026-09-18

- First Commit!

[Unreleased]: https://github.com/MASS4ORG/Fanstatic/compare/v7.1.0...HEAD
[7.1.0]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.2...v7.1.0
[7.0.2]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.1...v7.0.2
[7.0.1]: https://github.com/MASS4ORG/Fanstatic/compare/v7.0.0...v7.0.1
[7.0.0]: https://github.com/MASS4ORG/Fanstatic/compare/v6.9.0...v7.0.0
[1.0.0]: https://github.com/MASS4ORG/Fanstatic/tree/v7.0.0
