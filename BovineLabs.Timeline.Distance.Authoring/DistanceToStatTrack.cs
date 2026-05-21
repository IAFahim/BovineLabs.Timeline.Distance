using System;
using System.ComponentModel;
using BovineLabs.Reaction.Authoring.Core;
using BovineLabs.Timeline.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Distance.Authoring
{
    [Serializable]
    [TrackClipType(typeof(DistanceToStatClip))]
    [TrackColor(0.2f, 0.9f, 0.7f)]
    [TrackBindingType(typeof(TargetsAuthoring))]
    [DisplayName("BovineLabs/Distance/Distance To Stat")]
    public sealed class DistanceToStatTrack : DOTSTrack
    {
    }
}