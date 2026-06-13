---
name: unity-track-distance-to-stat
description: Master of DistanceToStatTrack + DistanceToStatClip (package BovineLabs.Timeline.Distance) — continuous/interval distance measurement into a live-updating while-active stat modifier, the multiplier=100 rule, the mode-doubles-as-readRootFrom link quirk. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks "feed the distance between A and B into a stat".
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
`EntityLinkEntry` search, key 0 / missing buffer = silent fail); stage construction → `unity-stage-foundations`. Behave per
unity-agent-protocol; operate the editor per unity-cli.

## 2. PORTABLE SEMANTICS

True in ANY project containing `BovineLabs.Timeline.Distance` (plus its Essence/Reaction/EntityLinks dependencies). Provenance
tags = where PROVEN, not where it applies. (All verified vex-ee 2026-06 via reflection dumps, package-source reads, raw YAML,
fresh-load read-backs, one real forced SubScene bake — all `unity-cli exec`, no play mode; runtime claims source-derived.)

| Type | Facts |
|---|---|
| `DistanceToStatTrack` | `BovineLabs.Timeline.Distance.Authoring`, sealed, base `DOTSTrack`. `[TrackClipType(DistanceToStatClip)]`, `[TrackBindingType(BovineLabs.Reaction.Authoring.Core.TargetsAuthoring)]`, `[TrackColor(0.20,0.90,0.70)]`, `[DisplayName("BovineLabs/Distance/Distance To Stat")]`. The lone Distance-package track. |
| `DistanceToStatClip` | sealed, base `DOTSClip`, `clipCaps => ClipCaps.Blending \| ClipCaps.Looping` (COSMETIC — see traps), `duration => 1` (seed only). |
| `DistanceUpdateMode` | `BovineLabs.Timeline.Distance.Data`, **byte-backed**: `OnStart=0, Continuous=1, Interval=2`. |
| System | `DistanceToStatSystem` — `[UpdateInGroup(TimelineComponentAnimationGroup)]`, `[UpdateAfter(typeof(EntityLinkTargetPatchSystem))]` (sees same-frame TargetPatch retargets, like the Essence triad). |

### Clip fields — camelCase (reflection + fresh-instance defaults)

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

(The `Target` enum, verified: `None=0, Target=1, Owner=2, Source=3, Self=4, Custom=6`; None/unset slot → `Entity.Null`.)

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

### Runtime semantics (one paragraph, source-quoted)

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
back as 0.05 — 99% of the value destroyed; concrete numbers in §5). Distance ROUNDS (`(int)math.round`) where EssenceStat
TRUNCATES (`(int)value`).

### Traps & DO/DON'T (each proven live or source-quoted, vex-ee 2026-06)

- **DON'T trust a clean console for a null `stat` — SILENT bake ABORT, real-bake proven** — a forced SubScene reimport with a
  null-stat temp clip grew the import-worker log by 17,245 bytes with ZERO error lines; `if (stat == null) return;` skips
  builder AND `base.Bake`. Same quiet-abort flavor as TimelineEssenceStatClip — NOT the EntityLinks loud-bake pattern, despite
  this clip carrying three link fields.
- **DON'T trust the default `from=Owner` — silent permanent skip; author `from=Self`** when the bound entity is endpoint A —
  an unwired Owner slot resolves `Entity.Null` → per-frame silent return (fourth family confirmation of the explicit-Self
  rule).
- **DON'T use multiplier=1 under ×100 encoding** — the truncation chain above.
- **DON'T expect a link override to win when the mode-entity reaches no link root — mode doubles as readRootFrom** (quoted
  above; proven live, §5 clip C).
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
- **DON'T create schema assets — ever** — reuse the project's `StatSchemaObject` inventory (discover per §3.4; re-count,
  inventories drift).

## 3. DISCOVERY RECIPES

Act only through `unity-cli exec` / `unity-cli console`; never the filesystem; never play mode. Follow the unity-cli Safe Loop
on every mutation. Names below are parameters — discover them in THIS project; never assume the worked example (§5).

