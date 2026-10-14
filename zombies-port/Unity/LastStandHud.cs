// LastStandHud.cs
//
// What the player sees for health and last stand:
//   Hurt        : a red screen tint that deepens as health drops, and pulses at 20% or below
//                 (BO1's red flashing overlay, healthOverlayCutoff 0.2)
//   Downed      : the screen darkens, then fades toward black over the second half of the
//                 45 s bleed-out (VisionSetLastStand "zombie_death", delay * 0.5), with a
//                 bleed-out bar and a revive bar when someone is helping
//   Reviving    : a progress bar on the reviver's screen (&"GAME_REVIVING")
//   Solo revive : "Reviving in N" (&"ZOMBIE_REVIVING_SOLO")
//   Bled out    : a note that they'll be back next round
//   Game over   : "GAME OVER / You Survived N Rounds" and a Play Again button
//
// Mobile: full-screen overlays are switched off entirely when invisible (not just alpha 0),
// because a full-screen transparent quad still costs fill rate on every frame. Text is only
// rewritten when it changes.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(Canvas))]
    public class LastStandHud : MonoBehaviour
    {
        [SerializeField] PlayerHealth player;
        [SerializeField] LastStandDirector director;

        [Header("Overlays (full screen)")]
        [SerializeField] Image damageOverlay;
        [SerializeField] float maxDamageAlpha = 0.45f;
        [SerializeField] Image downedOverlay;

        [Header("Last stand")]
        [SerializeField] TMP_Text statusText;
        [SerializeField] Image bleedoutBar;   // Image Type = Filled
        [SerializeField] Image reviveBar;     // Image Type = Filled

        [Header("Game over")]
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] TMP_Text gameOverTitle;
        [SerializeField] TMP_Text survivedText;
        [SerializeField] Button playAgainButton;

        int _lastStatusKey = int.MinValue;

        void Awake()
        {
            // Overlays and text must never swallow touches meant for the controls underneath.
            if (damageOverlay != null) damageOverlay.raycastTarget = false;
            if (downedOverlay != null) downedOverlay.raycastTarget = false;
            if (statusText != null) statusText.raycastTarget = false;
            if (bleedoutBar != null) bleedoutBar.raycastTarget = false;
            if (reviveBar != null) reviveBar.raycastTarget = false;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerHealth>();
            if (director == null) director = FindFirstObjectByType<LastStandDirector>();
            if (director != null) director.GameOver += ShowGameOver;
            if (playAgainButton != null && director != null) playAgainButton.onClick.AddListener(director.PlayAgain);
        }

        void OnDestroy()
        {
            if (director != null) director.GameOver -= ShowGameOver;
            if (playAgainButton != null && director != null) playAgainButton.onClick.RemoveListener(director.PlayAgain);
        }

        void LateUpdate()
        {
            if (player == null || player.Vitals == null) return;
            var v = player.Vitals;

            // ---- Hurt tint ----
            float damageAlpha = 0f;
            if (v.State == LifeState.Alive && v.HealthFraction < 1f)
            {
                damageAlpha = (1f - v.HealthFraction) * maxDamageAlpha;
                if (v.IsVeryHurt) damageAlpha *= 0.7f + 0.3f * Mathf.Sin(Time.time * 8f); // flashing overlay
            }
            SetOverlay(damageOverlay, damageAlpha);

            // ---- Downed darkening ----
            float downedAlpha = 0f;
            if (v.State == LifeState.Downed)
            {
                float total = v.Config.BleedoutSeconds;
                float half = total * 0.5f;
                downedAlpha = v.SelfReviving || v.BleedoutRemaining > half
                    ? 0.35f
                    : Mathf.Lerp(0.85f, 0.35f, v.BleedoutRemaining / half);
            }
            else if (v.State == LifeState.Spectating)
            {
                downedAlpha = 0.5f;
            }
            SetOverlay(downedOverlay, downedAlpha);

            // ---- Bars ----
            bool downed = v.State == LifeState.Downed;
            SetBar(bleedoutBar, downed && !v.SelfReviving, v.BleedoutRemaining / v.Config.BleedoutSeconds);

            float revive = 0f;
            bool showRevive = false;
            if (downed && (v.BeingRevived || v.SelfReviving)) { revive = v.ReviveProgress; showRevive = true; }
            else if (player.RevivingTarget != null) { revive = player.RevivingTarget.Vitals.ReviveProgress; showRevive = true; }
            SetBar(reviveBar, showRevive, revive);

            // ---- Status text (rewritten only when the key changes) ----
            int key;
            string text;
            if (director != null && director.IsGameOver) { key = 0; text = ""; }
            else if (v.State == LifeState.Spectating) { key = 1; text = "You bled out. You'll be back next round."; }
            else if (downed && v.SelfReviving)
            {
                int secs = Mathf.CeilToInt(v.Config.SoloSelfReviveSeconds * (1f - v.ReviveProgress));
                key = 100 + secs; text = "Reviving in " + secs;
            }
            else if (downed && v.BeingRevived) { key = 2; text = "Being revived..."; }
            else if (downed)
            {
                int secs = Mathf.CeilToInt(v.BleedoutRemaining);
                key = 1000 + secs; text = "You're down! Bleeding out: " + secs;
            }
            else if (player.RevivingTarget != null) { key = 3; text = "Reviving..."; }
            else { key = 4; text = ""; }

            if (key != _lastStatusKey && statusText != null)
            {
                _lastStatusKey = key;
                statusText.text = text;
                statusText.enabled = text.Length > 0;
            }
        }

        void ShowGameOver(int roundReached)
        {
            if (gameOverPanel == null) return;
            gameOverPanel.SetActive(true);
            if (gameOverTitle != null) gameOverTitle.text = "GAME OVER";
            // &"ZOMBIE_SURVIVED_ROUND" / &"ZOMBIE_SURVIVED_ROUNDS"
            if (survivedText != null)
                survivedText.text = roundReached == 1 ? "You Survived 1 Round" : "You Survived " + roundReached + " Rounds";
        }

        static void SetOverlay(Image img, float alpha)
        {
            if (img == null) return;
            bool on = alpha > 0.01f;
            if (img.enabled != on) img.enabled = on;
            if (!on) return;
            var c = img.color;
            if (!Mathf.Approximately(c.a, alpha)) { c.a = alpha; img.color = c; }
        }

        static void SetBar(Image bar, bool visible, float fill)
        {
            if (bar == null) return;
            if (bar.enabled != visible) bar.enabled = visible;
            if (visible) bar.fillAmount = Mathf.Clamp01(fill);
        }
    }
}
