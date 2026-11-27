using System.Collections.Generic;
using Abyss.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Abyss.Interaction
{
    // drop this on any object with a collider and give it a definition. that's it.
    // XRSimpleInteractable does the ray hit stuff, this just forwards to the manager
    [RequireComponent(typeof(XRSimpleInteractable))]
    [DisallowMultipleComponent]
    public class InteractableObject : MonoBehaviour, IAbyssInteractable
    {
        [SerializeField] InteractableDefinition _definition;
        [Tooltip("leave empty to use every renderer under this object")]
        [SerializeField] Renderer[] _highlightRenderers;
        [SerializeField] Material _highlightMaterial;

        XRSimpleInteractable _xr;
        Pose _home;
        Transform _homeParent;
        readonly List<GameObject> _shells = new List<GameObject>();
        bool _highlighted;

        public string Id => _definition != null ? _definition.Id : name;
        public InteractableDefinition Definition => _definition;
        public Transform Root => transform;
        public bool IsInteractable => isActiveAndEnabled && _xr != null && _xr.enabled;

        public void Configure(InteractableDefinition def, Material highlight)
        {
            _definition = def;
            _highlightMaterial = highlight;
        }

        void Awake()
        {
            _xr = GetComponent<XRSimpleInteractable>();
            _home = new Pose(transform.position, transform.rotation);
            _homeParent = transform.parent;
            if (_definition == null) AbyssLog.Warn($"{name} has no InteractableDefinition", this);
            BuildHighlightShells();
        }

        void OnEnable()
        {
            _xr.hoverEntered.AddListener(OnHoverEnter);
            _xr.hoverExited.AddListener(OnHoverExit);
            _xr.selectEntered.AddListener(OnSelectEnter);
            Services.Get<InteractionManager>()?.Register(this);
        }

        void OnDisable()
        {
            _xr.hoverEntered.RemoveListener(OnHoverEnter);
            _xr.hoverExited.RemoveListener(OnHoverExit);
            _xr.selectEntered.RemoveListener(OnSelectEnter);
            Services.Get<InteractionManager>()?.Unregister(this);
            SetHighlighted(false);
        }

        // InteractionManager may not exist yet in OnEnable on first load
        void Start() => Services.Get<InteractionManager>()?.Register(this);

        void OnHoverEnter(HoverEnterEventArgs e) => Services.Get<InteractionManager>()?.NotifyHover(this, true, e.interactorObject);
        void OnHoverExit(HoverExitEventArgs e) => Services.Get<InteractionManager>()?.NotifyHover(this, false, e.interactorObject);

        void OnSelectEnter(SelectEnterEventArgs e)
        {
            Services.Get<InteractionManager>()?.NotifySelect(this, e.interactorObject);
            // simple interactable stays "selected" while the trigger is held, we only care about the click
            if (_xr.interactionManager != null && e.interactorObject is IXRSelectInteractor sel)
                _xr.interactionManager.SelectExit(sel, _xr);
        }

        public virtual void OnSelected(IXRInteractor by) { }

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on) return;
            _highlighted = on;
            foreach (var s in _shells) if (s != null) s.SetActive(on);
        }

        public void ResetToHome()
        {
            transform.SetParent(_homeParent, true);
            transform.SetPositionAndRotation(_home.position, _home.rotation);
            SetHighlighted(false);
        }

        public Pose HomePose => _home;

        // highlight = a second copy of the mesh with a rim shader, only on while hovered.
        // costs one extra draw while active and nothing otherwise
        void BuildHighlightShells()
        {
            if (_highlightMaterial == null) return;
            var rends = (_highlightRenderers != null && _highlightRenderers.Length > 0)
                ? _highlightRenderers : GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var go = new GameObject("_highlight");
                go.transform.SetParent(r.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _highlightMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.SetActive(false);
                _shells.Add(go);
            }
        }
    }
}