**3.1 Confirm the package exists (else report a missing prerequisite — protocol §6):**
```csharp
var t = System.Type.GetType("BovineLabs.Timeline.Distance.Authoring.DistanceToStatTrack, BovineLabs.Timeline.Distance.Authoring");
if (t == null) foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
    { t = asm.GetType("BovineLabs.Timeline.Distance.Authoring.DistanceToStatTrack"); if (t != null) break; }
return t == null ? "MISSING_PREREQUISITE|DistanceToStatTrack not found - package BovineLabs.Timeline.Distance is absent" : "OK|" + t.AssemblyQualifiedName + "|dataPath=" + UnityEngine.Application.dataPath;
```

**3.2 Find the active scene + SubScene(s):** run the unity-cli First Command; record `parentScenePath` + candidate
`subScenePath`(s).

**3.3 Find PlayableDirector(s) inside the SubScene** (read-only additive open, restore parent after):
`FindObjectsByType<PlayableDirector>(Include, None)`; print hierarchy path, scene.path, playableAsset path-or-null, other
components (DOTS timelines need a timeline-reference authoring component on the director); state your selection rule; zero
directors → protocol §6.

**3.4 Find/confirm the bind target, BOTH endpoints, the receiver, and the stat schema.** The track binds the
**`TargetsAuthoring` COMPONENT** of a SubScene-baked object. Dump the binding's `TargetsAuthoring` slots — every `Target` mode
you use must be ASSIGNED (unset = silent skip); record the endpoints' positions (the expected distance is your verification
number). The stat receiver needs `StatAuthoring` with `AddStats=True` + `StatsCanBeModified=True` (the EssenceStat receiver
gate). Discover stat schemas and keys LIVE — **keys drift; NEVER create schema assets**:
```csharp
var sb = new System.Text.StringBuilder();
foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:StatSchemaObject")) {
    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
    var so = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadMainAssetAtPath(p));
    sb.AppendLine("STAT_SCHEMA|" + p + "|key=" + so.FindProperty("key").FindPropertyRelative("Value").intValue);
}
return sb.ToString();
```
Guid-sweep the chosen stat for consumers — prefer zero gameplay consumers and neutral semantics (§5 reasoning): the asset you
leave is an example designers copy. If using link overrides: discover `EntityLinkSchema` assets + ids and confirm the
MODE-slot entity reaches a link root carrying the key (the readRootFrom quirk, §2).

**3.5 Capture the chosen director's existing state — this is pre-state (`PRE|`)**:
```csharp
// PRE|playableAsset=<asset PATH or null>   via AssetDatabase.GetAssetPath(director.playableAsset)
// PRE|binding|<i>|<track name>|<track type>|<bound object hierarchy path + component type, or null>
//   one line per GetOutputTracks() of the CURRENT asset, via director.GetGenericBinding(track).
// Capture the asset PATH and each track's NAME/index even when the table looks empty — they are what
// makes the undo journal replayable (UNDO-1 reloads the old asset by path, re-binds by name/index).
```
Record these in the undo journal (§6) before any mutation.

**Name resolution rule**: `GameObject.Find` misses inactive objects and is ambiguous on duplicates — confirm the chosen name
is active and unique in the SubScene, else walk the SubScene roots to the recorded hierarchy path (or `FindObjectsByType`
filtered by `scene`).

## 4. CANONICAL RECIPES

One logical change per exec block; print `PRE|` captures before mutating (protocol §2), save in-block, verify fresh (§7).

**4.1 The proximity-stat pattern — create timeline + track + clip, wire:**

