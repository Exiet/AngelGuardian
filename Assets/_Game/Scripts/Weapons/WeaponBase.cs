using UnityEngine;
using System;

namespace AngelGuardian.Weapons
{
    /// <summary>
    /// Weapon rarity determines combo weight and base stat multipliers.
    /// </summary>
    public enum WeaponRarity
    {
        Normal = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
        Mythic = 4
    }

    /// <summary>
    /// Weapon type determines attack behavior.
    /// </summary>
    public enum WeaponType
    {
        Melee,
        Ranged,
        AOE
    }

    /// <summary>
    /// Growth stage for a weapon's evolution.
    /// </summary>
    public enum GrowthStage
    {
        None,           // 尚未进化
        KillEvolved,    // 杀敌进化 (100 kills → +50% dmg, rarity+1)
        ComboTempered,  // 连携淬炼 (10 combos → +30% combo effect)
        LimitBroken,    // 极限突破 (10k dmg → exclusive passive)
        Ultimate        // 完全进化
    }

    /// <summary>
    /// Base class for all weapons in Angel Guardian.
    /// Can be used as ScriptableObject or attached as MonoBehaviour.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "AngelGuardian/Weapon")]
    public class WeaponBase : ScriptableObject
    {
        #region ─── Core Identity ───────────────────────

        [Header("Identity")]
        public string weaponId;
        public string weaponName;
        public WeaponType type;
        public WeaponRarity rarity;

        [TextArea(2, 4)]
        public string description;

        #endregion

        #region ─── Combat Stats ─────────────────────────

        [Header("Combat")]
        [Tooltip("Base damage per hit / tick")]
        public float baseDamage = 10f;

        [Tooltip("Seconds between attacks")]
        public float attackInterval = 1f;

        [Tooltip("Projectile travel speed (0 for instant/AOE)")]
        public float projectileSpeed = 30f;

        [Tooltip("Number of projectiles per attack")]
        public int projectileCount = 1;

        [Tooltip("Number of enemies a projectile can pierce (0 = no pierce)")]
        public int pierceCount = 0;

        [Tooltip("Attack range for melee / AOE radius")]
        public float attackRange = 3f;

        [Tooltip("Optional special parameters as JSON")]
        public string specialParams;

        #endregion

        #region ─── Buff Tags ────────────────────────────

        [Header("Tags")]
        [Tooltip("Buff tags used for combo detection and card synergy")]
        public string[] buffTags = new string[0];

        #endregion

        #region ─── Runtime State (non-persistent) ──────

        [Header("Runtime (Read-Only)")]
        [SerializeField, ReadOnly] private int _totalKills = 0;
        [SerializeField, ReadOnly] private int _comboTriggerCount = 0;
        [SerializeField, ReadOnly] private float _totalDamageDealt = 0f;
        [SerializeField, ReadOnly] private GrowthStage _growthStage = GrowthStage.None;

        /// <summary>Total kills accumulated by this weapon in the current run.</summary>
        public int TotalKills => _totalKills;

        /// <summary>Number of combos triggered that involved this weapon.</summary>
        public int ComboTriggerCount => _comboTriggerCount;

        /// <summary>Total damage dealt this run.</summary>
        public float TotalDamageDealt => _totalDamageDealt;

        /// <summary>Current growth stage.</summary>
        public GrowthStage Growth => _growthStage;

        #endregion

        #region ─── Computed Properties ──────────────────

        /// <summary>
        /// Current effective damage, including growth modifiers.
        /// </summary>
        public float EffectiveDamage
        {
            get
            {
                float dmg = baseDamage;
                if (_growthStage >= GrowthStage.KillEvolved)
                    dmg *= 1.5f; // +50% from kill evolution
                return dmg;
            }
        }

        /// <summary>
        /// Combo weight based on rarity.
        /// Normal=1, Rare=2, Epic=3, Legendary=5, Mythic=7
        /// </summary>
        public int ComboWeight => GetComboWeight();

        /// <summary>
        /// Effective rarity after growth evolutions.
        /// </summary>
        public WeaponRarity EffectiveRarity
        {
            get
            {
                if (_growthStage >= GrowthStage.KillEvolved)
                {
                    int upgraded = (int)rarity + 1;
                    return (WeaponRarity)Mathf.Min(upgraded, (int)WeaponRarity.Mythic);
                }
                return rarity;
            }
        }

