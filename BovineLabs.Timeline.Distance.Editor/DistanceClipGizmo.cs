using BovineLabs.Reaction.Authoring.Core;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Distance.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Distance.Editor
{
    /// <summary>
    /// Edit-time scene preview for a selected <see cref="DistanceToStatClip"/>: draws the From→To segment it will
    /// measure and the resulting stat value, so a designer sees the measurement before entering play. Resolves the
    /// endpoints exactly like the clip bakes them (Target slot on the bound <see cref="TargetsAuthoring"/>, or the
    /// optional <see cref="EntityLinkSchema"/> override). Play-mode visualization is the runtime Quill debug system.
    /// </summary>
    [InitializeOnLoad]
    public static partial class DistanceClipGizmo
    {
        private static readonly Color LineColor = new(0.2f, 0.9f, 0.7f);

        static DistanceClipGizmo()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        // CoreCLR/no-domain-reload: unsubscribe before this assembly unloads on a code reload, else the sub
        // accumulates per recompile (draws multiply).
        [OnCodeUnloading]
        private static void OnCodeUnloading() => SceneView.duringSceneGui -= OnSceneGui;

        private static void OnSceneGui(SceneView view)
        {
            if (Application.isPlaying)
            {
                return; // runtime uses the Quill DistanceToStatDebugSystem.
            }

            var clips = TimelineEditor.selectedClips;
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            var director = TimelineEditor.inspectedDirector;
            if (director == null)
            {
                return;
            }

            foreach (var timelineClip in clips)
            {
                if (timelineClip?.asset is not DistanceToStatClip clip)
                {
                    continue;
                }

                if (director.GetGenericBinding(timelineClip.GetParentTrack()) is not Component bound)
                {
                    continue;
                }

                var from = ResolveEndpoint(bound, clip.from, clip.fromLink);
                var to = ResolveEndpoint(bound, clip.to, clip.toLink);
                if (from == null || to == null)
                {
                    continue;
                }

                Draw(from.position, to.position, clip.multiplier);
            }
        }

        private static void Draw(Vector3 from, Vector3 to, float multiplier)
        {
            using (new Handles.DrawingScope(LineColor))
            {
                Handles.DrawAAPolyLine(4f, from, to);
                Handles.DrawWireDisc(from, Vector3.up, 0.1f);
                Handles.DrawWireDisc(to, Vector3.up, 0.1f);

                var distance = Vector3.Distance(from, to);
                var statValue = Mathf.RoundToInt(distance * multiplier);
                var mid = (from + to) * 0.5f + Vector3.up * 0.25f;
                Handles.Label(mid, $"{distance:0.##}m  ->  [{statValue}]");
            }
        }

        // Mirrors the bake: an EntityLinkSchema override resolves through the bound object's link root; otherwise the
        // Target slot on the bound TargetsAuthoring (Self = the bound object, Owner/Source fall back to the root).
        private static Transform ResolveEndpoint(Component bound, Target target, EntityLinkSchema link)
        {
            if (link != null)
            {
                var root = bound.GetComponentInParent<EntityLinkRootAuthoring>(true)
                           ?? bound.GetComponentInChildren<EntityLinkRootAuthoring>(true);
                return root != null && EntityLinkAuthoringUtility.TryFindLinkedComponent(root, link, out var linked)
                    ? linked.transform
                    : null;
            }

            if (target == Target.Self)
            {
                return bound.transform;
            }

            var targets = bound as TargetsAuthoring ?? bound.GetComponent<TargetsAuthoring>();
            var rootGo = bound.transform.root.gameObject;

            var go = target switch
            {
                Target.Owner => targets != null && targets.Owner != null ? targets.Owner : rootGo,
                Target.Source => targets != null && targets.Source != null ? targets.Source : rootGo,
                Target.Target => targets != null ? targets.Target : null,
                Target.Custom => targets != null ? targets.Custom : null,
                _ => null,
            };

            return go != null ? go.transform : null;
        }
    }
}