```csharp
// ---- parameters (discovered in §3 / chosen with designer) ----
var parentScenePath = "<DISCOVERED>"; var subScenePath = "<DISCOVERED>";
var directorGoName  = "<DISCOVERED>"; var bindTargetPath = "<DISCOVERED>"; // carries TargetsAuthoring, slots verified
var statSchemaPath  = "<DISCOVERED>"; var assetFolder = "<CHOSEN>"; var assetPath = assetFolder + "/<Name>.playable"; // schema §3.4, NEVER created

var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(subScene);
try {
    // CAPTURE (print + journal): PRE|folderExisted=<bool> PRE|assetExisted=<bool>
    var folderExisted = UnityEditor.AssetDatabase.IsValidFolder(assetFolder);
    var timeline = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
    UnityEditor.AssetDatabase.CreateAsset(timeline, assetPath);
    var track = timeline.CreateTrack(/* §3.1 track type */, null, "<trackName>");
    var statSchema = UnityEditor.AssetDatabase.LoadMainAssetAtPath(statSchemaPath);

    // One clip = one live distance feed; duration IS the effect window (removed on deactivation; scrub/stop safe).
    // Mode: OnStart = snapshot once; Continuous = per-frame (full refold every frame); Interval = every N seconds.
    var clip = track.CreateClip(/* DistanceToStatClip type */);
    clip.start = 0; clip.duration = 4; clip.displayName = "<clipName>";
    var so = new UnityEditor.SerializedObject((UnityEngine.Object)clip.asset);
    so.FindProperty("from").intValue = 4; so.FindProperty("to").intValue = 1; // Self -> Target: NEVER trust the Owner default; slots verified §3.4
    so.FindProperty("statTarget").intValue = 4; so.FindProperty("stat").objectReferenceValue = statSchema; // Self receives
    so.FindProperty("multiplier").floatValue = 100f; so.FindProperty("mode").intValue = 1; // the ×100 rule (cm units, meters back); Continuous
    so.FindProperty("interval").floatValue = 0.5f; // used only when mode=Interval(2)
    // link overrides only when the mode-entity reaches a link root (§2 quirk): so.FindProperty("toLink")...
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.AssetDatabase.SaveAssets();

    var director = UnityEngine.GameObject.Find(directorGoName).GetComponent<UnityEngine.Playables.PlayableDirector>();
    // CAPTURE (print + journal) BEFORE mutating: PRE|playableAsset=<asset path or null>
    //   and PRE|binding|<each output track of the CURRENT asset>|<GetGenericBinding value>
    var bindComp = UnityEngine.GameObject.Find(bindTargetPath).GetComponent<BovineLabs.Reaction.Authoring.Core.TargetsAuthoring>();  // the COMPONENT
    director.playableAsset = timeline;
    director.SetGenericBinding(track, bindComp);
    UnityEditor.EditorUtility.SetDirty(director); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
    return "OK|" + assetPath;
} finally {
    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(parentScene); UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
}
```

**4.2 The CORRECTED link-routing recipe (measure to a linked thing):** for a link override to actually win, the mode slot must
point at an entity that reaches a link root — e.g. `to=Self` + `toLink=<schema>` hunts from the binding via its
`EntityLinkSource.Root` to the link map. Verify the chain in §3.4 BEFORE authoring; a fallback is indistinguishable from a win
when both resolve the same entity. Verify per §7 in SEPARATE blocks.

## 5. WORKED EXAMPLE (vex-ee training stage) — example environment; rediscover, never assume

- Project: `/home/i/GitHub/vex-ee` (`dataPath=/home/i/GitHub/vex-ee/Assets`); parent scene `Assets/Scenes/Main Scene.unity`;
  SubScene `Assets/Scenes/Main Sub Scene.unity`; `Stage_Director` (the only director, + TimelineReferenceAuthoring). Geometry:
  Stage_Actor world (0,1,0) → Stage_Target (5,0,0), distance √26 ≈ **5.0990 m**. Stage_Actor's Targets:
  `Owner=null|Source=null|Target=Stage_Target` (the from=Owner trap's root); StatDefaults[0]={SlowMo, Added, 25} (the
  lesson-13 ×100 correction).
