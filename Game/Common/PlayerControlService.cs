using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Central gate for any player-driven controls. Systems can register blockers so that
    /// mouse/keyboard input, UI interactions, etc. can be disabled consistently (e.g. during enemy turns,
    /// cutscenes, or ability cinematics).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerControlService : MonoBehaviour
    {
        static PlayerControlService _instance;
        public static PlayerControlService Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindFirstObjectByType<PlayerControlService>(FindObjectsInactive.Exclude);
                return _instance;
            }
        }

        public static PlayerControlService Ensure()
        {
            var svc = Instance;
            if (svc != null) return svc;

            var go = new GameObject(nameof(PlayerControlService));
            svc = go.AddComponent<PlayerControlService>();
            return svc;
        }

        [Tooltip("Optional: keep this service alive across scene loads.")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        readonly HashSet<object> _blockers = new();

        public bool IsControlEnabled => _blockers.Count == 0;

        public event Action<bool> OnControlStateChanged;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public IDisposable AcquireBlock(object owner = null)
        {
            owner ??= new object();
            var token = new ControlToken(this, owner);
            SetBlocked(owner, true);
            return token;
        }

        public void SetBlocked(object owner, bool blocked)
        {
            if (owner == null) owner = this;

            bool changed;
            if (blocked)
                changed = _blockers.Add(owner);
            else
                changed = _blockers.Remove(owner);

            if (changed)
                OnControlStateChanged?.Invoke(IsControlEnabled);
        }

        sealed class ControlToken : IDisposable
        {
            readonly PlayerControlService _service;
            readonly object _owner;
            bool _disposed;

            public ControlToken(PlayerControlService service, object owner)
            {
                _service = service;
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_service != null)
                    _service.SetBlocked(_owner, false);
            }
        }
    }
}
