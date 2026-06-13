# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-05-21

First release of BovineLabs Timeline Distance.

### Added
- `DistanceToStatTrack` and `DistanceToStatClip`: measure the world distance between two resolved endpoints (`from`, `to`) and apply it to a `statTarget` stat as a while-active `Added` modifier, with `Continuous`, `Interval`, and `OnStart` update modes.
- `multiplier` (default 100) scales the metre distance into the stat's x100 fixed-point space, mapping 1 metre to 1 stat unit.
- Link-source coupling: when a `*Link` schema is set, the matching `Target` enum also selects whose link map is read to resolve the endpoint.
