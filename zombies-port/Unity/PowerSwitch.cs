// PowerSwitch.cs
//
// The map's power switch. In BO1 the perk machines stay dark until a player throws the
// switch (each machine waits for "<perk>_power_on"); solo Quick Revive is the exception.
// Free to use, once per game. Players use it through the interact button like everything else.
//
// If a scene has no PowerSwitch, perk machines treat the power as on, so a test scene works
// without one.

using System;
using System.Collections;
using UnityEngine;

namespace ZombiesPort
{
    public class PowerSwitch : MonoBehaviour, IInteractable
    {
        [Tooltip("Optional handle that swings when the power comes on.")]
        [SerializeField] Transform lever;
        [SerializeField] Vector3 leverAxis = Vector3.right;
        [SerializeField] float leverDegrees = -100f;
        [SerializeField] float leverSeconds = 0.6f;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip powerOnSound;

        public bool IsOn { get; private set; }
        public event Action PoweredOn;

        public Vector3 InteractPosition => transform.position;

        public InteractPrompt GetPrompt(ZombiePlayer player)
        {
            if (IsOn || player == null || player.IsDowned || player.IsSpectating) return InteractPrompt.Hidden;
            return new InteractPrompt { Visible = true, Label = "Turn On Power", Affordable = true };
        }

        public InteractResult Interact(ZombiePlayer player)
        {
            if (IsOn || player == null || player.IsDowned || player.IsSpectating) return InteractResult.Unavailable;
            IsOn = true;
            if (audioSource != null && powerOnSound != null) audioSource.PlayOneShot(powerOnSound);
            if (lever != null) StartCoroutine(SwingLever());
            PoweredOn?.Invoke();
            return InteractResult.Done;
        }

        IEnumerator SwingLever()
        {
            Quaternion from = lever.localRotation;
            Quaternion to = from * Quaternion.AngleAxis(leverDegrees, leverAxis);
            for (float t = 0f; t < leverSeconds; t += Time.deltaTime)
            {
                lever.localRotation = Quaternion.Slerp(from, to, t / leverSeconds);
                yield return null;
            }
            lever.localRotation = to;
        }
    }
}
