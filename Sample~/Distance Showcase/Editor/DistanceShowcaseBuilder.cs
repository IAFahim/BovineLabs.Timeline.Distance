using System;
using System.Collections.Generic;
using TMPro;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using TargetsAuthoring = BovineLabs.Reaction.Authoring.Core.TargetsAuthoring;
using TargetSlot = BovineLabs.Reaction.Data.Core.Target;
using StatAuthoring = BovineLabs.Essence.Authoring.StatAuthoring;
using StatModifierAuthoring = BovineLabs.Essence.Authoring.StatModifierAuthoring;
using StatSchemaObject = BovineLabs.Essence.Authoring.StatSchemaObject;
using StatAuthoringType = BovineLabs.Essence.Authoring.StatAuthoringType;
using LifeCycleAuthoring = BovineLabs.Core.Authoring.LifeCycle.LifeCycleAuthoring;
using TimelineBeginAuthoring = BovineLabs.Timeline.Core.Authoring.TimelineBeginAuthoring;
using TimelineBeginMode = BovineLabs.Timeline.Core.Authoring.TimelineBeginMode;
using EntityLinkSchema = BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSchema;
using EntityLinkRootAuthoring = BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkRootAuthoring;
using EntityLinkSourceAuthoring = BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSourceAuthoring;
using DistanceTrack = BovineLabs.Timeline.Distance.Authoring.DistanceToStatTrack;
using DistanceClip = BovineLabs.Timeline.Distance.Authoring.DistanceToStatClip;
using DistanceMode = BovineLabs.Timeline.Distance.Data.DistanceUpdateMode;
using PositionTrack = BovineLabs.Timeline.Transform.Authoring.TransformPositionTrack;
using PositionClip = BovineLabs.Timeline.Transform.Authoring.PositionClip;
using PositionType = BovineLabs.Timeline.Transform.Authoring.PositionType;

public static class DistanceShowcaseBuilder
{
    private const string SampleFolder = "Assets/Samples/DistanceShowcase";
    private const string TimelineFolder = SampleFolder + "/Timelines";
    private const string ParentPath = SampleFolder + "/DistanceShowcase.unity";
    private const string SubPath = SampleFolder + "/DistanceShowcase_Sub.unity";

    private const string RequiredInSubScenePath = "Assets/Prefabs/Required In Subscene.prefab";
    private const string DashDistancePath = "Assets/Settings/Schemas/Stats/DashDistance.asset";
    private const string AoESizePath = "Assets/Settings/Schemas/Stats/AoESize.asset";
    private const string LinkSchemaPath = "Assets/Settings/Schemas/EntityLinks/Movement Body Link.asset";

    private static readonly Color ContColor = new Color(0.20f, 0.90f, 0.55f);
    private static readonly Color IntervalColor = new Color(0.25f, 0.75f, 0.95f);
    private static readonly Color OnStartColor = new Color(0.95f, 0.60f, 0.20f);
    private static readonly Color MultColor = new Color(0.70f, 0.45f, 0.95f);
    private static readonly Color LinkColor = new Color(0.20f, 0.85f, 0.80f);
    private static readonly Color ActorColor = new Color(0.85f, 0.85f, 0.90f);
    private static readonly Color TargetColor = new Color(0.95f, 0.25f, 0.25f);
    private static readonly Color LeaderColor = new Color(0.95f, 0.85f, 0.20f);
    private static readonly Color PadColor = new Color(0.22f, 0.24f, 0.29f);
    private static readonly Color BannerColor = new Color(0.06f, 0.08f, 0.12f);

    private const float ContX = -28f;
    private const float IntervalX = -14f;
    private const float OnStartX = 0f;
    private const float MultX = 14f;
    private const float LinkX = 28f;
    private const float RowStep = 7.5f;
    private const float ActorY = 1.0f;

    private static readonly Vector3 CameraPos = new Vector3(0f, 20f, -40f);

