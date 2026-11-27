using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Core
{
    // lives in the Bootstrap scene with the XR rig and all managers.
    // the environment scene gets loaded additively on top so it can be swapped out
    // without touching the rig or the systems
    public class ApplicationBootstrap : MonoBehaviour
    {
        [SerializeField] string _environmentScene = "MainExperience";
        [SerializeField] float _minIdentitySeconds = 2.5f;

        public static bool EnvironmentReady { get; private set; }
        public event System.Action EnvironmentLoaded;

        void Awake()
        {
            Services.Register(this);
            EnvironmentReady = false;
            // keep the whole root alive, it owns the rig
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = -1; // XR compositor drives timing
            QualitySettings.vSyncCount = 0;
        }

        void OnDestroy() => Services.Unregister(this);

        IEnumerator Start()
        {
            var state = Services.Get<ExperienceStateManager>();
            state?.SetState(ExperienceState.Intro);

            float t0 = Time.realtimeSinceStartup;

            if (!SceneManager.GetSceneByName(_environmentScene).isLoaded)
            {
                var op = SceneManager.LoadSceneAsync(_environmentScene, LoadSceneMode.Additive);
                if (op == null)
                {
                    AbyssLog.Error($"couldn't load scene '{_environmentScene}', is it in build settings?");
                    yield break;
                }
                while (!op.isDone) yield return null;
            }

            var env = SceneManager.GetSceneByName(_environmentScene);
            if (env.IsValid()) SceneManager.SetActiveScene(env);

            // let the logo breathe a bit even if loading was instant
            while (Time.realtimeSinceStartup - t0 < _minIdentitySeconds) yield return null;

            EnvironmentReady = true;
            EnvironmentLoaded?.Invoke();
            AbyssLog.Info("environment ready");
        }
    }
}
