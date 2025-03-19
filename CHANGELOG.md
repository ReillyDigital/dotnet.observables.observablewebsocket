# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Changed

- Move async construction logic to static factory methods to eliminate `Task.Wait()` calls, which break WASM portability.
- Update codebase to target line widths of less than 100 characters.

## [0.1.1] - 2024-11-15

### Changed

- Added the `idleTimeout` parameter to the `ObservableWebSocket.ListenAsync` method.

## [0.1.0] - 2024-09-08

### Added

- Initial release.