    private static Scene activeSub;
    private static StatSchemaObject dashDistance;
    private static StatSchemaObject aoeSize;
    private static EntityLinkSchema linkSchema;

    private sealed class CellWire
    {
        public string DirectorName;
        public string TimelinePath;
        public string TrackName;
        public string BindActorName;
    }

    private static readonly List<CellWire> Wires = new List<CellWire>();

    private sealed class CaptionData
    {
        public string Title;
        public string Usage;
        public Vector3 CellPos;
        public Color Color;
    }

    private static readonly List<CaptionData> Captions = new List<CaptionData>();

    [MenuItem("Showcase/Build Distance")]
    public static void Build()
    {
        Wires.Clear();
        Captions.Clear();

        dashDistance = AssetDatabase.LoadAssetAtPath<StatSchemaObject>(DashDistancePath);
        aoeSize = AssetDatabase.LoadAssetAtPath<StatSchemaObject>(AoESizePath);
        linkSchema = AssetDatabase.LoadAssetAtPath<EntityLinkSchema>(LinkSchemaPath);

        if (dashDistance == null || aoeSize == null || linkSchema == null)
        {
            Debug.LogError("DistanceShowcase: schema asset(s) missing. dashDistance=" + (dashDistance != null) +
                           " aoeSize=" + (aoeSize != null) + " linkSchema=" + (linkSchema != null));
            return;
        }

        EnsureFolders();
        ResetAssets();

        var parent = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(parent, ParentPath);
        var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);
        activeSub = sub;

        BuildRequiredInSubScene();
        BuildPads();
        BuildContinuousColumn();
        BuildIntervalColumn();
        BuildOnStartColumn();
        BuildMultiplierColumn();
        BuildLinkColumn();

        EditorSceneManager.SaveScene(sub, SubPath);
        EditorSceneManager.SetActiveScene(parent);
        EditorSceneManager.CloseScene(sub, true);

        sub = EditorSceneManager.OpenScene(SubPath, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);
        activeSub = sub;

        foreach (var w in Wires)
        {
            WireCell(w);
        }

        EditorSceneManager.MarkSceneDirty(sub);
        EditorSceneManager.SaveScene(sub);

        EditorSceneManager.SetActiveScene(parent);
        BuildParent();
        EditorSceneManager.SaveScene(parent);

