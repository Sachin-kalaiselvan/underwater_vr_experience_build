using System;
using Abyss.Logic;
using UnityEngine;

namespace Abyss.Core
{
    // loads/saves SettingsData as json in PlayerPrefs. call Apply() after changing stuff
    [DefaultExecutionOrder(-300)]
    public class SettingsManager : MonoBehaviour
    {
        const string Key = "abyss.settings.v1";

        public SettingsData Current { get; private set; } = new SettingsData();
        public event Action<SettingsData> Changed;

        void Awake()
        {
            Services.Register(this);
            Load();
        }

        void OnDestroy() => Services.Unregister(this);

        public void Load()
        {
            var json = PlayerPrefs.GetString(Key, null);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    Current = JsonUtility.FromJson<SettingsData>(json) ?? new SettingsData();
                }
                catch (Exception e)
                {
                    AbyssLog.Warn($"settings were broken, going back to defaults ({e.Message})");
                    Current = new SettingsData();
                }
            }
            Current.Clamp();
        }

        // change settings through here so everybody hears about it
        public void Modify(Action<SettingsData> edit)
        {
            edit?.Invoke(Current);
            Current.Clamp();
            Save();
            Changed?.Invoke(Current);
        }

        public void ResetToDefaults()
        {
            Current = new SettingsData();
            Save();
            Changed?.Invoke(Current);
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current));
            PlayerPrefs.Save();
        }

        // fire once on start so listeners can sync up
        void Start() => Changed?.Invoke(Current);
    }
}
