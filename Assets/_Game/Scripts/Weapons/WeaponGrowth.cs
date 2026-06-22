using UnityEngine;
using System;
using System.Collections.Generic;
using AngelGuardian.Core;

namespace AngelGuardian.Weapons
{
    /// <summary>
    /// Manages weapon growth and evolution across three dimensions:
    /// 1. Kill Evolution: 100 kills → +50% base damage, rarity +1
    /// 2. Combo Tempering: 10 combo participations → +30% combo effect
    /// 3. Limit Break: >10,000 damage in one run → exclusive passive
    /// </summary>
    public class WeaponGrowth : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static WeaponGrowth _instance;
        public static WeaponGrowth Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<WeaponGrowth>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        #endregion

        #region ─── Growth Thresholds ───��────────────────

        [Header("Kill Evolution")]
        [Tooltip("Kills required to trigger kill evolution")]
        public int killEvolutionThreshold = 100;

        [Header("Combo Tempering")]
        [Tooltip("Combo participations required for combo tempering")]
        public int comboTemperingThreshold = 10;

        [Header("Limit Break")]
        [Tooltip("Damage threshold for limit break")]
        public float limitBreakDamageThreshold = 10000f;

        #endregion

        #region ─── Exclusive Passive Definitions ────────

        /// <summary>
        /// Exclusive passive definitions for each weapon at Limit Break.
        /// </summary>
        [Serializable]
        public class ExclusivePassive
        {
            public string weaponId;
            public string passiveName;
            public string description;
            public float value;
            public string effectType; // "proc_chance", "stat_bonus", "count_bonus"
        }

        [Header("Exclusive Passives")]
        [SerializeField] private List<ExclusivePassive> _exclusivePassives = new List<ExclusivePassive>();

        #endregion

        #region ─── Runtime Tracking ─────────────────────

        /// <summary>Tracks which weapons have achieved kill evolution.</summary>
        private HashSet<string> _killEvolvedWeapons = new HashSet<string>();

        /// <summary>Tracks which weapons have achieved combo tempering.</summary>
        private HashSet<string> _comboTemperedWeapons = new HashSet<string>();

        /// <summary>Tracks which weapons have achieved limit break.</summary>
        private HashSet<string> _limitBrokenWeapons = new HashSet<string>();

        #endregion

        #region ─── Events ───────────────────────────────

        public event Action<WeaponBase, GrowthStage> OnWeaponEvolved;
        public event Action<WeaponBase, ExclusivePassive> OnLimitBreak;

        #endregion

        #region ─── Unity Messages ───────────────────────

