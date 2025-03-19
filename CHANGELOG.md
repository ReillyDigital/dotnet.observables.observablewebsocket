# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

## [1.0.0-alpha.1] - 2025-03-18

### Changed

- Move async construction logic to static factory methods to eliminate `Task.Wait()` calls, which break WASM portability.
- Update codebase to target line widths of less than 100 characters.
- Preparing for 1.0.0 release.

## [0.1.1] - 2024-11-15

### Changed

- Added the `idleTimeout` parameter to the `ObservableWebSocket.ListenAsync` method.

## [0.1.0] - 2024-09-08

### Added

- Initial release.
