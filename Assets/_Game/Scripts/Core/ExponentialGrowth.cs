using System;
using System.Collections.Generic;
using UnityEngine;

namespace AngelGuardian.Core
{
    // ──────────────────────────────────────────────────
    //  Growth Stage Definitions
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 指数成长阶段 (0-6)
    /// 
    /// 阶段0 入门 (1-5级):  属性1.0x, 敌人1.0x, 精神力压力0.5x
    /// 阶段1 适应 (6-10级): 属性1.0x, 敌人1.3x, 精神力压力0.7x
    /// 阶段2 成长 (11-15级): 属性1.1x, 敌人1.6x, 精神力压力0.9x
    /// 阶段3 爆发 (16-20级): 属性1.3x, 敌人2.0x, 精神力压力1.0x
    /// 阶段4 碾压 (21-25级): 属性1.6x, 敌人2.5x, 神力压力1.2x
    /// 阶段5 指数 (26-30级): 属性2.0x, 敌人3.2x, 精神力压力1.4x
    /// 阶段6 顶点 (30+级):  属性总量每10级×1.5, 敌人每波×1.3, 精神力每波×1.2
    /// </summary>
    public enum GrowthStage
    {
        Entry = 0,      // 入门 (1-5级)
        Adapt = 1,      // 适应 (6-10级)
        Growth = 2,      // 成长 (11-15级)
        Burst = 3,       // 爆发 (16-20级)
        Crush = 4,       // 碾压 (21-25级)
        Exponential = 5, // 指数 (26-30级)
        Apex = 6         // 顶点 (30+级)
    }

    // ──────────────────────────────────────────────────
    //  Rarity Weight Definitions
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 掉落稀有度权重结构
    /// 按阶段调整各稀有度的掉落概率权重
    /// </summary>
    [Serializable]
    public struct RarityWeights
    {
        public float commonWeight;      // N级权重
        public float rareWeight;        // R级权重
        public float superRareWeight;   // SR级权重
        public float ssrWeight;         // SSR级权重

        /// <summary>总权重</summary>
        public float TotalWeight => commonWeight + rareWeight + superRareWeight + ssrWeight;

        /// <summary>
        /// 按权重随机抽取稀有度
        /// </summary>
        public string RollRarity()
        {
            float roll = UnityEngine.Random.Range(0f, TotalWeight);

            if (roll < commonWeight) return "N";
            roll -= commonWeight;

            if (roll < rareWeight) return "R";
            roll -= rareWeight;

            if (roll < superRareWeight) return "SR";
            roll -= superRareWeight;

            return "SSR";
        }
    }

    // ──────────────────────────────────────────────────
    //  Soft Limit Definitions
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 软限制参数
    /// 
    /// 弹幕上限: 45颗
    /// 武器上限: 6把
    /// 连携冷却下限: 1秒
    /// 同屏敌人上限: 按平台
    /// 
    /// 性能保护: 超过限制时合并伤害而非丢弃
    /// </summary>
    [Serializable]
    public struct SoftLimits
    {
        [Tooltip("弹幕上限")]
        public int MaxProjectiles;

        [Tooltip("武器上限")]
        public int MaxWeapons;

        [Tooltip("连携冷却下限(秒)")]
        public float minComboCooldown;

        [Tooltip("移动端同屏敌人上限")]
        public int mobileEnemyCap;

        [Tooltip("PC端同屏敌人上限")]
        public int pcEnemyCap;

        /// <summary>当前平台敌人上限</summary>
        public int CurrentPlatformEnemyCap =>
#if UNITY_ANDROID || UNITY_IOS
            mobileEnemyCap;
#else
            pcEnemyCap;
#endif
    }

    // ──────────────────────────────────────────────────
    //  ExponentialGrowth – Core Growth System
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 指数成长系统 —— 游戏核心难度/收益曲线引擎
    /// 
    /// 功能:
    /// - 7阶段定义 (入门→适应→成长→爆发→碾压→指数→顶点)
    /// - 属性增益倍率 / 敌人强度倍率 / 精神力压力倍率
    /// - 掉落稀有度权重 (按阶段)
    /// - 软限制管理 (弹幕/武器/连携/敌人上限)
    /// - 性能保护: 超限合并伤害而非丢弃
    /// </summary>
    public class ExponentialGrowth : MonoBehaviour
    {
        #region ─── Singleton ───────────────────────────

        private static ExponentialGrowth _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static ExponentialGrowth Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[ExponentialGrowth] Instance accessed after application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<ExponentialGrowth>();