        private void Start()
        {
            InitializeExclusivePassives();

            // Subscribe to kill events
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnemyKilled.AddListener(OnEnemyKilled);
                EventBus.Instance.OnComboTriggered.AddListener(OnComboTriggeredGlobal);
            }
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnemyKilled.RemoveListener(OnEnemyKilled);
                EventBus.Instance.OnComboTriggered.RemoveListener(OnComboTriggeredGlobal);
            }
        }

        #endregion

        #region ─── Initialization ────────────────────────

        private void InitializeExclusivePassives()
        {
            if (_exclusivePassives.Count > 0) return;

            _exclusivePassives = new List<ExclusivePassive>
            {
                new ExclusivePassive
                {
                    weaponId = "W01", passiveName = "追踪弹追击",
                    description = "5%概率追加追踪弹",
                    value = 0.05f, effectType = "proc_chance"
                },
                new ExclusivePassive
                {
                    weaponId = "W02", passiveName = "深度穿透",
                    description = "穿透+2",
                    value = 2f, effectType = "stat_bonus"
                },
                new ExclusivePassive
                {
                    weaponId = "W05", passiveName = "疾风连射",
                    description = "攻速+15%",
                    value = 0.15f, effectType = "stat_bonus"
                },
                new ExclusivePassive
                {
                    weaponId = "W13", passiveName = "陨石雨",
                    description = "20%概率额外陨石",
                    value = 0.20f, effectType = "proc_chance"
                },
                new ExclusivePassive
                {
                    weaponId = "W18", passiveName = "风暴增弹",
                    description = "环绕弹数+4",
                    value = 4f, effectType = "count_bonus"
                }
            };
        }

        #endregion

        #region ─── Public API – Growth Checks ───────────

        /// <summary>
        /// Checks and applies growth for a weapon based on its current stats.
        /// Called after kills, combos, or significant damage events.
        /// </summary>
        public void EvaluateGrowth(WeaponBase weapon)
        {
            if (weapon == null) return;

            // Check kill evolution
            if (!_killEvolvedWeapons.Contains(weapon.weaponId) &&
                weapon.TotalKills >= killEvolutionThreshold)
            {
                ApplyKillEvolution(weapon);
            }

            // Check combo tempering
            if (!_comboTemperedWeapons.Contains(weapon.weaponId) &&
                weapon.ComboTriggerCount >= comboTemperingThreshold)
            {
                ApplyComboTempering(weapon);
            }

            // Check limit break
            if (!_limitBrokenWeapons.Contains(weapon.weaponId) &&
                weapon.TotalDamageDealt >= limitBreakDamageThreshold)
            {
                ApplyLimitBreak(weapon);
            }
        }

        /// <summary>
        /// Returns the current growth stage of a weapon.
        /// </summary>
        public GrowthStage GetGrowthStage(WeaponBase weapon)
        {
            if (weapon == null) return GrowthStage.None;

            bool killEvolved = _killEvolvedWeapons.Contains(weapon.weaponId);
            bool comboTempered = _comboTemperedWeapons.Contains(weapon.weaponId);
            bool limitBroken = _limitBrokenWeapons.Contains(weapon.weaponId);

            if (limitBroken && comboTempered && killEvolved) return GrowthStage.Ultimate;
            if (limitBroken) return GrowthStage.LimitBroken;
            if (comboTempered) return GrowthStage.ComboTempered;
            if (killEvolved) return GrowthStage.KillEvolved;
            return GrowthStage.None;
        }

        /// <summary>
        /// Gets the exclusive passive for a weapon if it exists.
        /// </summary>
        public ExclusivePassive GetExclusivePassive(string weaponId)
        {
            return _exclusivePassives.Find(p => p.weaponId == weaponId);
        }

        /// <summary>
        /// Checks if a weapon has a specific growth stage unlocked.
        /// </summary>
        public bool HasGrowthStage(string weaponId, GrowthStage stage)
        {
            return stage switch
            {
                GrowthStage.None => true,
                GrowthStage.KillEvolved => _killEvolvedWeapons.Contains(weaponId),
                GrowthStage.ComboTempered => _comboTemperedWeapons.Contains(weaponId),
                GrowthStage.LimitBroken => _limitBrokenWeapons.Contains(weaponId),
                GrowthStage.Ultimate => _killEvolvedWeapons.Contains(weaponId) &&
                                       _comboTemperedWeapons.Contains(weaponId) &&
                                       _limitBrokenWeapons.Contains(weaponId),
                _ => false
            };
        }

        #endregion

        #region ─── Growth Application ────────────────────

        /// <summary>
        /// Kill Evolution: +50% base damage, rarity +1 level.
        /// </summary>
        private void ApplyKillEvolution(WeaponBase weapon)
        {
            _killEvolvedWeapons.Add(weapon.weaponId);
            weapon.SetGrowthStage(GrowthStage.KillEvolved);

            Debug.Log($"[WeaponGrowth] 🔥 {weapon.weaponName} KILL EVOLUTION! " +
                      $"Dmg +50% (now {weapon.EffectiveDamage:F0}), " +
                      $"Rarity: {weapon.rarity}→{weapon.EffectiveRarity}");

            OnWeaponEvolved?.Invoke(weapon, GrowthStage.KillEvolved);
        }

        /// <summary>
        /// Combo Tempering: +30% combo effect for combos involving this weapon.
        /// </summary>
        private void ApplyComboTempering(WeaponBase weapon)
        {
            _comboTemperedWeapons.Add(weapon.weaponId);
            weapon.SetGrowthStage(GrowthStage.ComboTempered);

            Debug.Log($"[WeaponGrowth] ⚡ {weapon.weaponName} COMBO TEMPERING! " +
                      $"Combo effects +30% for combos involving this weapon.");

            OnWeaponEvolved?.Invoke(weapon, GrowthStage.ComboTempered);
        }

        /// <summary>
        /// Limit Break: Unlocks the weapon's exclusive passive.
        /// </summary>
        private void ApplyLimitBreak(WeaponBase weapon)
        {
            _limitBrokenWeapons.Add(weapon.weaponId);
            weapon.SetGrowthStage(GrowthStage.LimitBroken);

            ExclusivePassive passive = GetExclusivePassive(weapon.weaponId);

            if (passive != null)
            {
                Debug.Log($"[WeaponGrowth] 💎 {weapon.weaponName} LIMIT BREAK! " +
                          $"Exclusive Passive: {passive.passiveName} — {passive.description}");
            }
            else
            {
                Debug.Log($"[WeaponGrowth] 💎 {weapon.weaponName} LIMIT BREAK! " +
                          $"(No exclusive passive defined for {weapon.weaponId})");
            }

            OnWeaponEvolved?.Invoke(weapon, GrowthStage.LimitBroken);
            OnLimitBreak?.Invoke(weapon, passive);
        }

        #endregion

        #region ─── Event Handlers ───────────────────────

        private void OnEnemyKilled(GameObject enemy, Vector3 position)
        {
            // Check all owned weapons for growth evaluation
            WeaponManager wm = WeaponManager.Instance;
            if (wm == null) return;

            var weapons = wm.GetAllWeapons();
            foreach (var weapon in weapons)
            {
                EvaluateGrowth(weapon);
            }
        }

        private void OnComboTriggeredGlobal(string comboName, float duration)
        {
            // Check all owned weapons for growth evaluation
            WeaponManager wm = WeaponManager.Instance;
            if (wm == null) return;

            var weapons = wm.GetAllWeapons();
            foreach (var weapon in weapons)
            {
                EvaluateGrowth(weapon);
            }
        }

        #endregion

        #region ─── Combo Effect Bonus ───────────────────

        /// <summary>
        /// Returns the combo effect multiplier based on how many participating
        /// weapons have combo tempering.
        /// </summary>
        /// <param name="weaponIdA">First weapon in combo.</param>
        /// <param name="weaponIdB">Second weapon in combo.</param>
        /// <returns>Multiplier (1.0 = normal, 1.3 = one tempered, 1.6 = both tempered).</returns>
        public float GetComboEffectMultiplier(string weaponIdA, string weaponIdB)
        {
            float multiplier = 1.0f;

            if (_comboTemperedWeapons.Contains(weaponIdA))
                multiplier += 0.3f;

            if (_comboTemperedWeapons.Contains(weaponIdB))
                multiplier += 0.3f;

            return multiplier;
        }

        #endregion

        #region ─── Damage Tracking ──────────────────────

        /// <summary>
        /// Records damage for a specific weapon. Should be called by the damage system.
        /// </summary>
        public void RecordWeaponDamage(string weaponId, float damage)
        {
            WeaponManager wm = WeaponManager.Instance;
            if (wm == null) return;

            WeaponBase weapon = wm.GetWeaponById(weaponId);
            if (weapon != null)
            {
                weapon.RecordDamage(damage);
                EvaluateGrowth(weapon);
            }
        }

        /// <summary>
        /// Records a kill for a specific weapon.
        /// </summary>
        public void RecordWeaponKill(string weaponId)
        {
            WeaponManager wm = WeaponManager.Instance;
            if (wm == null) return;

            WeaponBase weapon = wm.GetWeaponById(weaponId);
            if (weapon != null)
            {
                weapon.OnKill();
                EvaluateGrowth(weapon);
            }
        }

        #endregion

        #region ─── Reset ────────────────────────────────

        /// <summary>
        /// Resets all growth tracking for a new run.
        /// </summary>
        public void ResetAll()
        {
            _killEvolvedWeapons.Clear();
            _comboTemperedWeapons.Clear();
            _limitBrokenWeapons.Clear();
        }

        #endregion

        #region ─── Debug ────────────────────────────────

        [ContextMenu("Log Growth Status")]
        private void LogGrowthStatus()
        {
            WeaponManager wm = WeaponManager.Instance;
            if (wm == null)
            {
                Debug.Log("[WeaponGrowth] No WeaponManager available.");
                return;
            }

            Debug.Log("[WeaponGrowth] === Growth Status ===");
            foreach (var weapon in wm.GetAllWeapons())
            {
                GrowthStage stage = GetGrowthStage(weapon);
                string passive = "";
                if (_limitBrokenWeapons.Contains(weapon.weaponId))
                {
                    var p = GetExclusivePassive(weapon.weaponId);
                    passive = p != null ? $" | Passive: {p.passiveName}" : "";
                }

                Debug.Log($"  {weapon.weaponName} [{weapon.weaponId}]: " +
                          $"Stage={stage} | " +
                          $"Kills:{weapon.TotalKills}/{killEvolutionThreshold} | " +
                          $"Combos:{weapon.ComboTriggerCount}/{comboTemperingThreshold} | " +
                          $"Dmg:{weapon.TotalDamageDealt:F0}/{limitBreakDamageThreshold}{passive}");
            }
        }

        #endregion
    }
}