- Schemas: **114** `StatSchemaObject`s under `Assets/Settings/Schemas/Stats/`. Demo stat `Luck.asset` (key **57**, guid
  `a1894082169143a99b790f676641cb90`): a guid sweep found Luck (and ComboCounter) referenced exactly once each — only the
  EssenceSettings registration, zero gameplay consumers (SlowMo by contrast: 4 refs — correctly avoided); Luck chosen over
  ComboCounter on neutral semantics. Link schema `Schema_Actor` guid `3b375c42affc2917f956d01310d31894`, id=10.
- Asset built in training (lesson 14): `Assets/Training/14-distance-to-stat-track/DistanceMastery.playable` — one track
  `DistanceTrack`, clips A_ContinuousCm (0–4s, multiplier=100, Continuous), B_IntervalHalfSec (5–8s, multiplier=1, Interval
  0.5 — the truncation lesson), C_LinkRouted (9–10s, toLink=Schema_Actor, kept as living documentation of the readRootFrom
  quirk); all `stat=Luck`, `from=Self`, `to=Target`, `statTarget=Self`. Track carries DOTSTrack's `resetOnDeactivate: 1`; no
  overlap → `m_BlendInDuration: -1`.
- The ×100 numbers on this stage: A → 5.0990×100 = 509.90 → round **510** → reads **5.10** ✓ (meters preserved to cm); B →
  5.0990×1 → round **5** → reads **0.05** ✗ (99% destroyed); rounding vs truncation: 5.099×100 → 510, not 509.
- Clip C corrected the curriculum's prediction: the hunt starts at `targets.Get(Target)` = Stage_Target (no
  `EntityLinkSource`, no `EntityLinkEntry` buffer) → silent fail → fallback Stage_Target — C measures actor→cube ≈ 5.10 m like
  A, NOT 0. The link-win configuration here is `to=Self` (hunt: Stage_Actor → Stage_LinkRoot → `{10 → Stage_Actor}` —
  degenerate self→self = 0, indistinguishable from its own fallback).
- Wiring: binding table grew 11 → **12**, #11 = `DistanceTrack (DistanceToStatTrack) → Stage_Actor (TargetsAuthoring)`; prior
  11 verified byte-for-byte intact; director restored to
  `Assets/Training/01-transform-position-track/PositionMastery.playable`.
- Known pre-existing console baseline: UnityCliConnector HTTP server start, PerformanceTesting
  IPrebuildSetup/IPostBuildCleanup, TestResults.xml save, lessons 08–10 `[Worker2]` EntityLinks bake errors.

## 6. UNDO APPENDIX

Runtime note first: this track's effect is **while-active and self-reverting** — the modifier is removed on the deactivation
edge (scrub/stop safe), and the stat key vanishes from the receiver's map entirely once removed (the fold rebuilds from
modifiers + defaults). No lingering runtime state to compensate. One runtime caveat to RECORD (not undo): a same-frame
TargetPatch retarget between add and remove can orphan the modifier — a design caution for live worlds, not an authoring
artifact. The workflow never enters play mode. Undo is purely the authoring artifacts:

Artifact inventory for one run of §4 (vex-ee instance shown in §5):
1. Created asset `<assetPath>` (.playable; `DeleteAsset` removes the track/clip sub-assets with the file).
2. Possibly-created folder(s) `<assetFolder>` (only if `PRE|folderExisted=false`).
3. Mutated `director.playableAsset` (vex-ee lesson 14: `EXPECTED:` previously `PositionMastery.playable` — implied by the
   lesson-13 restore and the lesson-14 restore target, but not printed pre-wiring in the report; capture it yourself per
   §3.5).
4. Added generic binding entry for the new track in the SubScene file (vex-ee: table 11 → 12, prior 11 verified byte-for-byte
   intact post-wiring; `EXPECTED:` the pre-wiring per-entry dump is not in the report — capture the full table per §3.5).
5. No scene values, schema assets, TargetsAuthoring slots, or StatAuthoring fields changed (schema reuse is mandatory;
   endpoints are only read).

