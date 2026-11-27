using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Audio
{
    public enum AudioCue { Hover, Select, Fail, Discover, StepComplete, ActivityComplete, UiClick, MenuOpen, Teleport }
    public enum AudioCategory { Environment, Interaction }

    [CreateAssetMenu(menuName = "ABYSS/Audio Cue Library", fileName = "AudioCueLibrary")]
    public class AudioCueLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public AudioCue Cue;
            public AudioClip[] Clips;
            [Range(0f, 1f)] public float Volume = 0.6f;
            [Tooltip("0 = flat 2D, 1 = fully positioned at the object")]
            [Range(0f, 1f)] public float Spatial = 0.7f;
            public Vector2 PitchRange = new Vector2(0.97f, 1.03f);
        }

        public List<Entry> Cues = new List<Entry>();

        [Header("Ambience")]
        public AudioClip AmbientLoop;
        [Range(0f, 1f)] public float AmbientVolume = 0.5f;
        public AudioClip BubblesLoop;
        [Range(0f, 1f)] public float BubblesVolume = 0.25f;

        public Entry Get(AudioCue cue)
        {
            foreach (var e in Cues) if (e.Cue == cue) return e;
            return null;
        }
    }
}
