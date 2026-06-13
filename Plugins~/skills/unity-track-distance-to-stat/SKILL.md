---
name: unity-track-distance-to-stat
description: Master of DistanceToStatTrack + DistanceToStatClip (package BovineLabs.Timeline.Distance) — continuous/interval distance measurement into a live-updating while-active stat modifier, the multiplier=100 rule, the mode-doubles-as-readRootFrom link quirk. Portable to any project containing the package; worked example from vex-ee.
---

# DistanceToStatTrack specialist

## 1. SCOPE

You are the specialist for **`DistanceToStatTrack`** and **`DistanceToStatClip`** from the package
`BovineLabs.Timeline.Distance`, namespace `BovineLabs.Timeline.Distance.Authoring`. Scope: exactly this track — the lone
Distance-package track and the program's first **CAPSTONE clip**: one clip touches **Targets** (three slots), **EntityLinks**
(three link overrides), and **Essence stats** (while-active modifier) at once. While the clip is active, `distance(from, to) ×
multiplier` is `(int)math.round`-ed into a flat-Added `StatModifier` on the resolved receiver, **replaced in place** every
update and removed on the deactivation edge. Duration IS the effect window. Cross-references: `unity-track-essence-stat` (the
while-active StatModifier pattern, ×100 int-Added truth, formula Σadded×(1+Σincreased)×Π(1+more));
`unity-track-entitylink-copytransform` (schema-link fundamentals: root hop via `EntityLinkSource.Root`, linear
`EntityLinkEntry` search, key 0 / missing buffer = silent fail); stage construction → `unity-stage-foundations`.

**Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`; use the editor per `unity-cli`.** The
discovery preamble (its §1), the SubScene bracket (its §2), the undo-appendix structure (its §3), and the verification protocol
(its §4) are owned there — this skill keeps ONLY the Distance-track-unique facts below.

## 2. TYPE FACTS

All verified vex-ee 2026-06 via reflection dumps, package-source reads, raw YAML, fresh-load read-backs, and one real forced
SubScene bake — all `unity-cli exec`, no play mode; runtime claims source-derived. True in ANY project containing
`BovineLabs.Timeline.Distance` (plus its Essence/Reaction/EntityLinks dependencies).

| Type | Facts |
|---|---|
| `DistanceToStatTrack` | `BovineLabs.Timeline.Distance.Authoring`, sealed, base `DOTSTrack`. `[TrackClipType(DistanceToStatClip)]`, **`[TrackBindingType(BovineLabs.Reaction.Authoring.Core.TargetsAuthoring)]`** (the bind target — the COMPONENT, not a Transform), `[TrackColor(0.20,0.90,0.70)]`, `[DisplayName("BovineLabs/Distance/Distance To Stat")]`. The lone Distance-package track. |
| `DistanceToStatClip` | sealed, base `DOTSClip`, `clipCaps => ClipCaps.Blending \| ClipCaps.Looping` (COSMETIC — see traps), `duration => 1` (seed only). |
| `DistanceUpdateMode` | `BovineLabs.Timeline.Distance.Data`, **byte-backed**: `OnStart=0, Continuous=1, Interval=2`. |
| `Target` (enum, verified) | `None=0, Target=1, Owner=2, Source=3, Self=4, Custom=6`; None/unset slot → `Entity.Null`. |
| System | `DistanceToStatSystem` — `[UpdateInGroup(TimelineComponentAnimationGroup)]`, `[UpdateAfter(typeof(EntityLinkTargetPatchSystem))]` (sees same-frame TargetPatch retargets, like the Essence triad). |

### Clip fields — camelCase YAML names (reflection + fresh-instance defaults)

| Field | Type | Default | Meaning |
|---|---|---|---|
| `from` | `Target` | **`Owner` (2)** | Distance endpoint A (TRAP — an unwired Owner slot = permanent silent skip) |
| `fromLink` | `EntityLinkSchema` | null | Link override for A |
| `to` | `Target` | `Target` (1) | Distance endpoint B |
| `toLink` | `EntityLinkSchema` | null | Link override for B |
| `statTarget` | `Target` | `Self` (4) | Who receives the stat modifier |
| `statTargetLink` | `EntityLinkSchema` | null | Link override for the receiver |
| `stat` | `StatSchemaObject` | null | Which stat. Null → **SILENT bake abort** (Essence-style) |
| `multiplier` | `float` | 1 | Applied BEFORE the int conversion (tooltip: "e.g., 100 to map 1.5m to 150") |
| `mode` | `DistanceUpdateMode` | `Continuous` (1) | OnStart / Continuous / Interval |
| `interval` | `float` | 0.5 | Used only when mode=Interval |

Bake (quoted from `DistanceToStatClip.Bake`):

```csharp
if (stat == null) return;   // SILENT abort - no LogError, skips base.Bake too
EntityLinkAuthoringUtility.TryGetKey(fromLink, out var fromKey);   // null -> key 0
...
Data = new DistanceToStatData { From = from, FromLinkKey = fromKey, To = to, ToLinkKey = toKey,
    StatTarget = statTarget, StatLinkKey = statTargetKey, StatKey = stat.Key,
    Mode = mode, Interval = interval, Multiplier = multiplier },
