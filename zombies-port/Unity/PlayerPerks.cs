// PlayerPerks.cs
//
// The perks one player holds, buying (with the drink), and what each perk does.
// Ports from _zombiemode_perks.gsc:
//   vending_trigger_think() : pay, then drink; the perk is only granted when the bottle is
//                             finished, and going down mid-drink loses it (the points are gone too)
//   perk_give_bottle_begin(): no aiming, sprinting, knifing or shooting while drinking
//   give_perk()             : Juggernog sets max health to 160. Quick Revive halves revive time;
//                             in solo it also grants one self-revive (self.lives = 1)
//   perk_think()            : going down loses every perk (Juggernog's health goes back to 100)
//
// The other six (see PerkEffects in Perks.cs for every number and where it comes from):
//   Speed Cola  : reloads take half as long          (PlayerWeapon reads HasSpeedCola)
//   Double Tap  : 33% faster fire                     (PlayerWeapon reads HasDoubleTap)
//   Stamin-Up   : sprint lasts twice as long          (FirstPersonController.Stamina)
//   PhD Flopper : a dive-to-prone explosion in BO1. Touch controls have no dive, so dropping
//                 1.2 m or more and landing sets it off instead: 300 units (7.6 m), 5000 damage
//                 at the center down to 1000 at the edge. The player is never hurt by it.
//   Deadshot    : tighter hip-fire, and aiming down sights snaps to a nearby zombie's head
//                 (PlayerWeapon and FirstPersonController read HasDeadshot)
//   Mule Kick   : a third weapon slot. Losing it (going down) takes the third gun away
//                 (take_additionalprimaryweapon), before the last-stand pistol comes out.

using System;
using System.Collections;
using UnityEngine;

namespace ZombiesPort
{
    [RequireComponent(typeof(ZombiePlayer))]
    public class PlayerPerks : MonoBehaviour
    {
        public PerkSet Perks { get; } = new PerkSet();
        public ZombiePlayer Player { get; private set; }
        public bool IsDrinking => Player.IsDrinkingPerk;

        /// <summary>Raised when the bottle comes out (for the drinking view model and sound).</summary>
        public event Action<PerkId> StartedDrinking;
        /// <summary>PhD Flopper went off here (for explosion VFX and sound).</summary>
        public event Action<Vector3> Flopped;

        public bool HasSpeedCola { get; private set; }
        public bool HasDoubleTap { get; private set; }
        public bool HasDeadshot { get; private set; }
        public bool HasPhdFlopper { get; private set; }
        public bool HasStaminUp { get; private set; }
        public bool HasMuleKick { get; private set; }

        PlayerHealth _health;
        PlayerWeapon _weapon;
        FirstPersonController _controller;
        Coroutine _drink;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        readonly Collider[] _blast = new Collider[48];
        readonly System.Collections.Generic.List<ZombieAgent> _blastHit = new System.Collections.Generic.List<ZombieAgent>(24);
        Transform _flash;
        Coroutine _flashRoutine;

        static ZombieGameState State => GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        static bool Solo => GameBootstrap.Instance == null || GameBootstrap.Instance.Players.Count <= 1;

        void Awake()
        {
            Player = GetComponent<ZombiePlayer>();
            _health = GetComponent<PlayerHealth>();
            _weapon = GetComponent<PlayerWeapon>();
            _controller = GetComponent<FirstPersonController>();
            Perks.Gained += ApplyPerk;
            Perks.Lost += RemovePerk;
        }

        void OnEnable()
        {
            if (_health != null) _health.GoingDown += OnGoingDown;
            if (_controller != null) _controller.Landed += OnLanded;
        }

        void OnDisable()
        {
            if (_health != null) _health.GoingDown -= OnGoingDown;
            if (_controller != null) _controller.Landed -= OnLanded;
        }

        // ---------------- Buying ----------------

        public PerkBuyResult CanBuy(PerkId perk, bool powered)
        {
            var state = State;
            if (Player.Score == null || state == null) return PerkBuyResult.Busy;
            bool busy = Player.IsDowned || Player.IsReviving || Player.IsDrinkingPerk || Player.IsSpectating;
            bool soloLivesUnused = _health != null && _health.Vitals.Lives > 0;
            return Perks.CanBuy(perk, Player.Score.Score, powered, busy, Solo, state.SoloQuickRevive, soloLivesUnused);
        }

        /// <summary>Pays and starts drinking. The perk arrives when the bottle is finished.</summary>
        public PerkBuyResult TryBuy(PerkId perk, bool powered)
        {
            var result = CanBuy(perk, powered);
            if (result != PerkBuyResult.Ok) return result;

            var state = State;
            if (!Player.Score.TrySpend(PerkInfo.Cost(perk, Solo), state.Intermission)) return PerkBuyResult.NotEnoughPoints;
            _drink = StartCoroutine(Drink(perk));
            return PerkBuyResult.Ok;
        }

        IEnumerator Drink(PerkId perk)
        {
            Player.IsDrinkingPerk = true;            // increment_is_drinking(): weapon lowered
            if (_weapon != null && _weapon.Current != null) _weapon.Current.CancelReload();
            StartedDrinking?.Invoke(perk);

            for (float t = 0f; t < PerkInfo.DrinkSeconds; t += Time.deltaTime)
            {
                if (Player.IsDowned) { Player.IsDrinkingPerk = false; _drink = null; yield break; }
                yield return null;
            }

            Player.IsDrinkingPerk = false;
            _drink = null;
            var state = State;
            if (Player.IsDowned || (state != null && state.Intermission)) yield break;
            Perks.Give(perk);
        }

