using System;
using System.Collections;
using Abyss.Core;
using Abyss.Locomotion;
using TMPro;
using UnityEngine;

namespace Abyss.UI
{
    // the first minute: logo, fade into the water, four short cards, then you're in charge.
    // every card has Skip. menu button skips too. never traps anyone
    public class OnboardingSequence : WorldPanel
    {
        [Serializable]
        public class Card
        {
            public string Heading;
            [TextArea(2, 4)] public string Body;
        }

        [SerializeField] Card[] _cards =
        {
            new Card { Heading = "Point and select", Body = "Aim either controller at something.\nWhen it glows, pull the trigger." },
            new Card { Heading = "Moving around", Body = "Push the left stick forward, aim at the sea floor, let go.\nFlick the right stick to turn. You can also just walk a step or two." },
            new Card { Heading = "The menu", Body = "Press the menu button on your left controller any time to pause, change comfort settings or get help." },
            new Card { Heading = "Getting back", Body = "Lost? Open the menu and choose \"Return to the start point\"." },
        };
        [SerializeField] float _logoSeconds = 2.2f;

        TextMeshProUGUI _heading, _body, _step;
        Transform _buttons;
        int _index;
        Action _onDone;
        Coroutine _run;
        bool _skip, _next;

        void Awake()
        {
            Services.Register(this);
            Init("Onboarding", new Vector2(820, 520));
            _step = UIFactory.Text(Content, "", UITheme.SmallSize, ThemeRole.TextDim, FontStyles.Bold);
            _heading = UIFactory.Text(Content, "", UITheme.TitleSize, ThemeRole.Text, FontStyles.Bold);
            _body = UIFactory.Text(Content, "", UITheme.BodySize, ThemeRole.TextDim);
            var le = _body.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); le.flexibleHeight = 1;
            _buttons = UIFactory.Row(Content).transform;
        }

        void OnDestroy() => Services.Unregister(this);

        public bool IsPlaying => _run != null;

        public void Play(Action onDone)
        {
            _onDone = onDone;
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Run());
        }

        public void Skip() => _skip = true;

        IEnumerator Run()
        {
            _skip = false;
            Services.Get<ExperienceStateManager>()?.SetState(ExperienceState.Intro);
            var fader = Services.Get<ScreenFader>();

            // identity: water colour, the name, then fade into the world
            if (fader != null) fader.Set(1f);
            ShowLogo();
            Open();
            for (float t = 0; t < _logoSeconds && !_skip; t += Time.unscaledDeltaTime) yield return null;
            Close();
            if (fader != null) yield return fader.FadeTo(0f, 1.2f);

            for (_index = 0; _index < _cards.Length && !_skip; _index++)
            {
                ShowCard(_index);
                Open();
                _next = false;
                while (!_next && !_skip) yield return null;
                // stay where it is between cards, re-placing every card is annoying
                yield return null;
            }

            if (fader != null && fader.Alpha > 0f) yield return fader.FadeTo(0f, 0.3f);
            Close();
            _run = null;
            var cb = _onDone; _onDone = null;
            cb?.Invoke();
        }

        void ShowLogo()
        {
            _step.text = "";
            _heading.text = "<cspace=0.3em>ABYSS</cspace>";
            _body.text = "An underwater exhibition";
            Clear();
        }

        void ShowCard(int i)
        {
            var c = _cards[i];
            _step.text = $"{i + 1} of {_cards.Length}";
            _heading.text = c.Heading;
            _body.text = c.Body;
            Clear();
            UIFactory.Button(_buttons, "Skip intro", () => _skip = true);
            UIFactory.Button(_buttons, i == _cards.Length - 1 ? "Start" : "Next", () => _next = true, primary: true);
        }

        void Clear()
        {
            for (int i = _buttons.childCount - 1; i >= 0; i--) Destroy(_buttons.GetChild(i).gameObject);
        }
    }
}