HasState = true
```

→ adds `DistanceToStatData` + `DistanceToStatState { float Timer }` (the interval timer lives ON the clip entity) via
`DistanceToStatBuilder.ApplyTo`.

## 3. RUNTIME SEMANTICS

`DistanceToStatSystem` runs three jobs per frame: `GatherActiveJob` (`[WithAll(ClipActive)]`, every active frame) guards
`binding.Value == Entity.Null || data.StatKey.Value == 0` and a missing `Targets` on the binding (silent returns), detects the
activation edge via `var isFirstFrame = !activePrev.ValueRO`, computes `shouldUpdate` per mode (OnStart → first frame only;
Continuous → always; Interval → first frame does `state.Timer = 0f; shouldUpdate = true;`, later frames accumulate
`state.Timer += DeltaTime` and fire on `Timer >= Interval` with `Timer -= Interval`), resolves all three slots through
`ResolveTarget` (link hunt first iff `linkKey != 0`, else/on-failure `targets.Get(mode, self)`), silently skips if ANY of
from/to/stat is `Entity.Null` or from/to lacks `LocalToWorld`, then computes `distance = math.distance(fromLtw.Position,
toLtw.Position) * data.Multiplier` and enqueues `StatModifier { Type = StatKey, ModifyType = Added, Value =
(int)math.round(distance) }` keyed by `Source = clipEntity`; `GatherRemoveJob` (the deactivation edge) re-resolves only
statTarget and enqueues an `IsRemove` mutation; the single-threaded `ApplyJob` drains the one queue in order and for EVERY
mutation first `RemoveAtSwapBack`s the at-most-one buffer entry with `SourceEntity == mutation.Source` then `buffer.Add`s the
fresh value unless IsRemove — net ONE buffer entry per clip, updated in place while active, deleted at clip end — and enables
`StatChanged` on every touched receiver.

### ResolveTarget — mode doubles as readRootFrom (quoted, family-critical)

```csharp
if (linkKey != 0 &&
    EntityLinkResolver.TryResolve(self, targets, mode, linkKey, sources, entries, out var linked))
    return linked;
