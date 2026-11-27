using System;

namespace Abyss.Logic
{
    public enum TurnMode { Snap, Smooth }
    public enum QualityProfile { Standard, Reduced }

    // everything the visitor (or the exhibition staff) can change.
    // defaults = most comfortable setup for someone who has never used VR
    [Serializable]
    public class SettingsData
    {
        public int Version = 1;

        // comfort
        public bool TeleportEnabled = true;
        public bool SmoothMoveEnabled = false;
        public TurnMode Turn = TurnMode.Snap;
        public float SnapAngle = 45f;
        public float SmoothTurnSpeed = 60f;
        public float SmoothMoveSpeed = 1.2f;
        public bool Vignette = true;
        public bool Stationary = false;      // kills all artificial movement, just look around
        public bool ReducedMotion = false;   // calmer fish, no panel animation, no particle drift

        // accessibility
        public float TextScale = 1f;         // 0.8 - 1.6
        public bool HighContrast = false;

        // audio, 0-1
        public float MasterVolume = 0.8f;
        public float EnvironmentVolume = 0.7f;
        public float InteractionVolume = 0.8f;
        public bool Muted = false;

        // other
        public bool HandednessLeft = false;
        public bool SkipIntroNextTime = false;
        public QualityProfile Quality = QualityProfile.Standard;

        public const float MinTextScale = 0.8f, MaxTextScale = 1.6f;

        public SettingsData Clone() => (SettingsData)MemberwiseClone();

        // keep stuff in sane ranges, also used after loading old/hand-edited json
        public void Clamp()
        {
            TextScale = Clamp(TextScale, MinTextScale, MaxTextScale);
            SnapAngle = Clamp(SnapAngle, 15f, 90f);
            SmoothTurnSpeed = Clamp(SmoothTurnSpeed, 20f, 180f);
            SmoothMoveSpeed = Clamp(SmoothMoveSpeed, 0.5f, 3f);
            MasterVolume = Clamp(MasterVolume, 0f, 1f);
            EnvironmentVolume = Clamp(EnvironmentVolume, 0f, 1f);
            InteractionVolume = Clamp(InteractionVolume, 0f, 1f);
            // need at least one way to get around unless stationary is on on purpose
            if (!TeleportEnabled && !SmoothMoveEnabled && !Stationary) TeleportEnabled = true;
        }

        public float EffectiveVolume(float category) => Muted ? 0f : MasterVolume * category;

        static float Clamp(float v, float a, float b) => float.IsNaN(v) ? a : (v < a ? a : (v > b ? b : v));
    }
}