        // ---------------- Effects ----------------

        void ApplyPerk(PerkId perk)
        {
            switch (perk)
            {
                case PerkId.Juggernog:
                    if (_health != null) _health.Vitals.SetMaxHealth(PerkInfo.JuggernogHealth);
                    break;

                case PerkId.QuickRevive:
                    Player.HasQuickRevive = true;    // PlayerHealth halves revive time for this reviver
                    if (Solo && _health != null)
                    {
                        _health.Vitals.Lives = 1;    // self.lives = 1
                        State?.SoloQuickRevive.RecordPurchase();
                    }
                    break;

                case PerkId.SpeedCola: HasSpeedCola = true; break;
                case PerkId.DoubleTap: HasDoubleTap = true; break;
                case PerkId.Deadshot: HasDeadshot = true; break;
                case PerkId.PhdFlopper: HasPhdFlopper = true; break;

                case PerkId.StaminUp:
                    HasStaminUp = true;
                    if (_controller != null) _controller.Stamina.SetLongerSprint(true);
                    break;

                case PerkId.MuleKick:
                    HasMuleKick = true;
                    Player.Weapons.HasMuleKick = true;   // the 3rd slot
                    break;
            }
        }

        void RemovePerk(PerkId perk)
        {
            switch (perk)
            {
                case PerkId.Juggernog:
                    if (_health != null) _health.Vitals.SetMaxHealth(_health.Vitals.Config.MaxHealth); // SetMaxHealth( 100 )
                    break;
                case PerkId.QuickRevive:
                    Player.HasQuickRevive = false;
                    break;
                case PerkId.SpeedCola: HasSpeedCola = false; break;
                case PerkId.DoubleTap: HasDoubleTap = false; break;
                case PerkId.Deadshot: HasDeadshot = false; break;
                case PerkId.PhdFlopper: HasPhdFlopper = false; break;
                case PerkId.StaminUp:
                    HasStaminUp = false;
                    if (_controller != null) _controller.Stamina.SetLongerSprint(false);
                    break;
                case PerkId.MuleKick:
                    HasMuleKick = false;
                    Player.Weapons.LoseMuleKick();       // take_additionalprimaryweapon()
                    break;
            }
        }

        /// <summary>perk_think() "player_downed": a half-drunk bottle is wasted and every perk is lost.</summary>
        void OnGoingDown(PlayerHealth h)
        {
            if (_drink != null) { StopCoroutine(_drink); _drink = null; }
            Player.IsDrinkingPerk = false;           // clear_is_drinking()
            Perks.LoseAll();
        }

        // ---------------- PhD Flopper ----------------

        void OnLanded(float fallMeters)
        {
            if (!HasPhdFlopper || fallMeters < PerkEffects.PhdFlopMinFallMeters) return;
            if (Player.IsDowned || Player.IsSpectating) return;
            Flop(transform.position);
        }

        /// <summary>divetonuke_explode(): RadiusDamage( origin, 300, 5000, 1000, self ). Kills score as usual.</summary>
        void Flop(Vector3 origin)
        {
            float radius = PerkEffects.PhdRadiusUnits * MysteryBoxRules.UnitsToMeters;
            int n = Physics.OverlapSphereNonAlloc(origin, radius, _blast, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            _blastHit.Clear();
            for (int i = 0; i < n; i++)
            {
                var z = _blast[i].GetComponentInParent<ZombieAgent>();
                if (z == null || !z.IsAlive || _blastHit.Contains(z)) continue;
                _blastHit.Add(z);
            }
            // Damage after collecting: a kill can return the zombie to its pool mid-loop.
            foreach (var z in _blastHit)
            {
                if (!z.IsAlive) continue;
                float units = Vector3.Distance(origin, z.transform.position) / MysteryBoxRules.UnitsToMeters;
                int damage = PerkEffects.PhdDamageAt(units);
                if (damage > 0) z.TakeDamage(damage, Player, HitZone.Limb); // splash: no head/torso bonus
            }
            Flopped?.Invoke(origin);
            ShowFlash(origin, radius);
        }

        /// <summary>Placeholder explosion: a purple sphere that grows and vanishes. Replace with a particle effect.</summary>
        void ShowFlash(Vector3 origin, float radius)
        {
            if (_flash == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "PhdFlopFlash";
                Destroy(go.GetComponent<Collider>());
                var block = new MaterialPropertyBlock();
                var color = new Color(0.6f, 0.3f, 0.9f);
                block.SetColor(ColorId, color);
                block.SetColor(BaseColorId, color);
                go.GetComponent<Renderer>().SetPropertyBlock(block);
                _flash = go.transform;
            }
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(Flash(origin, radius));
        }

        IEnumerator Flash(Vector3 origin, float radius)
        {
            _flash.gameObject.SetActive(true);
            _flash.position = origin;
            const float seconds = 0.3f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                _flash.localScale = Vector3.one * (radius * 2f * (t / seconds));
                yield return null;
            }
            _flash.gameObject.SetActive(false);
            _flashRoutine = null;
        }

        void OnDestroy()
        {
            if (_flash != null) Destroy(_flash.gameObject);
        }

        // ---------------- Testing (right-click the component) ----------------

        [ContextMenu("Debug: Give All Perks (ignores the 4-perk limit)")]
        void DebugGiveAll()
        {
            if (!Application.isPlaying) return;
            foreach (PerkId p in Enum.GetValues(typeof(PerkId))) Perks.Give(p);
        }
    }
}
