using System.Collections.Generic;
using Abyss.Activities;
using Abyss.Audio;
using Abyss.Core;
using Abyss.Logic;
using UnityEngine;

namespace Abyss.UI
{
    public enum UIScreen { None, MainMenu, ActivityPicker, Pause, Settings, Help, Completion }

    // one menu open at a time. remembers where you came from so Back works
    [DefaultExecutionOrder(-100)]
    public class UIManager : MonoBehaviour
    {
        readonly Dictionary<UIScreen, MenuPanel> _screens = new Dictionary<UIScreen, MenuPanel>();
        readonly Stack<UIScreen> _history = new Stack<UIScreen>();
        MenuPanel _open;

        public UIScreen Current => _open != null ? _open.Screen : UIScreen.None;
        public ActivityDefinition LastCompleted { get; private set; }

        void Awake()
        {
            Services.Register(this);
            foreach (var p in GetComponentsInChildren<MenuPanel>(true)) _screens[p.Screen] = p;
        }

        void OnEnable()
        {
            var s = Services.Get<SettingsManager>();
            if (s != null) { s.Changed += ApplyTheme; ApplyTheme(s.Current); }
        }

        void OnDisable()
        {
            var s = Services.Get<SettingsManager>();
            if (s != null) s.Changed -= ApplyTheme;
        }

        void OnDestroy() => Services.Unregister(this);

        void ApplyTheme(SettingsData s)
        {
            UITheme.UseHighContrast = s.HighContrast;
            UITheme.TextScale = s.TextScale;
            UITheme.NotifyChanged();
        }

        public void Show(UIScreen screen)
        {
            if (!_screens.TryGetValue(screen, out var panel)) { AbyssLog.Warn($"no panel for {screen}"); return; }
            bool keepPlace = _open != null; // moving between menus: stay where you are, don't jump
            Vector3 pos = keepPlace ? _open.transform.position : default;
            Quaternion rot = keepPlace ? _open.transform.rotation : default;

            if (_open != null && _open != panel)
            {
                _history.Push(_open.Screen);
                _open.Close();
            }
            _open = panel;
            if (keepPlace) { panel.transform.SetPositionAndRotation(pos, rot); panel.Open(placeInFront: false); }
            else panel.Open();
            AudioManager.PlayCue(AudioCue.MenuOpen, panel.transform.position);
        }

        public void Back()
        {
            if (_history.Count == 0) { HideAll(); return; }
            var prev = _history.Pop();
            var cur = _open;
            _open = null;
            if (cur != null)
            {
                var pos = cur.transform.position; var rot = cur.transform.rotation;
                cur.Close();
                if (_screens.TryGetValue(prev, out var p))
                {
                    _open = p;
                    p.transform.SetPositionAndRotation(pos, rot);
                    p.Open(placeInFront: false);
                }
            }
        }

        public void HideAll()
        {
            _history.Clear();
            if (_open != null) _open.Close();
            _open = null;
        }

        public void ShowCompletion(ActivityDefinition def)
        {
            LastCompleted = def;
            HideAll();
            Show(UIScreen.Completion);
        }
    }
}
