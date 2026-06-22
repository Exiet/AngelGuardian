using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AngelGuardian.Player
{
    /// <summary>
    /// Angel属性系统 —— 管理天使的所有属性（20个可见 + 10个隐藏）
    /// 支持属性修改、加成叠加、指数成长系数、宝箱增益计算
    /// </summary>
    public class AngelAttributes : MonoBehaviour
    {
        #region ─── Visible Attributes (20) ─────────────────

        [Header("=== Visible Attributes ===")]

        [SerializeField] private float _weaponDamage = 20f;
        [SerializeField] private float _attackSpeed = 1.0f;
        [SerializeField] private float _projectileSpeed = 400f;
        [SerializeField] private int _projectileCount = 1;
        [SerializeField] private float _projectileSize = 6f;
        [SerializeField] private int _projectilePierce = 0;
        [SerializeField] private float _cooldownReduction = 0f;
        [SerializeField] private float _critRate = 0.01f;
        [SerializeField] private float _critDamage = 1.5f;
        [SerializeField] private float _frostChance = 0f;
        [SerializeField] private float _burnChance = 0f;
        [SerializeField] private float _tauntChance = 0f;
        [SerializeField] private int _chainCount = 0;
        [SerializeField] private float _aoeRadius = 0f;
        [SerializeField] private float _moveSpeed = 200f;
        [SerializeField] private float _pickupRange = 80f;
        [SerializeField] private float _luck = 0f;
        [SerializeField] private float _expBonus = 0f;
        [SerializeField] private float _comboDmgBonus = 0f;
        [SerializeField] private float _comboCdReduce = 0f;

        #endregion

        #region ─── Hidden Attributes (10) ──────────────────

        [Header("=== Hidden Attributes ===")]

        [SerializeField] private float _actualCooldown = 1.0f;
        [SerializeField] private float _bloodDropRate = 0.05f;
        [SerializeField] private float _bloodHealAmt = 3f;
        [SerializeField] private float _chestDropRate = 0.01f;
        [SerializeField] private float _comboAccumulator = 0f;
        [SerializeField] private float _expGrowthMultiplier = 1.0f;
        [SerializeField] private float _terrainAwakening = 0f;
        [SerializeField] private float _minDPS = 10f;
        [SerializeField] private int _maxProjectiles = 45;
        [SerializeField] private int _maxWeapons = 6;

        #endregion

        #region ─── Bonus Multipliers ────────────────────────

        /// <summary>
        /// 属性加成叠加字典：fieldName → (multiplier, sourceId)
        /// multiplier > 1.0 表示加成，< 1.0 表示减益
        /// </summary>
        private Dictionary<string, List<StatBonus>> _bonuses = new Dictionary<string, List<StatBonus>>();

        /// <summary>
        /// 属性修改器 —— 记录所有通过 ModifyStat 的加减值
        /// </summary>
        private Dictionary<string, float> _modifiers = new Dictionary<string, float>();

        #endregion

        #region ─── Properties ───────────────────────────────

        // Visible properties (return effective value = base × bonuses + modifiers)
        public float WeaponDamage      => GetEffectiveValue(nameof(_weaponDamage),       _weaponDamage);
        public float AttackSpeed       => GetEffectiveValue(nameof(_attackSpeed),        _attackSpeed);
        public float ProjectileSpeed   => GetEffectiveValue(nameof(_projectileSpeed),    _projectileSpeed);
        public int   ProjectileCount   => Mathf.RoundToInt(GetEffectiveValue(nameof(_projectileCount),  _projectileCount));
        public float ProjectileSize    => GetEffectiveValue(nameof(_projectileSize),     _projectileSize);
        public int   ProjectilePierce  => Mathf.RoundToInt(GetEffectiveValue(nameof(_projectilePierce), _projectilePierce));
        public float CooldownReduction => Mathf.Clamp(GetEffectiveValue(nameof(_cooldownReduction), _cooldownReduction), 0f, 0.9f);
        public float CritRate          => Mathf.Clamp(GetEffectiveValue(nameof(_critRate),       _critRate),       0f, 1f);
        public float CritDamage        => GetEffectiveValue(nameof(_critDamage),         _critDamage);
        public float FrostChance       => Mathf.Clamp(GetEffectiveValue(nameof(_frostChance),    _frostChance),    0f, 1f);
        public float BurnChance        => Mathf.Clamp(GetEffectiveValue(nameof(_burnChance),     _burnChance),     0f, 1f);
        public float TauntChance       => Mathf.Clamp(GetEffectiveValue(nameof(_tauntChance),    _tauntChance),    0f, 1f);
        public int   ChainCount        => Mathf.RoundToInt(GetEffectiveValue(nameof(_chainCount),      _chainCount));
        public float AoeRadius         => GetEffectiveValue(nameof(_aoeRadius),          _aoeRadius);
        public float MoveSpeed         => GetEffectiveValue(nameof(_moveSpeed),          _moveSpeed);
        public float PickupRange       => GetEffectiveValue(nameof(_pickupRange),        _pickupRange);
        public float Luck              => GetEffectiveValue(nameof(_luck),               _luck);
        public float ExpBonus          => GetEffectiveValue(nameof(_expBonus),           _expBonus);
        public float ComboDmgBonus     => GetEffectiveValue(nameof(_comboDmgBonus),      _comboDmgBonus);
        public float ComboCdReduce     => GetEffectiveValue(nameof(_comboCdReduce),      _comboCdReduce);

        // Hidden properties
        public float ActualCooldown       => GetEffectiveValue(nameof(_actualCooldown),        _actualCooldown);
        public float BloodDropRate        => GetEffectiveValue(nameof(_bloodDropRate),         _bloodDropRate);
        public float BloodHealAmt         => GetEffectiveValue(nameof(_bloodHealAmt),          _bloodHealAmt);
        public float ChestDropRate        => GetEffectiveValue(nameof(_chestDropRate),         _chestDropRate);
        public float ComboAccumulator     { get => _comboAccumulator; set => _comboAccumulator = value; }
        public float ExpGrowthMultiplier  => GetEffectiveValue(nameof(_expGrowthMultiplier),   _expGrowthMultiplier);
        public float TerrainAwakening     => GetEffectiveValue(nameof(_terrainAwakening),      _terrainAwakening);
        public float MinDPS               => GetEffectiveValue(nameof(_minDPS),                _minDPS);
        public int   MaxProjectiles       => Mathf.RoundToInt(GetEffectiveValue(nameof(_maxProjectiles), _maxProjectiles));
        public int   MaxWeapons           => Mathf.RoundToInt(GetEffectiveValue(nameof(_maxWeapons),     _maxWeapons));

        /// <summary>
        /// 获取原始基础值（不含加成和修改器）
        /// </summary>
        public float GetBaseValue(string fieldName)
        {
            var field = GetType().GetField($"_{fieldName}", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return Convert.ToSingle(field.GetValue(this));
            return 0f;
        }

        /// <summary>
        /// 设置原始基础值
        /// </summary>
        public void SetBaseValue(string fieldName, float value)
        {
            var field = GetType().GetField($"_{fieldName}", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(this, value);
        }

        #endregion

        #region ─── Stat Modification ────────────────────────

        /// <summary>
        /// 直接修改属性值（加减法）。适用于永久属性变更。
        /// </summary>
        /// <param name="fieldName">属性字段名（不带下划线前缀），如 "weaponDamage"</param>
        /// <param name="delta">修改量，正值增加，负值减少</param>
        public void ModifyStat(string fieldName, float delta)
        {
            if (!_modifiers.ContainsKey(fieldName))
                _modifiers[fieldName] = 0f;

            _modifiers[fieldName] += delta;

            Debug.Log($"[AngelAttributes] ModifyStat: {fieldName} += {delta} → modifier = {_modifiers[fieldName]}");
        }

        /// <summary>
        /// 添加属性加成（乘法）。适用于卡牌、Buff等临时加成。
        /// </summary>
        /// <param name="fieldName">属性字段名（不带下划线前缀）</param>
        /// <param name="multiplier">加成倍率，如 1.5 表示 +50%</param>
        /// <param name="sourceId">来源标识，用于后续移除</param>
        public void AddBonus(string fieldName, float multiplier, string sourceId = null)
        {
            if (!_bonuses.ContainsKey(fieldName))
                _bonuses[fieldName] = new List<StatBonus>();

            // 避免重复添加同一来源
            if (!string.IsNullOrEmpty(sourceId))
            {
                _bonuses[fieldName].RemoveAll(b => b.SourceId == sourceId);
            }

            _bonuses[fieldName].Add(new StatBonus { Multiplier = multiplier, SourceId = sourceId });

            Debug.Log($"[AngelAttributes] AddBonus: {fieldName} ×{multiplier} (source: {sourceId ?? "unknown"})");
        }

        /// <summary>
        /// 移除指定来源的所有加成
        /// </summary>
        public void RemoveBonus(string sourceId)
        {
            foreach (var kvp in _bonuses)
            {
                kvp.Value.RemoveAll(b => b.SourceId == sourceId);
            }
        }

        /// <summary>
        /// 移除指定属性的指定来源加成
        /// </summary>
        public void RemoveBonus(string fieldName, string sourceId)
        {
            if (_bonuses.TryGetValue(fieldName, out var list))
            {
                list.RemoveAll(b => b.SourceId == sourceId);
            }
        }

        /// <summary>
        /// 清除所有加成和修改器
        /// </summary>
        public void ResetAllBonuses()
        {
            _bonuses.Clear();
            _modifiers.Clear();
            Debug.Log("[AngelAttributes] All bonuses and modifiers cleared.");
        }

        #endregion

        #region ─── Effective Value Calculation ──────────────

        /// <summary>
        /// 计算属性的有效值 = (baseValue × ∏bonuses) + modifiers
        /// </summary>
        private float GetEffectiveValue(string fieldName, float baseValue)
        {
            float result = baseValue;

            // 应用所有加成倍率（乘法叠加）
            if (_bonuses.TryGetValue(fieldName, out var bonusList))
            {
                foreach (var bonus in bonusList)
                {
                    result *= bonus.Multiplier;
                }
            }

            // 应用修改器（加法）
            if (_modifiers.TryGetValue(fieldName, out var modifier))
            {
                result += modifier;
            }

            return result;
        }

        #endregion

        #region ─── EXP Growth Calculation ───────────────────

        /// <summary>
        /// 计算指数成长系数。
        /// 公式：baseMultiplier × (1 + level × 0.02) × cardBonuses
        /// </summary>
        /// <param name="level">当前等级</param>
        /// <param name="cardBonuses">卡牌提供的额外倍率（默认1.0）</param>
        public float CalculateExpGrowthMultiplier(int level = 1, float cardBonuses = 1.0f)
        {
            float baseMultiplier = Core.GameManager.Instance?.Config?.expGrowthMultiplier ?? 1.0f;
            float levelFactor = 1f + (level - 1) * 0.02f;
            float result = baseMultiplier * levelFactor * cardBonuses * ExpBonus;

            // 更新内部值
            _expGrowthMultiplier = result;

            return result;
        }

        #endregion

        #region ─── Chest Bonus Calculation ──────────────────

        /// <summary>
        /// 根据当前属性总量调整宝箱增益倍率。
        /// 属性总量越高，增益越低（防止无限叠加）。
        /// 公式：baseMultiplier / (1 + totalStatRatio × 0.05)
        /// </summary>
        /// <param name="baseMultiplier">基础倍率</param>
        /// <returns>调整后的倍率</returns>
        public float CalculateChestBonusMultiplier(float baseMultiplier = 1.0f)
        {
            // 计算当前所有可见属性的总量（作为"强度"指标）
            float totalStats =
                WeaponDamage + AttackSpeed * 10f + ProjectileSpeed / 40f +
                ProjectileCount * 5f + ProjectileSize + ProjectilePierce * 3f +
                CooldownReduction * 20f + CritRate * 100f + CritDamage * 2f +
                FrostChance * 10f + BurnChance * 10f + TauntChance * 5f +
                ChainCount * 4f + AoeRadius * 2f + MoveSpeed / 20f +
                PickupRange / 8f + Luck * 5f + ExpBonus * 5f +
                ComboDmgBonus * 3f + ComboCdReduce * 10f;

            // 归一化：假设"平均"属性总量约为100
            float statRatio = totalStats / 100f;

            // 使用对数衰减
            float adjustedMultiplier = baseMultiplier / (1f + statRatio * 0.05f);

            Debug.Log($"[AngelAttributes] Chest bonus: base={baseMultiplier}, totalStats={totalStats:F0}, adjusted={adjustedMultiplier:F3}");

            return Mathf.Max(adjustedMultiplier, 0.1f); // 最低0.1倍
        }

        #endregion

        #region ─── Utility ──────────────────────────────────

        /// <summary>
        /// 获取所有可见属性的字典快照（用于UI显示）
        /// </summary>
        public Dictionary<string, float> GetVisibleStatsSnapshot()
        {
            return new Dictionary<string, float>
            {
                ["weaponDamage"]      = WeaponDamage,
                ["attackSpeed"]       = AttackSpeed,
                ["projectileSpeed"]   = ProjectileSpeed,
                ["projectileCount"]   = ProjectileCount,
                ["projectileSize"]    = ProjectileSize,
                ["projectilePierce"]  = ProjectilePierce,
                ["cooldownReduction"] = CooldownReduction,
                ["critRate"]          = CritRate,
                ["critDamage"]        = CritDamage,
                ["frostChance"]       = FrostChance,
                ["burnChance"]        = BurnChance,
                ["tauntChance"]       = TauntChance,
                ["chainCount"]        = ChainCount,
                ["aoeRadius"]         = AoeRadius,
                ["moveSpeed"]         = MoveSpeed,
                ["pickupRange"]       = PickupRange,
                ["luck"]              = Luck,
                ["expBonus"]          = ExpBonus,
                ["comboDmgBonus"]     = ComboDmgBonus,
                ["comboCdReduce"]     = ComboCdReduce,
            };
        }

        /// <summary>
        /// 获取所有隐藏属性的字典快照（用于调试）
        /// </summary>
        public Dictionary<string, float> GetHiddenStatsSnapshot()
        {
            return new Dictionary<string, float>
            {
                ["actualCooldown"]       = ActualCooldown,
                ["bloodDropRate"]        = BloodDropRate,
                ["bloodHealAmt"]         = BloodHealAmt,
                ["chestDropRate"]        = ChestDropRate,
                ["comboAccumulator"]     = ComboAccumulator,
                ["expGrowthMultiplier"]  = ExpGrowthMultiplier,
                ["terrainAwakening"]     = TerrainAwakening,
                ["minDPS"]               = MinDPS,
                ["maxProjectiles"]       = MaxProjectiles,
                ["maxWeapons"]           = MaxWeapons,
            };
        }

        #endregion

        #region ─── Inner Types ──────────────────────────────

        [Serializable]
        private struct StatBonus
        {
            public float Multiplier;
            public string SourceId;
        }

        #endregion
    }
}
