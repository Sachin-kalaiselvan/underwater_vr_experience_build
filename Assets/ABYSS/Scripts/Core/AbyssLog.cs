using UnityEngine;

namespace Abyss.Core
{
    // tiny wrapper so every log line is tagged and dev-only spam can be stripped from release builds
    public static class AbyssLog
    {
        const string Tag = "[ABYSS] ";

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string msg, Object ctx = null) => Debug.Log(Tag + msg, ctx);

        public static void Warn(string msg, Object ctx = null) => Debug.LogWarning(Tag + msg, ctx);

        public static void Error(string msg, Object ctx = null) => Debug.LogError(Tag + msg, ctx);
    }
}
