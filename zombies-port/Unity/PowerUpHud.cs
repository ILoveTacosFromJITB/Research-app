// PowerUpHud.cs
//
// Power-up feedback on screen (powerup_hud_overlay, full_ammo_on_hud, nuke_flash):
//   - An icon for each running 30-second power-up (Insta-Kill, Double Points, Fire Sale). The icon
//     blinks in its last 5 seconds, as in BO1.
//   - A short announcement when anything is grabbed ("Max Ammo!", "Carpenter"...).
//   - The Nuke's white flash: up to 80% over 0.2 s, held, then fading out over 1 s.
//
// The flash overlay is switched off entirely when not flashing, so it costs no fill rate.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(Canvas))]
    public class PowerUpHud : MonoBehaviour
    {
        [SerializeField] PowerUpDirector director;
        [Tooltip("Icons for Insta-Kill, Double Points and Fire Sale, in that order.")]
        [SerializeField] Graphic[] timedIcons = new Graphic[3];
        [SerializeField] TMP_Text announcement;
        [SerializeField] Image nukeFlash;
        [SerializeField] float announcementSeconds = 2f;

        static readonly PowerUpId[] TimedOrder = { PowerUpId.InstaKill, PowerUpId.DoublePoints, PowerUpId.FireSale };

        Coroutine _announce, _flash;

        void Awake()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            if (announcement != null) announcement.enabled = false;
            if (nukeFlash != null) nukeFlash.enabled = false;
            foreach (var icon in timedIcons) if (icon != null) icon.gameObject.SetActive(false);
        }

        void Start()
        {
            if (director == null) director = FindFirstObjectByType<PowerUpDirector>();
            if (director == null) return;
            director.Grabbed += OnGrabbed;
            director.NukeFlash += OnNuke;
        }

        void OnDestroy()
        {
            if (director == null) return;
            director.Grabbed -= OnGrabbed;
            director.NukeFlash -= OnNuke;
        }

        void LateUpdate()
        {
            if (director == null) return;
            for (int i = 0; i < timedIcons.Length && i < TimedOrder.Length; i++)
            {
                var icon = timedIcons[i];
                if (icon == null) continue;
                float left = director.Timed.Remaining(TimedOrder[i]);
                bool on = left > 0f;
                if (icon.gameObject.activeSelf != on) icon.gameObject.SetActive(on);
                if (!on) continue;

                // Power_up_hud(): blink when under 5 seconds.
                float alpha = left < 5f ? (Mathf.Sin(Time.time * 12f) > 0f ? 1f : 0.25f) : 1f;
                var c = icon.color;
                if (!Mathf.Approximately(c.a, alpha)) { c.a = alpha; icon.color = c; }
            }
        }

        void OnGrabbed(PowerUpId id, ZombiePlayer player)
        {
            if (announcement == null) return;
            if (_announce != null) StopCoroutine(_announce);
            _announce = StartCoroutine(Announce(PowerUpInfo.DisplayName(id)));
        }

        IEnumerator Announce(string text)
        {
            announcement.text = text;
            announcement.enabled = true;
            var c = announcement.color;
            for (float t = 0f; t < announcementSeconds; t += Time.deltaTime)
            {
                c.a = 1f - Mathf.Clamp01((t - announcementSeconds * 0.6f) / (announcementSeconds * 0.4f));
                announcement.color = c;
                yield return null;
            }
            announcement.enabled = false;
        }

        void OnNuke()
        {
            if (nukeFlash == null) return;
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(Flash());
        }

        /// <summary>nuke_flash(): FadeOverTime(0.2) to 0.8, wait 0.5, FadeOverTime(1.0) to 0.</summary>
        IEnumerator Flash()
        {
            nukeFlash.enabled = true;
            var c = Color.white;
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) { c.a = 0.8f * t / 0.2f; nukeFlash.color = c; yield return null; }
            c.a = 0.8f; nukeFlash.color = c;
            yield return new WaitForSecondsRealtime(0.3f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { c.a = 0.8f * (1f - t); nukeFlash.color = c; yield return null; }
            nukeFlash.enabled = false;
        }
    }
}