        EditorSceneManager.CloseScene(sub, true);
        EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);

        Debug.Log("DistanceShowcase: built grid at " + ParentPath + " directors=" + Wires.Count +
                  " | DashDistance.Key=" + dashDistance.Key + " AoESize.Key=" + aoeSize.Key);
    }

    // ============================================================
    //  CONTINUOUS column (green) — distance written every frame.
    // ============================================================

    private static void BuildContinuousColumn()
    {
        // Row 0 — Self -> orbiting Target, multiplier=100, Continuous.
        {
            var z = 0 * RowStep;
            var cell = "Cont0";
            var actor = MakeActor(cell + "_Actor", new Vector3(ContX, ActorY, z), ActorColor);
            var target = MakeMovingTarget(cell, new Vector3(ContX, ActorY, z), TargetColor);
            actor.GetComponent<TargetsAuthoring>().Target = target;

            var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
            var t = timeline.CreateTrack<DistanceTrack>(null, "Distance");
            AddDistanceClip(t, 0.0, 12.0, "Self->Target cont", TargetSlot.Self, TargetSlot.Target,
                TargetSlot.Self, dashDistance, 100f, DistanceMode.Continuous, 0.5f);
            FinishCell(timeline, cell, ContX, z,
                "Continuous Self->Target",
                "from=Self to=Target statTarget=Self, multiplier=100, mode=Continuous. The RED cube orbits, so math.distance(Self,Target) changes every frame and is rounded to an Added modifier on DashDistance (key " +
                dashDistance.Key + ") -> the stat tracks the live metres (reads value/100).",
                ContColor, cell + "_Actor");
        }

        // Row 1 — Source -> orbiting Target (both endpoints non-Self).
        {
            var z = 1 * RowStep;
            var cell = "Cont1";
            var actor = MakeActor(cell + "_Actor", new Vector3(ContX, ActorY, z), ActorColor);
            var target = MakeMovingTarget(cell, new Vector3(ContX, ActorY, z), TargetColor);
            // a static Source anchor offset from the actor so the measured pair is Source<->Target
            var source = MakeAnchor(cell + "_Source", new Vector3(ContX - 3.0f, ActorY, z), LeaderColor);
            var ta = actor.GetComponent<TargetsAuthoring>();
            ta.Target = target;
            ta.Source = source;

            var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
            var t = timeline.CreateTrack<DistanceTrack>(null, "Distance");
            AddDistanceClip(t, 0.0, 12.0, "Source->Target cont", TargetSlot.Source, TargetSlot.Target,
                TargetSlot.Self, dashDistance, 100f, DistanceMode.Continuous, 0.5f);
            FinishCell(timeline, cell, ContX, z,
                "Continuous Source->Target",
                "from=Source (yellow anchor) to=Target (orbiting red) statTarget=Self. Exercises a non-Self pair: BOTH endpoints resolve from the bound Targets component; the receiver is still the bound actor. Distance updates every frame.",
                ContColor, cell + "_Actor");
        }
    }

    // ============================================================
    //  INTERVAL column (cyan) — distance resampled every N seconds.
    // ============================================================

    private static void BuildIntervalColumn()
    {
        BuildIntervalCell(0, 0.5f, "Interval 0.5s",
            "mode=Interval interval=0.5. Resamples the distance to the orbiting target every 0.5s (cheaper than per-frame). The stat steps in 0.5s increments rather than tracking smoothly.");
        BuildIntervalCell(1, 1.5f, "Interval 1.5s",
            "mode=Interval interval=1.5. Same track, slower cadence: the stat holds the last sample for 1.5s then jumps. Demonstrates the Interval timer (Timer+=DeltaTime, fire on Timer>=interval).");
    }

    private static void BuildIntervalCell(int row, float interval, string title, string usage)
    {
        var z = row * RowStep;
        var cell = "Intv" + row;
        var actor = MakeActor(cell + "_Actor", new Vector3(IntervalX, ActorY, z), ActorColor);
        var target = MakeMovingTarget(cell, new Vector3(IntervalX, ActorY, z), TargetColor);
        actor.GetComponent<TargetsAuthoring>().Target = target;

        var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
        var t = timeline.CreateTrack<DistanceTrack>(null, "Distance");
        AddDistanceClip(t, 0.0, 12.0, "interval " + interval, TargetSlot.Self, TargetSlot.Target,
            TargetSlot.Self, dashDistance, 100f, DistanceMode.Interval, interval);
        FinishCell(timeline, cell, IntervalX, z, title, usage, IntervalColor, cell + "_Actor");
    }

    // ============================================================
    //  ON-START column (orange) — single snapshot held until exit.
    // ============================================================

    private static void BuildOnStartColumn()
    {
        // Row 0 — long single clip: samples once, holds while the target keeps orbiting.
        {
            var z = 0 * RowStep;
            var cell = "Start0";
            var actor = MakeActor(cell + "_Actor", new Vector3(OnStartX, ActorY, z), ActorColor);
            var target = MakeMovingTarget(cell, new Vector3(OnStartX, ActorY, z), TargetColor);
            actor.GetComponent<TargetsAuthoring>().Target = target;

            var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
            var t = timeline.CreateTrack<DistanceTrack>(null, "Distance");
            AddDistanceClip(t, 0.0, 12.0, "OnStart hold", TargetSlot.Self, TargetSlot.Target,
                TargetSlot.Self, dashDistance, 100f, DistanceMode.OnStart, 0.5f);
            FinishCell(timeline, cell, OnStartX, z,
                "OnStart snapshot (hold)",
                "mode=OnStart: writes the distance ONLY on the first active frame, then holds that one value the whole clip even though the red target keeps orbiting. The stat freezes at the activation snapshot; re-samples each loop.",
                OnStartColor, cell + "_Actor");
        }

        // Row 1 — re-snapshot cadence: 4 short OnStart clips chained so it re-samples 4x per loop.
        {
            var z = 1 * RowStep;
            var cell = "Start1";
            var actor = MakeActor(cell + "_Actor", new Vector3(OnStartX, ActorY, z), ActorColor);
            var target = MakeMovingTarget(cell, new Vector3(OnStartX, ActorY, z), TargetColor);
            actor.GetComponent<TargetsAuthoring>().Target = target;

            var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
            var t = timeline.CreateTrack<DistanceTrack>(null, "Distance");
            for (var i = 0; i < 4; i++)
                AddDistanceClip(t, i * 3.0, 2.6, "snap " + i, TargetSlot.Self, TargetSlot.Target,
                    TargetSlot.Self, dashDistance, 100f, DistanceMode.OnStart, 0.5f);
            FinishCell(timeline, cell, OnStartX, z,
                "OnStart re-snapshot x4",
                "Four chained OnStart clips. Each activation grabs one fresh distance sample of the orbiting target and holds it; the stat advances in 4 discrete steps per loop (each clip-enter = one new snapshot).",
                OnStartColor, cell + "_Actor");
        }
    }

    // ============================================================
    //  MULTIPLIER column (purple) — the x100 fixed-point rule.
    // ============================================================

    private static void BuildMultiplierColumn()
    {
        // Row 0 — multiplier=100 (correct): integer Added = centimetres, reads metres.
        {
            var z = 0 * RowStep;
            var cell = "Mult0";
            var actor = MakeActor(cell + "_Actor", new Vector3(MultX, ActorY, z), ActorColor);
            var target = MakeMovingTarget(cell, new Vector3(MultX, ActorY, z), TargetColor);
            actor.GetComponent<TargetsAuthoring>().Target = target;

            var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
            var t = timeline.CreateTrack<DistanceTrack>(null, "Distance");
            AddDistanceClip(t, 0.0, 12.0, "x100 correct", TargetSlot.Self, TargetSlot.Target,
                TargetSlot.Self, dashDistance, 100f, DistanceMode.Continuous, 0.5f);
            FinishCell(timeline, cell, MultX, z,
                "multiplier=100 (correct)",
                "x100 fixed-point rule: distance*100 -> round -> int Added; readers divide by 100. e.g. 5.099m -> 510 -> reads 5.10. Precision preserved to centimetres.",
                MultColor, cell + "_Actor");
        }

        // Row 1 — multiplier=1 (the trap): rounds to whole metres -> reads ~0.05.
        {
            var z = 1 * RowStep;
            var cell = "Mult1";
            var actor = MakeActor(cell + "_Actor", new Vector3(MultX, ActorY, z), ActorColor);
            var target = MakeMovingTarget(cell, new Vector3(MultX, ActorY, z), TargetColor);
            actor.GetComponent<TargetsAuthoring>().Target = target;

            var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
            var t = timeline.CreateTrack<DistanceTrack>(null, "Distance");
            AddDistanceClip(t, 0.0, 12.0, "x1 trap", TargetSlot.Self, TargetSlot.Target,
                TargetSlot.Self, dashDistance, 1f, DistanceMode.Continuous, 0.5f);
            FinishCell(timeline, cell, MultX, z,
                "multiplier=1 (the trap)",
                "multiplier=1 under x100 encoding destroys precision: 5.099m -> round 5 -> stored 5 -> reader divides by 100 -> reads 0.05m. Distance ROUNDS its int (Essence Stat clip truncates). Cautionary tale.",
                MultColor, cell + "_Actor");
        }
    }

    // ============================================================
    //  LINK column (teal) — link-override slot (readRootFrom quirk).
    // ============================================================

    private static void BuildLinkColumn()
    {
        var z = 0 * RowStep;
        var cell = "Link0";

        // Leader sphere carries the link Root + a source advertising the schema.
        var leaderHome = new Vector3(LinkX, ActorY + 2.0f, z);
        var leader = MakeAnchor(cell + "_Leader", leaderHome, LeaderColor);
        leader.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        var rootAuth = leader.AddComponent<EntityLinkRootAuthoring>();
        var leaderSrc = leader.AddComponent<EntityLinkSourceAuthoring>();
        leaderSrc.Root = rootAuth;
        leaderSrc.Schemas = new[] { linkSchema };
        rootAuth.Links = new[] { leaderSrc };
        DriveOrbit(cell + "_Leader", leader, leaderHome, 3.2f);

        // Actor binds Targets + an EMPTY-schema source under the SAME root, so the
        // mode-slot (to=Self) hops Actor -> Root -> EntityLinkEntry{key -> leader}.
        var actor = MakeActor(cell + "_Actor", new Vector3(LinkX, ActorY, z), ActorColor);
        var actorSrc = actor.AddComponent<EntityLinkSourceAuthoring>();
        actorSrc.Root = rootAuth;
        actorSrc.Schemas = Array.Empty<EntityLinkSchema>();

        var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
        var t = timeline.CreateTrack<DistanceTrack>(null, "Distance");
        var c = AddDistanceClip(t, 0.0, 12.0, "to=Self+toLink", TargetSlot.Self, TargetSlot.Self,
            TargetSlot.Self, dashDistance, 100f, DistanceMode.Continuous, 0.5f);
        ((DistanceClip)c.asset).toLink = linkSchema;
        Dirty(c.asset);

        FinishCell(timeline, cell, LinkX, z,
            "Link override (to=Self+toLink)",
            "to=Self with toLink=Movement Body Link: the mode-slot DOUBLES as the link-hunt root. The actor reaches the leader's EntityLinkRoot via its empty-schema source, so 'to' resolves to the orbiting YELLOW leader (not the actor itself). Distance to the linked entity feeds the stat. If the chain failed it would silently fall back to targets.Get(Self)=0.",
            LinkColor, cell + "_Actor");
    }

    // ============================================================
    //  actor / target / clip builders
    // ============================================================

    private static GameObject MakeActor(string name, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.position = pos;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, color);

        go.AddComponent<LifeCycleAuthoring>();

        var stats = go.AddComponent<StatAuthoring>();
        stats.AddStats = true;
        stats.StatsCanBeModified = true;
        stats.StatDefaults = new[]
        {
            new StatModifierAuthoring { Stat = dashDistance, ModifyType = StatAuthoringType.Added, Value = 0f },
        };

        var targets = go.AddComponent<TargetsAuthoring>();
        targets.Owner = go;
        targets.Source = go;
        targets.Custom = go;
        targets.Target = go;

        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    // A static visible anchor (no stat) used as a Source / leader body.
    private static GameObject MakeAnchor(string name, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, color);
        go.AddComponent<LifeCycleAuthoring>();
        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    // Visible cube that orbits via its own Transform position timeline, so the
    // measured distance to it changes continuously every frame.
    private static GameObject MakeMovingTarget(string cell, Vector3 home, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = cell + "_Target";
        go.transform.position = home;
        go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(cell + "_Target", color);
        go.AddComponent<LifeCycleAuthoring>();
        SceneManager.MoveGameObjectToScene(go, activeSub);

        DriveOrbit(cell + "_Target", go, home, 3.4f);
        return go;
    }

    // Builds a 4-leg position timeline that sweeps the object far/near so the
    // distance between it and the actor visibly varies each loop.
    private static void DriveOrbit(string name, GameObject go, Vector3 home, float reach)
    {
        var dirName = name + "_OrbitDir";
        var timelinePath = TimelineFolder + "/" + name + "_Orbit.playable";
        MakeDirector(dirName);

        var timeline = NewTimeline(timelinePath);
        var track = timeline.CreateTrack<PositionTrack>(null, "Position");
        track.ResetPositionOnDeactivate = true;

        var a = AddWorldPos(track, 0.0, 3.0, "far+X", home + new Vector3(reach, 0.4f, 0f));
        var b = AddWorldPos(track, 3.0, 3.0, "near", home + new Vector3(0.6f, -0.2f, reach * 0.4f));
        var d = AddWorldPos(track, 6.0, 3.0, "far-X", home + new Vector3(-reach, 0.6f, 0f));
        var e = AddWorldPos(track, 9.0, 3.0, "home", home);
        a.blendInDuration = 0.6; b.blendInDuration = 0.6; d.blendInDuration = 0.6; e.blendInDuration = 0.6;

        FixDuration(timeline);
        Dirty(timeline, track);
        AssetDatabase.SaveAssets();

        Wires.Add(new CellWire
        {
            DirectorName = dirName,
            TimelinePath = timelinePath,
            TrackName = "Position",
            BindActorName = go.name,
        });
    }

    private static TimelineClip AddWorldPos(PositionTrack t, double start, double dur, string name, Vector3 world)
    {
        var c = AddClip<PositionClip>(t, start, dur, name);
        var a = (PositionClip)c.asset;
        a.Type = PositionType.World;
        a.Position = world;
        Dirty(c.asset);
        return c;
    }

    private static TimelineClip AddDistanceClip(TrackAsset t, double start, double dur, string name,
        TargetSlot from, TargetSlot to, TargetSlot statTarget, StatSchemaObject stat,
        float multiplier, DistanceMode mode, float interval)
    {
        var c = AddClip<DistanceClip>(t, start, dur, name);
        var a = (DistanceClip)c.asset;
        a.from = from;
        a.fromLink = null;
        a.to = to;
        a.toLink = null;
        a.statTarget = statTarget;
        a.statTargetLink = null;
        a.stat = stat;
        a.multiplier = multiplier;
        a.mode = mode;
        a.interval = interval;
        Dirty(c.asset);
        return c;
    }

    // ============================================================
    //  wire / caption plumbing
    // ============================================================

    private static void FinishCell(TimelineAsset timeline, string cell, float x, float z,
        string label, string usage, Color color, string actorName)
    {
        FixDuration(timeline);
        Dirty(timeline);
        foreach (var tr in timeline.GetOutputTracks()) Dirty(tr);
        AssetDatabase.SaveAssets();

        var dirName = cell + "_Director";
        MakeDirector(dirName);
        Wires.Add(new CellWire
        {
            DirectorName = dirName,
            TimelinePath = AssetDatabase.GetAssetPath(timeline),
            TrackName = "Distance",
            BindActorName = actorName,
        });
        Captions.Add(new CaptionData { Title = label, Usage = usage, CellPos = new Vector3(x, 4.2f, z), Color = color });
    }

    private static void WireCell(CellWire w)
    {
        var director = GameObject.Find(w.DirectorName).GetComponent<PlayableDirector>();
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(w.TimelinePath);
        director.playableAsset = timeline;

        foreach (var track in timeline.GetOutputTracks())
        {
            if (track.name != w.TrackName) continue;
            var actor = GameObject.Find(w.BindActorName);
            if (track is PositionTrack)
                director.SetGenericBinding(track, actor.transform);
            else
                director.SetGenericBinding(track, actor.GetComponent<TargetsAuthoring>());
        }

        EditorUtility.SetDirty(director);
    }

    private static PlayableDirector MakeDirector(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        var director = go.AddComponent<PlayableDirector>();
        director.playOnAwake = true;
        director.extrapolationMode = DirectorWrapMode.Loop;
        var begin = go.AddComponent<TimelineBeginAuthoring>();
        begin.Mode = TimelineBeginMode.OnLoad;
        begin.DelaySeconds = 0f;
        return director;
    }

    private static TimelineAsset NewTimeline(string path)
    {
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, path);
        return timeline;
    }

    private static TimelineClip AddClip<T>(TrackAsset track, double start, double duration, string name) where T : PlayableAsset
    {
        var clip = track.CreateClip<T>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = name;
        return clip;
    }

    private static void FixDuration(TimelineAsset timeline)
    {
        var end = 0.0;
        foreach (var track in timeline.GetOutputTracks())
            foreach (var clip in track.GetClips())
            {
                var clipEnd = clip.start + clip.duration;
                if (clipEnd > end) end = clipEnd;
            }

        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = end;
    }

    // ============================================================
    //  primitives / parent scene
    // ============================================================

    private static GameObject MakePad(string name, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, PadColor);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    private static void BuildRequiredInSubScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RequiredInSubScenePath);
        if (prefab == null)
        {
            Debug.LogWarning("DistanceShowcase: '" + RequiredInSubScenePath + "' missing; runtime singletons may be absent.");
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "Required In Subscene";
        SceneManager.MoveGameObjectToScene(go, activeSub);
    }

    private static void BuildPads()
    {
        float[] xs = { ContX, IntervalX, OnStartX, MultX, LinkX };
        string[] names = { "Continuous", "Interval", "OnStart", "Multiplier", "Link" };
        var zCenter = RowStep * 0.5f;
        for (var i = 0; i < xs.Length; i++)
            MakePad(names[i] + "_Pad", new Vector3(xs[i], 0.05f, zCenter), new Vector3(11.0f, 0.12f, RowStep * 2f + 4f));
    }

    private static Material MakeMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { name = name + "_Mat" };
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        return mat;
    }

    private static void BuildParent()
    {
        FrameCamera();
        RenderSettings.fog = false;

        MakeBanner("Title_Banner", new Vector3(0f, 18.2f, 0f), new Vector3(64f, 3.6f, 0.1f));
        MakeWorldLabel("Title", "DISTANCE TIMELINE GRID — DISTANCE TO STAT", new Vector3(0f, 18.6f, -0.4f), 64f, Color.white, 5.0f, TextAlignmentOptions.Center);
        MakeWorldLabel("Subtitle", "one track (DistanceToStat) writing live metric distance into a stat   ·   com.bovinelabs.timeline.distance", new Vector3(0f, 17.1f, -0.4f), 64f, new Color(0.85f, 0.9f, 1f), 1.9f, TextAlignmentOptions.Center);

        MakeColumnHeader("Cont_Header", "CONTINUOUS", ContX, ContColor);
        MakeColumnHeader("Interval_Header", "INTERVAL", IntervalX, IntervalColor);
        MakeColumnHeader("OnStart_Header", "ON START", OnStartX, OnStartColor);
        MakeColumnHeader("Mult_Header", "MULTIPLIER", MultX, MultColor);
        MakeColumnHeader("Link_Header", "LINK OVERRIDE", LinkX, LinkColor);

        foreach (var cap in Captions)
            MakeCaption(cap.Title, cap.Usage, cap.CellPos, cap.Color);

        MakeBanner("Usage_Banner", new Vector3(0f, 0.7f, -9.5f), new Vector3(70f, 2.4f, 0.1f));
        MakeWorldLabel("Usage",
            "Each white capsule carries StatAuthoring (AddStats + StatsCanBeModified) bound to a DistanceToStat track. A RED cube (or YELLOW linked leader) orbits via its own Transform timeline so math.distance changes every frame; the track rounds distance*multiplier into an Added modifier on DashDistance. CONTINUOUS=every frame · INTERVAL=every N s · ON START=one snapshot · MULTIPLIER shows the x100 precision rule (100 correct vs 1 trap) · LINK resolves 'to' through an EntityLink root. Effects are NUMERIC (stat buffer value), not transform motion. FixedLength + Loop.",
            new Vector3(0f, 0.7f, -9.8f), 68f, new Color(0.96f, 0.97f, 1f), 1.5f, TextAlignmentOptions.Center);

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath);
        if (sceneAsset == null)
        {
            Debug.LogError("DistanceShowcase: sub-scene asset missing at " + SubPath);
            return;
        }

        var subSceneGo = new GameObject("Showcase SubScene");
        var subScene = subSceneGo.AddComponent<SubScene>();
        subScene.SceneAsset = sceneAsset;
        subScene.AutoLoadScene = true;
        EditorUtility.SetDirty(subScene);
    }

    private static void MakeColumnHeader(string name, string text, float x, Color color)
    {
        var pos = new Vector3(x, 5.4f, -5.5f);
        MakeBanner(name + "_Banner", pos + new Vector3(0f, 0f, 0.08f), new Vector3(10.4f, 1.5f, 0.1f));
        MakeWorldLabel(name, "<b>" + text + "</b>", pos, 10.2f, color, 2.8f, TextAlignmentOptions.Center);
    }

    private static float CaptionY(float z)
    {
        return 5.4f + z * 0.12f;
    }

    private static void MakeCaption(string title, string usage, Vector3 cellPos, Color color)
    {
        var z = cellPos.z;
        var y = CaptionY(z);
        MakeBanner("CapBanner_" + title + "_" + z, new Vector3(cellPos.x, y, z + 0.06f), new Vector3(10.0f, 2.4f, 0.05f));
        MakeWorldLabel("Cap_" + title + "_" + z, "<b>" + title + "</b>", new Vector3(cellPos.x, y + 0.6f, z), 10.0f, color, 2.2f, TextAlignmentOptions.Center);
        MakeWorldLabel("Use_" + title + "_" + z, usage, new Vector3(cellPos.x, y - 0.5f, z), 10.0f, new Color(0.95f, 0.96f, 1f), 1.1f, TextAlignmentOptions.Center);
    }

    private static void FrameCamera()
    {
        var required = GameObject.Find("Required In Scene");
        if (required == null) return;
        var camTransform = required.transform.Find("Main Camera");
        if (camTransform == null) return;
        camTransform.position = CameraPos;
        camTransform.rotation = Quaternion.Euler(22f, 0f, 0f);
        var cam = camTransform.GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = 62f;
            cam.farClipPlane = 500f;
            EditorUtility.SetDirty(cam);
        }

        EditorUtility.SetDirty(camTransform);
    }

    private static void MakeBanner(string name, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(pos - CameraPos, Vector3.up);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, BannerColor);
    }

    private static void MakeWorldLabel(string name, string text, Vector3 pos, float width, Color color, float fontSize, TextAlignmentOptions alignment)
    {
        var holder = new GameObject(name);
        holder.transform.position = pos;
        holder.transform.rotation = Quaternion.LookRotation(pos - CameraPos, Vector3.up);

        var go = new GameObject("Text");
        go.transform.SetParent(holder.transform, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.rectTransform.sizeDelta = new Vector2(width, 4f);
        tmp.rectTransform.localPosition = Vector3.zero;
        tmp.fontStyle = FontStyles.Bold;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Samples"))
            AssetDatabase.CreateFolder("Assets", "Samples");
        if (!AssetDatabase.IsValidFolder(SampleFolder))
            AssetDatabase.CreateFolder("Assets/Samples", "DistanceShowcase");
        if (!AssetDatabase.IsValidFolder(TimelineFolder))
            AssetDatabase.CreateFolder(SampleFolder, "Timelines");
    }

    private static void ResetAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TimelineFolder) != null)
            foreach (var guid in AssetDatabase.FindAssets("t:TimelineAsset", new[] { TimelineFolder }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

        foreach (var p in new[] { ParentPath, SubPath })
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) != null)
                AssetDatabase.DeleteAsset(p);
    }

    private static void Dirty(params UnityEngine.Object[] objects)
    {
        foreach (var o in objects)
            EditorUtility.SetDirty(o);
    }
}