return targets.Get(mode, self);
```

Inside `EntityLinkResolver.TryResolve`: `var rootCandidate = targets.Get(readRootFrom, self);` — **the clip has NO separate
`readRootFrom` field; the same `mode` enum is BOTH the link-hunt start AND the fallback slot.** A link override only wins when
the mode slot points at an entity REACHING a link root (`EntityLinkSource.Root`, or itself) whose `EntityLinkEntry` buffer
carries the key; otherwise the hunt fails silently and the slot falls back to `targets.Get(mode)` — the override LOOKS dead.
Designer rule: **link overrides only re-route a slot whose mode-entity is (or parents under) a link root** — e.g. a linked
weapon/anchor hanging off the entity the mode slot already points at.

### The multiplier=100 rule (×100 fixed-point chain, family-critical)

Essence stats are ×100 fixed-point with an integer Added sum (`StatValue { int Added; ... ValueFloat => Added * Multi / 100
}`). The Distance chain is: `float distance → × multiplier → (int)math.round → int Added → float readers divide by 100`.
**multiplier=100 makes the stat's integer units centimeters, which is exactly ×100 fixed-point — float readers get meters
back. multiplier=1 is almost always a designer mistake under ×100 encoding** (a ~5 m distance collapses to int 5 and reads
back as 0.05 — 99% of the value destroyed; concrete numbers in §6). Distance ROUNDS (`(int)math.round`) where EssenceStat
TRUNCATES (`(int)value`).

### Silence profile + traps & DO/DON'T (each proven live or source-quoted, vex-ee 2026-06)

This clip is **bake-silent even when misconfigured** — silence is expected, not evidence.

- **DON'T trust a clean console for a null `stat` — SILENT bake ABORT, real-bake proven** — a forced SubScene reimport with a
  null-stat temp clip grew the import-worker log by 17,245 bytes with ZERO error lines; `if (stat == null) return;` skips
  builder AND `base.Bake`. Same quiet-abort flavor as TimelineEssenceStatClip — NOT the EntityLinks loud-bake pattern, despite
  this clip carrying three link fields.
- **DON'T trust the default `from=Owner` — silent permanent skip; author `from=Self`** when the bound entity is endpoint A —
  an unwired Owner slot resolves `Entity.Null` → per-frame silent return (fourth family confirmation of the explicit-Self
  rule).
- **DON'T use multiplier=1 under ×100 encoding** — the truncation chain above.
- **DON'T expect a link override to win when the mode-entity reaches no link root — mode doubles as readRootFrom** (quoted
  above; proven live, §6 clip C).
- **DO distinguish replace-per-update from EssenceStat's single edge add** — EssenceStat adds ONE entry on activation, removes
  on deactivation; Distance enqueues a mutation EVERY update (remove-own-entry then re-add) — net one live entry, both removed
  at clip end. Corollary: GatherRemoveJob RE-resolves statTarget at remove time — a same-frame TargetPatch retarget can orphan
  the modifier (stagger such clips).
- **DO trust the interval timer — re-zeroed per activation, drift-free** — first frame ALWAYS samples; `Timer -= Interval`
  (not `= 0`) between samples; stale `DistanceToStatState` is never read on re-entry.
- **DON'T expect overlaps to blend — Blending|Looping is COSMETIC** — a grep of every Distance-package `.cs` for
  Weight/MixData/TrackBlend/ IAnimatedComponent/Ease/Blend hits exactly ONE line: the `clipCaps` declaration; overlapping
  clips' Added values SUM in the stat fold.
- **DON'T default to Continuous for slow-changing uses — `StatChanged` EVERY update** = full `StatCalculationSystem` refold of
  the receiver every frame; prefer `Interval`, or `OnStart` for one-shot snapshots.
- **DO rely on write-side stat-map self-heal — but mind the reader-only trap** — `StatModifierCalculator.ApplyTo` does
  `stats.Clear()` then `GetOrAddRefUnsafe(key)`, so the receiver does NOT need the stat in `StatDefaults`; but the key
  vanishes from the map once the modifier is removed — readers polling between/after clips see key-missing again.
- **DON'T create schema assets — ever** — reuse the project's `StatSchemaObject` inventory (re-count, inventories drift).

## 4. TRACK-SPECIFIC DISCOVERY DELTA

Run the shared discovery preamble (`unity-timeline-track-authoring` §1) with `<TRACK_TYPE>` =
`BovineLabs.Timeline.Distance.Authoring.DistanceToStatTrack` (assembly `BovineLabs.Timeline.Distance.Authoring`) and
`<BIND_TARGET>` = `BovineLabs.Reaction.Authoring.Core.TargetsAuthoring`. Beyond the generic D4, this capstone clip needs THREE
endpoints resolved, not one:

- Dump the binding's `TargetsAuthoring` slots — every `Target` mode you use (from / to / statTarget) must be ASSIGNED (unset =
  silent skip). Record the endpoints' positions: the expected distance is your verification number.
- The stat receiver needs `StatAuthoring` with `AddStats=True` + `StatsCanBeModified=True` (the EssenceStat receiver gate).
- Discover `StatSchemaObject` assets + LIVE keys (`FindAssets("t:StatSchemaObject")`, then read each
  `key.Value`); **keys drift; NEVER create schema assets**. Guid-sweep the chosen stat for consumers — prefer zero gameplay
  consumers and neutral semantics (§6 reasoning): the asset you leave is an example designers copy.
- If using link overrides: discover `EntityLinkSchema` assets + ids and confirm the MODE-slot entity reaches a link root
  carrying the key (the readRootFrom quirk, §3).

## 5. CLIP PATTERNS (the bracket's track-specific middle)

Fill these into the shared SubScene bracket (`unity-timeline-track-authoring` §2). `from`/`to`/`statTarget`/`mode` are
byte-backed — set via `SerializedObject` `intValue` on the camelCase YAML names. One clip = one live distance feed; duration IS
the effect window (removed on deactivation; scrub/stop safe).

```csharp
// shared-bracket TRACK-SPECIFIC MIDDLE for DistanceToStat
var statSchema = UnityEditor.AssetDatabase.LoadMainAssetAtPath(statSchemaPath); // §4, NEVER created
var clip = track.CreateClip(/* DistanceToStatClip */);
clip.start = 0; clip.duration = 4; clip.displayName = "<clipName>";
var so = new UnityEditor.SerializedObject((UnityEngine.Object)clip.asset);
so.FindProperty("from").intValue = 4; so.FindProperty("to").intValue = 1;            // Self -> Target: NEVER trust Owner default; slots verified §4
so.FindProperty("statTarget").intValue = 4; so.FindProperty("stat").objectReferenceValue = statSchema; // Self receives
so.FindProperty("multiplier").floatValue = 100f; so.FindProperty("mode").intValue = 1; // the ×100 rule (cm units, meters back); Continuous
so.FindProperty("interval").floatValue = 0.5f;                                        // used only when mode=Interval(2)
so.ApplyModifiedPropertiesWithoutUndo();
```

- **A. Proximity-to-stat (continuous):** "feed live A↔B distance into a stat" → `from=Self`, `to=Target`, `statTarget=Self`,
  `multiplier=100`, `mode=Continuous`. The default capstone.
- **B. Periodic sample (interval):** "sample the distance every N seconds" → as A but `mode=Interval`, `interval=N`. Cheaper
  than Continuous (no per-frame refold). With `multiplier=1` it is the truncation cautionary tale (§6 clip B).
- **C. One-shot snapshot (OnStart):** "capture the distance once at clip start, hold it" → `mode=OnStart`; samples first frame
  only, holds until deactivation.
- **D. Measure to a linked thing:** for a link override to actually win, the MODE slot must point at an entity that reaches a
  link root — e.g. `to=Self` + `toLink=<schema>` hunts from the binding via its `EntityLinkSource.Root` to the link map.
  Verify the chain in §4 BEFORE authoring; a fallback is indistinguishable from a win when both resolve the same entity.

## 6. WORKED EXAMPLE DELTA (vex-ee lesson 14) — rediscover, never assume

Beyond the shared stage (`unity-timeline-track-authoring` §5):
- Asset `Assets/Training/14-distance-to-stat-track/DistanceMastery.playable`, one track `DistanceTrack` bound to
  **`Stage_Actor` (TargetsAuthoring)**; binding table grew 11 → **12** (#11 = `DistanceTrack (DistanceToStatTrack) →
  Stage_Actor`); director restored to `Assets/Training/01-transform-position-track/PositionMastery.playable`.
- Clips: A_ContinuousCm (0–4s, multiplier=100, Continuous), B_IntervalHalfSec (5–8s, multiplier=1, Interval 0.5 — the
  truncation lesson), C_LinkRouted (9–10s, toLink=Schema_Actor, living doc of the readRootFrom quirk); all `stat=Luck`,
  `from=Self`, `to=Target`, `statTarget=Self`. Track carries `resetOnDeactivate: 1`; no overlap → `m_BlendInDuration: -1`.
- Geometry: Stage_Actor (0,1,0) → Stage_Target (5,0,0), distance √26 ≈ **5.0990 m**. The ×100 numbers: A → 5.0990×100 = 509.90
  → round **510** → reads **5.10** ✓; B → 5.0990×1 → round **5** → reads **0.05** ✗ (99% destroyed); rounding vs truncation:
  5.099×100 → 510, not 509.
- Schema: 114 `StatSchemaObject`s under `Assets/Settings/Schemas/Stats/`. Demo stat `Luck.asset` (key **57**, guid
  `a1894082169143a99b790f676641cb90`) — guid-sweep found Luck referenced once (EssenceSettings registration only, zero gameplay
  consumers; SlowMo had 4 refs and was avoided); chosen over ComboCounter on neutral semantics. Link schema `Schema_Actor` guid
  `3b375c42affc2917f956d01310d31894`, id=10.
- Clip C corrected the curriculum prediction: the hunt starts at `targets.Get(Target)` = Stage_Target (no `EntityLinkSource`,
  no `EntityLinkEntry` buffer) → silent fail → fallback Stage_Target — C measures actor→cube ≈ 5.10 m like A, NOT 0. A true
  link-win here would need `to=Self` (hunt Stage_Actor → Stage_LinkRoot → `{10 → Stage_Actor}` — degenerate self→self = 0,
  indistinguishable from its own fallback).

## 7. UNDO + VERIFICATION DELTA

Undo and verification run per `unity-timeline-track-authoring` §3 / §4 — the standard four-artifact inventory (created
`.playable`, possibly-created folder, mutated `director.playableAsset`, added binding entry), restore-director-first ORDER, and
the UNDO-1/2/3/4 + fresh-load-verify templates. Track-specific notes:

- **Runtime undo: NONE needed.** The modifier is while-active and self-reverting — removed on the deactivation edge (scrub/stop
  safe), and the stat key vanishes from the receiver's map once removed (the fold rebuilds from modifiers + defaults). The
  workflow never enters play mode. RECORD (do not undo) one caveat: a same-frame TargetPatch retarget between add and remove
  can orphan the modifier — a live-world design caution, not an authoring artifact.
- **No stage mutation:** endpoints, receiver, schema assets, TargetsAuthoring slots, StatAuthoring fields are only READ — UNDO-3
  is empty.
- **Verification extras** (added to the shared §4 dump): on the fresh-load asset dump, read all three Target slots + link refs,
  `stat` + key, `multiplier`, `mode`, `interval`. Raw-YAML: `from`/`to`/`statTarget`/`mode` as bytes; `stat`/link guids present
  (no `{fileID: 0}` where intended); `resetOnDeactivate: 1`; no overlap → `m_BlendInDuration: -1`. Re-dump the schema key LIVE
  (drifts) and compute the expected distance from endpoint positions, sanity-checking `distance × multiplier → round → /100` by
  hand.
