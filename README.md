# BovineLabs Timeline Distance

A DOTS timeline track that measures the distance between two entities and feeds it into a stat as a live, while-active modifier.

`DistanceToStatTrack` / `DistanceToStatClip` resolve two endpoints (`from`, `to`) and a stat owner (`statTarget`), measure the world distance between the endpoints, and write it to the owner's stat as a `StatModifyType.Added` modifier sourced by the clip. The modifier is added when the clip becomes active and removed when it ends, so the contribution exists only for the clip's lifetime.

## Update modes

- `Continuous` rewrites the modifier every frame.
- `Interval` rewrites it every `interval` seconds.
- `OnStart` writes it once when the clip becomes active.

## The x100 multiplier rule

The measured metre distance is multiplied by `multiplier`, rounded to an integer, and stored as the `Added` modifier. Stats read an `Added` value as `value / 100` (x100 fixed point), so use the default `multiplier = 100` to map 1 metre to 1 stat unit (1.5m becomes 150, read as 1.5).

## Link-source coupling

When a `*Link` schema (`fromLink`, `toLink`, `statTargetLink`) is assigned, the matching `Target` enum (`from`, `to`, `statTarget`) serves double duty: it selects both the endpoint/owner role and whose link map is read to resolve the linked entity.