ORDER: restore the director FIRST (so nothing references the asset), THEN delete the asset, THEN restore other captured values
— deleting first leaves a dangling `{fileID: 0}` reference and destroys the track objects `ClearGenericBinding` needs.

Journal entry templates (protocol §5 — fill from YOUR captures, reverse order):

```csharp
// UNDO-1: restore director's captured playableAsset + binding table (SubScene bracket)
var parentScenePath = "<CAPTURED>"; var subScenePath = "<CAPTURED>"; var directorGoName = "<CAPTURED>"; var assetPath = "<CAPTURED>";
var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(subScene);
try {
    var director = UnityEngine.GameObject.Find(directorGoName).GetComponent<UnityEngine.Playables.PlayableDirector>();
    var myAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(assetPath);
    foreach (var tr in myAsset.GetOutputTracks()) director.ClearGenericBinding(tr);   // entries I added for MY tracks
    // restore each CAPTURED binding (PRE|binding| lines): reload the PREVIOUS playable asset by captured path, match
    // tracks by name/index, re-find bound objects by captured hierarchy path, SetGenericBinding(prevTrack, boundComponent).
    director.playableAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Playables.PlayableAsset>("<CAPTURED pre path>"); // CAPTURED value (or null if captured null), never "default"
    UnityEditor.EditorUtility.SetDirty(director); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
    return "UNDONE|director restored";
} finally {
    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(parentScene); UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
}
```

```csharp
// UNDO-2: delete the created .playable (+ folder, only if PRE|folderExisted=false and now empty)
var assetPath = "<CAPTURED>"; var assetFolder = "<CAPTURED>"; var folderExisted = false; // <CAPTURED>
var ok = UnityEditor.AssetDatabase.DeleteAsset(assetPath);
if (!folderExisted && UnityEditor.AssetDatabase.FindAssets("", new[]{ assetFolder }).Length == 0)
    UnityEditor.AssetDatabase.DeleteAsset(assetFolder);
return "UNDONE|deleted=" + ok + "|" + assetPath;
```

```csharp
// UNDO-3: restore any other captured scene values — normally none beyond UNDO-1 (endpoints/receiver are
// only read; no stage mutation needed); include only entries your own journal recorded.
```

UNDO-4 (verify, fresh load — protocol §7): reload the SubScene additively; `director.playableAsset` and the binding table must
equal the CAPTURED `PRE|` values; `AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)` must be null; restore the
parent scene; console clean against the project baseline.

## 7. VERIFICATION PROTOCOL

1. **Fresh-load asset dump**: new exec block; `LoadAssetAtPath` the `.playable`, dump every track/clip (name, start/duration,
   all three Target slots + link refs, `stat` + key, `multiplier`, `mode`, `interval`). In-memory state after a save is not
   evidence.
2. **Raw YAML check**: enum fields as bytes (`from`/`to`/`statTarget`/`mode`); `stat` and any link guids present (no `{fileID:
   0}` where a ref was intended); no overlap → `m_BlendInDuration: -1`; track carries `resetOnDeactivate: 1` (inherited
   DOTSTrack default).
3. **Stage checks**: re-dump the chosen schema's key live (§3.4 — keys and inventories drift); dump the binding's Targets
   slots (every used mode assigned); compute the expected distance from the endpoints' recorded positions and sanity-check
   `distance × multiplier → round → /100` by hand.
4. **Binding check from a RELOADED SubScene**: expect `BINDING|<trackName>|bound=<bindTarget> (TargetsAuthoring)` — the
   component, not the Transform; prior entries intact.
5. **Parent-scene restore**: end with `sceneCount=1`, `scene[0]=<parentScenePath>|loaded=True|active=True|dirty=False`.
6. **Console**: `unity-cli console --filter error` shows nothing new beyond the project's known baseline (vex-ee baseline in
   §5). This clip is bake-silent even when misconfigured — silence is expected, not evidence.
