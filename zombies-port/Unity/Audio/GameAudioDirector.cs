// GameAudioDirector.cs
//
// Connects gameplay events to sound aliases, the way the zombies scripts call playsound():
//   rounds (round start / over music, dog rounds), zombies (ambient moans, attack, death), the
//   player's weapon (fire, dry fire, raise and every "sndnt#alias" notetrack in the viewmodel clips,
//   knife), purchases (cha-ching / no cha-ching), bullet hits, the Mystery Box, power-ups, game over,
//   and the map's music loop. Alias names are fields so another map can swap them.

using UnityEngine;

namespace ZombiesPort
{
    public class GameAudioDirector : MonoBehaviour
    {
        [Header("Music")]
        [SerializeField] string musicLoop = "mus_theatre_underscore";
        [SerializeField] float musicVolume = 3f;   // the underscore alias is mixed very low in BO1
        [SerializeField] string roundStart = "mus_zombie_round_start";
        [SerializeField] string roundOver = "mus_zombie_round_over";
        [SerializeField] string dogRoundStart = "mus_zombie_dog_start";
        [SerializeField] string dogRoundEnd = "mus_zombie_dog_end";
        [SerializeField] string gameOver = "mus_zombie_game_over";

        [Header("Zombies")]
        [SerializeField] string zombieAmbient = "zmb_vocals_zombie_ambience";
        [SerializeField] string zombieAttack = "zmb_vocals_zombie_attack";
        [SerializeField] string zombieDeath = "zmb_vocals_zombie_death";
        [SerializeField] string playerSwiped = "evt_player_swiped";
        [SerializeField] Vector2 moanInterval = new Vector2(1.2f, 3f);
        [SerializeField] float moanRange = 18f;

        [Header("Player")]
        [SerializeField] string purchase = "zmb_cha_ching";
        [SerializeField] string denied = "zmb_no_cha_ching";
        [SerializeField] string bulletHit = "prj_bullet_impact_large_flesh";
        [SerializeField] string headshotHit = "prj_bullet_impact_xtreme_flesh";
        [SerializeField] string knifeSwing = "wpn_melee_swing_gear_plr";
        [SerializeField] string knifeHit = "wpn_melee_knife_hit_body";

        [Header("Box and power-ups")]
        [SerializeField] string boxMoved = "zmb_box_poof";
        [SerializeField] string teddyBear = "zmb_box_move";
        [SerializeField] string powerUpGrabbed = "zmb_powerup_grabbed";
        [SerializeField] string nuke = "evt_nuked";
        [SerializeField] string instaKill = "zmb_insta_kill";
        [SerializeField] string maxAmmo = "zmb_full_ammo";

        float _nextMoan;

        void Start()
        {
            var audio = ZombieAudio.Instance;
            if (audio == null) { Debug.LogWarning("GameAudioDirector: no ZombieAudio in the scene."); enabled = false; return; }
            if (!string.IsNullOrEmpty(musicLoop)) audio.PlayMusic(musicLoop, musicVolume);

            var rounds = FindFirstObjectByType<RoundDirector>();
            if (rounds != null)
            {
                rounds.RoundStarted += _ => Play2D(roundStart);
                rounds.RoundEnded += (_, __) => Play2D(roundOver);
                rounds.ZombieKilled += (z, _) => { if (z != null) audio.Play(zombieDeath, z.transform.position); };
                rounds.PlayerAttacked += (z, _) =>
                {
                    if (z != null) audio.Play(zombieAttack, z.transform.position);
                    Play2D(playerSwiped);
                };
            }

            var dogs = FindFirstObjectByType<DogRoundDirector>();
            if (dogs != null)
            {
                dogs.DogRoundStarting += _ => Play2D(dogRoundStart);
                dogs.DogRoundEnded += _ => Play2D(dogRoundEnd);
            }

            var laststand = FindFirstObjectByType<LastStandDirector>();
            if (laststand != null) laststand.GameOver += _ => { audio.StopMusic(); Play2D(gameOver); };

            foreach (var interactor in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
                interactor.Interacted += r =>
                {
                    if (r == InteractResult.Done) Play2D(purchase);
                    else if (r == InteractResult.NotEnoughPoints) Play2D(denied);
                };

            foreach (var weapon in FindObjectsByType<PlayerWeapon>(FindObjectsSortMode.None))
            {
                weapon.ZombieHit += (z, zone) => { if (z != null) audio.Play(zone == HitZone.Head ? headshotHit : bulletHit, z.transform.position + Vector3.up); };
                weapon.Knifed += hit => Play2D(hit ? knifeHit : knifeSwing);
            }

            foreach (var rig in FindObjectsByType<ViewModelRig>(FindObjectsSortMode.None))
                rig.SoundRequested += (weaponId, key) => OnWeaponSound(rig, weaponId, key);

            var box = FindFirstObjectByType<MysteryBoxDirector>();
            if (box != null)
            {
                box.BoxMoved += b => audio.Play(boxMoved, b != null ? b.transform.position : (Vector3?)null);
                box.TeddyBearRolled += b => audio.Play(teddyBear, b != null ? b.transform.position : (Vector3?)null);
            }

            var powerUps = FindFirstObjectByType<PowerUpDirector>();
            if (powerUps != null)
                powerUps.Grabbed += (id, _) =>
                {
                    Play2D(powerUpGrabbed);
                    string name = id.ToString();
                    if (name.Contains("Nuke")) Play2D(nuke);
                    else if (name.Contains("Insta")) Play2D(instaKill);
                    else if (name.Contains("Ammo")) Play2D(maxAmmo);
                };

            _nextMoan = Time.time + 2f;
        }

        void OnWeaponSound(ViewModelRig rig, string weaponId, string key)
        {
            if (string.IsNullOrEmpty(key) || key.StartsWith("knife_")) return; // knife handled by Knifed
            const string notetrack = "sndnt#";
            if (key.StartsWith(notetrack)) { Play2D(key.Substring(notetrack.Length)); return; }
            var lib = rig.Library;
            var entry = lib != null ? lib.Find(weaponId) : null;
            var alias = entry?.Sound(key);
            if (!string.IsNullOrEmpty(alias)) Play2D(alias);
        }

        void Update()
        {
            if (Time.time < _nextMoan) return;
            _nextMoan = Time.time + Random.Range(moanInterval.x, moanInterval.y);

            // One zombie near the listener groans now and then (zombie_moan in the scripts).
            var listener = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            var zombies = FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None);
            if (zombies.Length == 0) return;
            var z = zombies[Random.Range(0, zombies.Length)];
            if (!z.isActiveAndEnabled || (z.transform.position - listener).sqrMagnitude > moanRange * moanRange) return;
            ZombieAudio.Instance.Play(zombieAmbient, z.transform.position + Vector3.up * 1.5f);
        }

        static void Play2D(string alias)
        {
            if (!string.IsNullOrEmpty(alias)) ZombieAudio.Instance?.Play(alias);
        }
    }
}