        #endregion

        #region ─── Virtual Methods ──────────────────────

        /// <summary>
        /// Execute an attack from a position in a direction.
        /// Override in subclasses for specific weapon behavior.
        /// </summary>
        /// <param name="from">Origin position.</param>
        /// <param name="direction">Normalized attack direction.</param>
        /// <returns>Number of projectiles spawned.</returns>
        public virtual int Attack(Vector3 from, Vector3 direction)
        {
            // Base implementation: spawn projectiles based on projectileCount
            // Actual projectile spawning is handled by WeaponManager / ProjectileSystem
            Debug.Log($"[WeaponBase] {weaponName} attacks from {from} dir {direction}");
            return projectileCount;
        }

        /// <summary>
        /// Returns the combo accumulator weight for this weapon.
        /// Normal=1, Rare=2, Epic=3, Legendary=5, Mythic=7
        /// </summary>
        public virtual int GetComboWeight()
        {
            return rarity switch
            {
                WeaponRarity.Normal => 1,
                WeaponRarity.Rare => 2,
                WeaponRarity.Epic => 3,
                WeaponRarity.Legendary => 5,
                WeaponRarity.Mythic => 7,
                _ => 1
            };
        }

        /// <summary>
        /// Called when this weapon participates in a combo trigger.
        /// </summary>
        public virtual void OnComboParticipated(string comboName)
        {
            _comboTriggerCount++;
            Debug.Log($"[WeaponBase] {weaponName} participated in combo: {comboName} (total: {_comboTriggerCount})");
        }

        /// <summary>
        /// Called when an enemy is killed by this weapon.
        /// </summary>
        public virtual void OnKill()
        {
            _totalKills++;
        }

        /// <summary>
        /// Records damage dealt. Used for growth tracking.
        /// </summary>
        public virtual void RecordDamage(float damage)
        {
            _totalDamageDealt += damage;
        }

        /// <summary>
        /// Resets runtime state for a new run.
        /// </summary>
        public virtual void ResetRuntimeState()
        {
            _totalKills = 0;
            _comboTriggerCount = 0;
            _totalDamageDealt = 0f;
            _growthStage = GrowthStage.None;
        }

        #endregion

        #region ─── Growth Methods ───────────────────────

        /// <summary>
        /// Sets the growth stage. Called by WeaponGrowth system.
        /// </summary>
        public void SetGrowthStage(GrowthStage stage)
        {
            if (stage > _growthStage)
            {
                _growthStage = stage;
                Debug.Log($"[WeaponBase] {weaponName} growth: {stage}");
            }
        }

        /// <summary>
        /// Returns the exclusive passive description for this weapon at Limit Break.
        /// </summary>
        public virtual string GetExclusivePassiveDescription()
        {
            return weaponId switch
            {
                "W01" => "5%概率追加追踪弹",
                "W02" => "穿透+2",
                "W05" => "攻速+15%",
                "W13" => "20%概率额外陨石",
                "W18" => "环绕弹数+4",
                _ => "专属被动（未定义）"
            };
        }

        /// <summary>
        /// Returns the exclusive passive effect value for limit break.
        /// </summary>
        public virtual float GetExclusivePassiveValue()
        {
            return weaponId switch
            {
                "W01" => 0.05f,   // 5%追踪弹概率
                "W02" => 2f,      // 穿透+2
                "W05" => 0.15f,   // 攻速+15%
                "W13" => 0.20f,   // 20%额外陨石
                "W18" => 4f,      // 环绕弹+4
                _ => 0f
            };
        }

        #endregion

        #region ─── Clone / Copy ─────────────────────────

        /// <summary>
        /// Creates a runtime instance copy of this weapon (for per-run state tracking).
        /// </summary>
        public virtual WeaponBase CreateRuntimeInstance()
        {
            var copy = Instantiate(this);
            copy._totalKills = 0;
            copy._comboTriggerCount = 0;
            copy._totalDamageDealt = 0f;
            copy._growthStage = GrowthStage.None;
            return copy;
        }

        #endregion
    }

    /// <summary>
    /// Simple ReadOnly attribute for inspector display.
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}
