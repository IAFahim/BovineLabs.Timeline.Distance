# About BovineLabs Timeline Distance

Use the BovineLabs Timeline Distance package to drive a stat from the distance between two entities over the course of a timeline clip. The package adds the `DistanceToStatTrack` and `DistanceToStatClip`, which measure the world distance between two resolved endpoints and apply it to a stat as a while-active modifier.

# Using BovineLabs Timeline Distance

Add a `DistanceToStatTrack` to a DOTS timeline and place a `DistanceToStatClip` on it.

The clip resolves three roles:

- `from` / `fromLink`: endpoint A of the distance.
- `to` / `toLink`: endpoint B of the distance.
- `statTarget` / `statTargetLink` + `stat`: the entity whose stat receives the modifier and which stat.

While the clip is active it measures the world distance between the two endpoints and writes it to the stat owner as a `StatModifyType.Added` modifier sourced by the clip. The modifier is added on the active edge and removed on the inactive edge, so the contribution lasts exactly as long as the clip. The removal is applied against the entity the modifier was actually added to, so retargeting a link mid-clip does not leak a modifier on the previous owner.

## Update mode

The `mode` field controls how often the modifier is refreshed:

- `Continuous`: every frame.
- `Interval`: every `interval` seconds.
- `OnStart`: once, when the clip becomes active.

## The x100 multiplier rule

The measured metre distance is multiplied by `multiplier`, rounded to an integer, and stored as the `Added` modifier value. Stats interpret an `Added` value as `value / 100` (x100 fixed point). The default `multiplier = 100` therefore maps 1 metre to 1 stat unit (1.5m becomes the integer 150, which the stat reads as 1.5). A `multiplier` of 1 would make a 5m distance read as 0.05.

## Link-source coupling

When a `*Link` schema (`fromLink`, `toLink`, `statTargetLink`) is assigned, the matching `Target` enum (`from`, `to`, `statTarget`) does double duty: besides selecting the role, it also selects whose link map is read when resolving the linked entity. With no link schema set, the `Target` enum resolves directly against the bound entity's `Targets`.

# Technical details

## Requirements

This package depends on the BovineLabs Core, Reaction, Essence, and Timeline (including EntityLinks) packages and a project using Unity DOTS / Entities.
