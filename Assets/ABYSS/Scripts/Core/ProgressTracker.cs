using System;
using Abyss.Logic;
using UnityEngine;

namespace Abyss.Core
{
    // unity side of ProgressStore, handles saving. exhibitions usually want
    // progress wiped per visitor so that's a toggle
    [DefaultExecutionOrder(-250)]
    public class ProgressTracker : MonoBehaviour
    {
        const string Key = "abyss.progress.v1";

        [Tooltip("off = every launch starts fresh, good for exhibition kiosks")]
        [SerializeField] bool _persistBetweenSessions = false;

        public ProgressStore Store { get; } = new ProgressStore();
        public event Action Changed;

        void Awake()
        {
            Services.Register(this);
            if (_persistBetweenSessions)
            {
                var json = PlayerPrefs.GetString(Key, null);
                if (!string.IsNullOrEmpty(json))
                {
                    try { Store.Load(JsonUtility.FromJson<ProgressData>(json)); }
                    catch (Exception e) { AbyssLog.Warn($"progress file broken, starting fresh ({e.Message})"); }
                }
            }
            Store.Changed += OnStoreChanged;
        }

        void OnDestroy()
        {
            Store.Changed -= OnStoreChanged;
            Services.Unregister(this);
        }

        void OnStoreChanged()
        {
            if (_persistBetweenSessions)
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(Store.Data));
                PlayerPrefs.Save();
            }
            Changed?.Invoke();
        }

        public void MarkActivityCompleted(string id) => Store.MarkCompleted(id, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public bool MarkDiscovered(string id) => Store.MarkDiscovered(id);
        public void ResetForNextVisitor() => Store.ResetAll();
    }
}