                        if (_instance == null)
                        {
                            var go = new GameObject("[ExponentialGrowth]");
                            _instance = go.AddComponent<ExponentialGrowth>();
                            DontDestroyOnLoad(go);
                        }
                        else if (_instance.transform.parent == null)
                        {
                            DontDestroyOnLoad(_instance.gameObject);
                        }
                    }

                    return _instance;
                }
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
            DontDestroyOnLoad(gameObject);

            InitializeStageData();
            InitializeSoftLimits();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            _instance = null;
        }

        #endregion

        #region ─── Inspector ───────────────────────────

        [Header("Stage Configuration")]
        [SerializeField] private GrowthStageData[] _stageData = new GrowthStageData[7];

        [Header("Apex Stage Scaling")]
        [Tooltip("顶点阶段: 属性总量每10级×1.5")]
        [SerializeField] private float _apexAttributeBase = 1.5f;
        [Tooltip("顶点阶段: 敌人每波×1.3")]
        [SerializeField] private float _apexEnemyPerWave = 1.3f;
        [Tooltip("顶点阶段: 精神力每波×1.2")]
        [SerializeField] private float _apexMentalPerWave = 1.2f;

        [Header("Soft Limits")]
        [SerializeField] private SoftLimits _softLimits = new SoftLimits
        {
            MaxProjectiles = 45,
            MaxWeapons = 6,
            minComboCooldown = 1.0f,
            mobileEnemyCap = 300,
            pcEnemyCap = 600
        };

        [Header("Performance Protection")]
        [Tooltip("超过限制时的伤害合并系数")]
        [SerializeField] private float _mergeDamageCoefficient = 0.7f;
        [Tooltip("是否启用性能保护(超限合并而非丢弃)")]
        [SerializeField] private bool _enablePerformanceProtection = true;

        #endregion

        #region ─── Runtime Data ───────────────────────

        /// <summary>当前玩家等级</summary>
        private int _playerLevel = 1;

        /// <summary>当前波次</summary>
        private int _currentWave = 0;

        /// <summary>当前阶段</summary>
        private GrowthStage _currentStage = GrowthStage.Entry;

        /// <summary>当前敌人数量</summary>
        private int _enemyCount = 0;

        /// <summary>累计合并伤害值(性能保护用)</summary>
        private float _mergedDamagePool = 0f;

        #endregion

        #region ─── Properties ──────────────────────────

        /// <summary>当前成长阶段</summary>
        public GrowthStage CurrentStage => _currentStage;

        /// <summary>当前玩家等级</summary>
        public int PlayerLevel => _playerLevel;

        /// <summary>当前波次</summary>
        public int CurrentWave => _currentWave;

        /// <summary>软限制参数</summary>
        public SoftLimits Limits => _softLimits;

        /// <summary>累计合并伤害池</summary>
        public float MergedDamagePool => _mergedDamagePool;

        #endregion

        #region ─── Initialization ──────────────────────

        /// <summary>
        /// 初始化7阶段数据（按规格）
        /// </summary>
        private void InitializeStageData()
        {
            // 如果Inspector中没有配置，使用规格默认值
            if (_stageData == null || _stageData.Length < 7)
            {
                _stageData = new GrowthStageData[7];
            }

            // 阶段0 入门 (1-5级)
            _stageData[0] = new GrowthStageData
            {
                stage = GrowthStage.Entry,
                name = "入门",
                minLevel = 1, maxLevel = 5,
                attributeMultiplier = 1.0f,
                enemyMultiplier = 1.0f,
                mentalPressureMultiplier = 0.5f,
                rarityWeights = new RarityWeights { commonWeight = 80f, rareWeight = 15f, superRareWeight = 4f, ssrWeight = 1f }
            };

            // 阶段1 适应 (6-10级)
            _stageData[1] = new GrowthStageData
            {
                stage = GrowthStage.Adapt,
                name = "适应",
                minLevel = 6, maxLevel = 10,
                attributeMultiplier = 1.0f,
                enemyMultiplier = 1.3f,
                mentalPressureMultiplier = 0.7f,
                rarityWeights = new RarityWeights { commonWeight = 70f, rareWeight = 20f, superRareWeight = 8f, ssrWeight = 2f }
            };

            // 阶段2 成长 (11-15级)
            _stageData[2] = new GrowthStageData
            {
                stage = GrowthStage.Growth,
                name = "成长",
                minLevel = 11, maxLevel = 15,
                attributeMultiplier = 1.1f,
                enemyMultiplier = 1.6f,
                mentalPressureMultiplier = 0.9f,
                rarityWeights = new RarityWeights { commonWeight = 55f, rareWeight = 25f, superRareWeight = 15f, ssrWeight = 5f }
            };

            // 阶段3 爆发 (16-20级)
            _stageData[3] = new GrowthStageData
            {
                stage = GrowthStage.Burst,
                name = "爆发",
                minLevel = 16, maxLevel = 20,
                attributeMultiplier = 1.3f,
                enemyMultiplier = 2.0f,
                mentalPressureMultiplier = 1.0f,
                rarityWeights = new RarityWeights { commonWeight = 45f, rareWeight = 30f, superRareWeight = 18f, ssrWeight = 7f }
            };

            // 阶段4 碾压 (21-25级)
            _stageData[4] = new GrowthStageData
            {
                stage = GrowthStage.Crush,
                name = "碾压",
                minLevel = 21, maxLevel = 25,
                attributeMultiplier = 1.6f,
                enemyMultiplier = 2.5f,
                mentalPressureMultiplier = 1.2f,
                rarityWeights = new RarityWeights { commonWeight = 35f, rareWeight = 30f, superRareWeight = 25f, ssrWeight = 10f }
            };

            // 阶段5 指数 (26-30级)
            _stageData[5] = new GrowthStageData
            {
                stage = GrowthStage.Exponential,
                name = "指数",
                minLevel = 26, maxLevel = 30,
                attributeMultiplier = 2.0f,
                enemyMultiplier = 3.2f,
                mentalPressureMultiplier = 1.4f,
                rarityWeights = new RarityWeights { commonWeight = 25f, rareWeight = 30f, superRareWeight = 30f, ssrWeight = 15f }
            };

            // 阶段6 顶点 (30+级)
            _stageData[6] = new GrowthStageData
            {
                stage = GrowthStage.Apex,
                name = "顶点",
                minLevel = 31, maxLevel = int.MaxValue,
                attributeMultiplier = 2.0f, // 基础，实际按公式计算
                enemyMultiplier = 3.2f,     // 基础，实际按公式计算
                mentalPressureMultiplier = 1.4f, // 基础，实际按公式计算
                rarityWeights = new RarityWeights { commonWeight = 20f, rareWeight = 25f, superRareWeight = 35f, ssrWeight = 20f }
            };

            Debug.Log("[ExponentialGrowth] 7 stages initialized.");
        }

        /// <summary>
        /// 初始化软限制参数（从GameConfig读取或使用默认值）
        /// </summary>
        private void InitializeSoftLimits()
        {
            var config = GameManager.Instance?.Config;
            if (config != null)
            {
                _softLimits.MaxProjectiles = config.MaxProjectiles;
                _softLimits.MaxWeapons = config.MaxWeapons;
                _softLimits.minComboCooldown = config.ComboCdMin;
                _softLimits.pcEnemyCap = config.EnemyCountCap;
            }

            Debug.Log($"[ExponentialGrowth] Soft limits: Projectiles={_softLimits.MaxProjectiles}, " +
                      $"Weapons={_softLimits.MaxWeapons}, ComboCD={_softLimits.minComboCooldown}s, " +
                      $"EnemyCap={_softLimits.CurrentPlatformEnemyCap}");
        }

        #endregion

        #region ─── Public API – Stage & Multipliers ────

        /// <summary>
        /// 根据玩家等级返回当前成长阶段
        /// </summary>
        /// <param name="level">玩家等级</param>
        /// <returns>当前阶段</returns>
        public GrowthStage GetCurrentStage(int level = 0)
        {
            if (level <= 0) level = _playerLevel;

            if (level <= 5) return GrowthStage.Entry;
            if (level <= 10) return GrowthStage.Adapt;
            if (level <= 15) return GrowthStage.Growth;
            if (level <= 20) return GrowthStage.Burst;
            if (level <= 25) return GrowthStage.Crush;
            if (level <= 30) return GrowthStage.Exponential;
            return GrowthStage.Apex;
        }

        /// <summary>
        /// 属性增益倍率
        /// 顶点阶段: 属性总量每10级×1.5
        /// </summary>
        /// <param name="level">玩家等级 (0=使用当前)</param>
        /// <returns>属性增益倍率</returns>
        public float GetAttributeMultiplier(int level = 0)
        {
            if (level <= 0) level = _playerLevel;

            GrowthStage stage = GetCurrentStage(level);

            if (stage == GrowthStage.Apex)
            {
                // 顶点阶段: 属性总量每10级×1.5
                int apexLevels = level - 30;
                int increments = apexLevels / 10;
                float baseMult = _stageData[6].attributeMultiplier;
                return baseMult * Mathf.Pow(_apexAttributeBase, increments);
            }

            return _stageData[(int)stage].attributeMultiplier;
        }

        /// <summary>
        /// 敌人强度倍率
        /// 顶点阶段: 敌人每波×1.3
        /// </summary>
        /// <param name="wave">波次 (0=使用当前)</param>
        /// <returns>敌人强度倍率</returns>
        public float GetEnemyMultiplier(int wave = 0)
        {
            if (wave <= 0) wave = _currentWave;

            GrowthStage stage = GetCurrentStage(_playerLevel);

            if (stage == GrowthStage.Apex)
            {
                // 顶点阶段: 敌人每波×1.3
                float baseMult = _stageData[6].enemyMultiplier;
                return baseMult * Mathf.Pow(_apexEnemyPerWave, wave - 25);
            }

            return _stageData[(int)stage].enemyMultiplier;
        }

        /// <summary>
        /// 精神力压力倍率
        /// 顶点阶段: 精神力每波×1.2
        /// </summary>
        /// <param name="wave">波次 (0=使用当前)</param>
        /// <returns>精神力压力倍率</returns>
        public float GetMentalPressureMultiplier(int wave = 0)
        {
            if (wave <= 0) wave = _currentWave;

            GrowthStage stage = GetCurrentStage(_playerLevel);

            if (stage == GrowthStage.Apex)
            {
                // 顶点阶段: 神力每波×1.2
                float baseMult = _stageData[6].mentalPressureMultiplier;
                return baseMult * Mathf.Pow(_apexMentalPerWave, wave - 25);
            }

            return _stageData[(int)stage].mentalPressureMultiplier;
        }

        /// <summary>
        /// 掉落稀有度权重（按阶段）
        /// </summary>
        /// <param name="level">玩家等级 (0=使用当前)</param>
        /// <returns>稀有度权重结构</returns>
        public RarityWeights GetDropRarityWeights(int level = 0)
        {
            if (level <= 0) level = _playerLevel;

            GrowthStage stage = GetCurrentStage(level);
            return _stageData[(int)stage].rarityWeights;
        }

        #endregion

        #region ─── Public API – Soft Limits ────────────

        /// <summary>
        /// 检查弹幕是否超限
        /// </summary>
        /// <param name="count">当前弹幕数量</param>
        /// <returns>是否超过上限</returns>
        public bool IsProjectileOverLimit(int count)
        {
            return count > _softLimits.MaxProjectiles;
        }

        /// <summary>
        /// 检查武器是否超限
        /// </summary>
        /// <param name="count">当前武器数量</param>
        /// <returns>是否超过上限</returns>
        public bool IsWeaponOverLimit(int count)
        {
            return count > _softLimits.MaxWeapons;
        }

        /// <summary>
        /// 检查敌人是否超限
        /// </summary>
        /// <param name="count">当前敌人数量</param>
        /// <returns>是否超过上限</returns>
        public bool IsEnemyOverLimit(int count)
        {
            return count > _softLimits.CurrentPlatformEnemyCap;
        }

        /// <summary>
        /// 获取当前平台敌人上限
        /// </summary>
        public int GetEnemyCap()
        {
            return _softLimits.CurrentPlatformEnemyCap;
        }

        /// <summary>
        /// 获取弹幕上限
        /// </summary>
        public int GetProjectileCap()
        {
            return _softLimits.MaxProjectiles;
        }

        /// <summary>
        /// 获取武器上限
        /// </summary>
        public int GetWeaponCap()
        {
            return _softLimits.MaxWeapons;
        }

        /// <summary>
        /// 获取连携冷却下限
        /// </summary>
        public float GetMinComboCooldown()
        {
            return _softLimits.minComboCooldown;
        }

        #endregion

        #region ─── Public API – Performance Protection ─

        /// <summary>
        /// 性能保护：超限时合并伤害而非丢弃
        /// 
        /// 当弹幕/敌人超过软限制时:
        /// - 将超限部分的伤害合并到合并池
        /// - 合并池定期释放为一次综合伤害
        /// - 合并系数0.7（避免伤害完全丢失但也不全额）
        /// </summary>
        /// <param name="rawDamage">超限原始伤害</param>
        /// <param name="limitType">限制类型</param>
        public void MergeOverLimitDamage(float rawDamage, string limitType)
        {
            if (!_enablePerformanceProtection) return;

            float mergedDamage = rawDamage * _mergeDamageCoefficient;
            _mergedDamagePool += mergedDamage;

            Debug.Log($"[ExponentialGrowth] Merged over-limit damage: {rawDamage} → {mergedDamage} ({limitType}). " +
                      $"Pool total: {_mergedDamagePool}");
        }

        /// <summary>
        /// 释放合并伤害池
        /// 将池中积累的合并伤害以单次伤害释放给目标
        /// </summary>
        /// <returns>合并伤害总量</returns>
        public float ReleaseMergedDamage()
        {
            float damage = _mergedDamagePool;
            _mergedDamagePool = 0f;

            if (damage > 0f)
            {
                Debug.Log($"[ExponentialGrowth] Released merged damage: {damage}");
            }

            return damage;
        }

        #endregion

        #region ─── Public API – Level Update ───────────

        /// <summary>
        /// 更新玩家等级（触发阶段计算）
        /// </summary>
        /// <param name="newLevel">新等级</param>
        public void SetPlayerLevel(int newLevel)
        {
            int oldLevel = _playerLevel;
            _playerLevel = Mathf.Max(1, newLevel);

            GrowthStage oldStage = _currentStage;
            _currentStage = GetCurrentStage(_playerLevel);

            if (_currentStage != oldStage)
            {
                Debug.Log($"[ExponentialGrowth] Stage transition: {oldStage}({_stageData[(int)oldStage].name}) → " +
                          $"{_currentStage}({_stageData[(int)_currentStage].name}) at level {_playerLevel}");

                OnStageChanged?.Invoke(oldStage, _currentStage);
            }
        }

        /// <summary>
        /// 更新当前波次
        /// </summary>
        /// <param name="wave">波次号</param>
        public void SetCurrentWave(int wave)
        {
            _currentWave = Mathf.Max(0, wave);
        }

        /// <summary>
        /// 更新敌人数量
        /// </summary>
        /// <param name="count">当前敌人数</param>
        public void SetEnemyCount(int count)
        {
            _enemyCount = count;
        }

        #endregion

        #region ─── Events ──────────────────────────────

        /// <summary>阶段变更事件 (旧阶段, 新阶段)</summary>
        public event Action<GrowthStage, GrowthStage> OnStageChanged;

        #endregion

        #region ─── Debug ───────────────────────────────

        [ContextMenu("Log Growth State")]
        private void LogGrowthState()
        {
            Debug.Log($"[ExponentialGrowth] Level: {_playerLevel} | Stage: {_currentStage}({_stageData[(int)_currentStage].name}) | Wave: {_currentWave}\n" +
                      $"  AttributeMult: {GetAttributeMultiplier():F2} | EnemyMult: {GetEnemyMultiplier():F2} | MentalMult: {GetMentalPressureMultiplier():F2}\n" +
                      $"  RarityWeights: {GetDropRarityWeights()}\n" +
                      $"  SoftLimits: Proj={_softLimits.MaxProjectiles}, Weapons={_softLimits.MaxWeapons}, " +
                      $"ComboCD={_softLimits.minComboCooldown}s, EnemyCap={_softLimits.CurrentPlatformEnemyCap}\n" +
                      $"  MergedDamagePool: {_mergedDamagePool:F2}");
        }

        #endregion

        #region ─── Inner Types ─────────────────────────

        /// <summary>
        /// 成长阶段数据
        /// </summary>
        [Serializable]
        public class GrowthStageData
        {
            public GrowthStage stage;
            public string name;
            [Tooltip("最低等级")] public int minLevel;
            [Tooltip("最高等级")] public int maxLevel;
            [Tooltip("属性增益倍率")] public float attributeMultiplier;
            [Tooltip("敌人强度倍率")] public float enemyMultiplier;
            [Tooltip("精神力压力倍率")] public float mentalPressureMultiplier;
            [Tooltip("掉落稀有度权重")] public RarityWeights rarityWeights;

            public override string ToString()
            {
                return $"{name} (Lv{minLevel}-{maxLevel}): Attr×{attributeMultiplier}, Enemy×{enemyMultiplier}, Mental×{mentalPressureMultiplier}";
            }
        }

        #endregion
    }
}
