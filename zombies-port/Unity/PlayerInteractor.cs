// PlayerInteractor.cs
//
// Replaces the GSC use trigger: `self waittill("trigger", user)` fired when a player stood in
// a trigger_use volume and pressed Use (Square on PS3, X on Xbox). On mobile, this component
// tracks which interactable the player is standing in, publishes its prompt for the on-screen
// button, and runs the interaction when that button is tapped.
//
// Setup: put this next to ZombiePlayer on the player object. Each interactable needs a
// trigger collider ("Is Trigger" checked). Unity only sends trigger events when one side has
// a Rigidbody or a CharacterController, so a CharacterController player works as is.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    public enum InteractResult { Done, NotEnoughPoints, Unavailable }

    /// <summary>What the on-screen button should show. GSC: set_hint_string / sethintstring.</summary>
    public struct InteractPrompt : IEquatable<InteractPrompt>
    {
        public bool Visible;
        public string Label;
        public int Cost;          // 0 = no cost shown
        public bool Affordable;
        public float Progress;    // 0..1 for hold actions such as reviving; 0 = no bar

        public static readonly InteractPrompt Hidden = new InteractPrompt();

        public bool Equals(InteractPrompt o) =>
            Visible == o.Visible && Label == o.Label && Cost == o.Cost && Affordable == o.Affordable &&
            Math.Abs(Progress - o.Progress) < 0.01f;
    }

    /// <summary>Anything the context button can use: Mystery Box now, wall buys, perks and doors later.</summary>
    public interface IInteractable
    {
        Vector3 InteractPosition { get; }
        InteractPrompt GetPrompt(ZombiePlayer player);
        InteractResult Interact(ZombiePlayer player);
    }

    [RequireComponent(typeof(ZombiePlayer))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("Seconds between prompt checks. Checking every frame wastes battery.")]
        [SerializeField] float refreshInterval = 0.1f;

        public ZombiePlayer Player { get; private set; }
        public IInteractable Current { get; private set; }
        public InteractPrompt Prompt { get; private set; }
        /// <summary>
        /// True while the interact button is held down. Hold actions (reviving a teammate,
        /// GSC: is_reviving() = can_revive() and UseButtonPressed()) read this every frame.
        /// </summary>
        public bool IsHolding { get; private set; }

        public void SetHolding(bool holding) => IsHolding = holding;

        public event Action<InteractPrompt> PromptChanged;
        public event Action<InteractResult> Interacted;

        readonly List<IInteractable> _inRange = new List<IInteractable>();
        float _nextRefresh;

        void Awake() => Player = GetComponent<ZombiePlayer>();

        void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<IInteractable>();
            if (target != null && !_inRange.Contains(target)) _inRange.Add(target);
        }

        void OnTriggerExit(Collider other)
        {
            var target = other.GetComponentInParent<IInteractable>();
            if (target != null) _inRange.Remove(target);
        }

        void Update()
        {
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + refreshInterval;
            Refresh();
        }

        /// <summary>Picks the nearest in-range interactable whose prompt is visible.</summary>
        public void Refresh()
        {
            // Unity's "fake null": drop interactables that were destroyed while in range.
            _inRange.RemoveAll(i => i is UnityEngine.Object o && o == null);

            IInteractable best = null;
            InteractPrompt bestPrompt = InteractPrompt.Hidden;
            float bestDist = float.MaxValue;
            Vector3 pos = transform.position;

            foreach (var candidate in _inRange)
            {
                var prompt = candidate.GetPrompt(Player);
                if (!prompt.Visible) continue;
                float d = (candidate.InteractPosition - pos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = candidate; bestPrompt = prompt; }
            }

            Current = best;
            if (!bestPrompt.Equals(Prompt))
            {
                Prompt = bestPrompt;
                PromptChanged?.Invoke(Prompt);
            }
        }

        /// <summary>The on-screen button calls this. It is the mobile version of pressing Use.</summary>
        public void TryInteract()
        {
            if (Current == null) return;
            var result = Current.Interact(Player);
            Interacted?.Invoke(result);
            Refresh(); // Update the prompt immediately (for example "Mystery Box" to nothing while spinning).
        }
    }
}
