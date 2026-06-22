using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using AngelGuardian.Data;

namespace AngelGuardian.Cards
{
    /// <summary>
    /// Defines a single fusion evolution: two source cards combine into a new card.
    /// </summary>
    [Serializable]
    public class FusionDefinition
    {
        public string fusionId;         // e.g. "FUSE-01"
        public string fusionName;       // e.g. "圣光进化·护盾"
        public string sourceCardA;      // e.g. "A-01"
        public string sourceCardB;      // e.g. "A-05"
        public string resultCardId;     // e.g. "F-01"

        [TextArea(2, 4)]
        public string description;

        public CardCategory resultCategory;
        public CardRarity resultRarity;

        [Header("Fusion Bonus Effects")]
        public float damageMultiplier = 1.5f;
        public float durationBonus = 0.5f;
        public float rangeBonus = 0.5f;

        /// <summary>Has this fusion been executed this run?</summary>
        [NonSerialized] public bool hasBeenExecuted = false;
    }

    /// <summary>
    /// Manages the 17 fusion evolutions (FUSE-01 through FUSE-17).
    /// Handles fusion detection, validation, and execution.
    /// </summary>
    public class FusionSystem : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static FusionSystem _instance;
        public static FusionSystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<FusionSystem>();
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
            InitializeFusions();
        }

        #endregion

        #region ─── Runtime State ────────────────────────

        [Header("Fusion Definitions")]
        [SerializeField] private List<FusionDefinition> _fusionDefinitions = new List<FusionDefinition>();

        [Header("Runtime (Read-Only)")]
        [SerializeField] private int _totalFusionsExecuted = 0;
        [SerializeField] private List<string> _executedFusionIds = new List<string>();

        /// <summary>All fusion definitions.</summary>
        public List<FusionDefinition> FusionDefinitions => _fusionDefinitions;

        /// <summary>Total fusions executed this run.</summary>
        public int TotalFusionsExecuted => _totalFusionsExecuted;

        #endregion

        #region ─── Events ───────────────────────────────

        public event Action<FusionDefinition> OnFusionExecuted;

        #endregion

        #region ─── Initialization ────────────────────────

        /// <summary>
        /// Initializes all 17 fusion definitions based on design document.
        /// </summary>
        private void InitializeFusions()
        {
            if (_fusionDefinitions.Count > 0) return;

            _fusionDefinitions = new List<FusionDefinition>
            {
                // FUSE-01: 精神屏障(A-01) + 天使之翼(A-05) → 圣光进化·护盾(F-01)
                new FusionDefinition
                {
                    fusionId = "FUSE-01", fusionName = "圣光进化·护盾",
                    sourceCardA = "A-01", sourceCardB = "A-05", resultCardId = "F-01",
                    description = "护盾吸收量+50%，护盾破碎时释放圣光冲击",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.5f, durationBonus = 0.5f
                },
                // FUSE-02: 圣光弹(A-02) + 圣光弹幕(A-09) → 圣光进化·弹幕(F-02)
                new FusionDefinition
                {
                    fusionId = "FUSE-02", fusionName = "圣光进化·弹幕",
                    sourceCardA = "A-02", sourceCardB = "A-09", resultCardId = "F-02",
                    description = "圣光弹数量+50%，弹幕密度翻倍",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.5f
                },
                // FUSE-03: 圣光之墙(A-03) + 圣光地面(D-01) → 圣光进化·壁垒(F-03)
                new FusionDefinition
                {
                    fusionId = "FUSE-03", fusionName = "圣光进化·壁垒",
                    sourceCardA = "A-03", sourceCardB = "D-01", resultCardId = "F-03",
                    description = "光墙持续时间+50%，光墙附带回复光环",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    durationBonus = 0.5f
                },
                // FUSE-04: 审判之剑(A-04) + 圣雷领域(D-04) → 圣光进化·审判(F-04)
                new FusionDefinition
                {
                    fusionId = "FUSE-04", fusionName = "圣光进化·审判",
                    sourceCardA = "A-04", sourceCardB = "D-04", resultCardId = "F-04",
                    description = "审判之剑落地后生成雷电场域，持续造成伤害",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.6f
                },
                // FUSE-05: 圣光洗礼(A-06) + 回复光环(C-03) → 圣光进化·净化(F-05)
                new FusionDefinition
                {
                    fusionId = "FUSE-05", fusionName = "圣光进化·净化",
                    sourceCardA = "A-06", sourceCardB = "C-03", resultCardId = "F-05",
                    description = "净化范围+50%，同时移除友军debuff和敌人buff",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    rangeBonus = 0.5f
                },
                // FUSE-06: 安抚摇篮曲(B-01) + 怜悯之歌(G-02) → 圣光进化·安眠(F-06)
                new FusionDefinition
                {
                    fusionId = "FUSE-06", fusionName = "圣光进化·安眠",
                    sourceCardA = "B-01", sourceCardB = "G-02", resultCardId = "F-06",
                    description = "沉睡概率+50%，沉睡中的敌人受到的首次伤害翻倍",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 2.0f
                },
                // FUSE-07: 婴灵退散(B-03) + 荆棘之地(D-02) → 圣光进化·震退(F-07)
                new FusionDefinition
                {
                    fusionId = "FUSE-07", fusionName = "圣光进化·震退",
                    sourceCardA = "B-03", sourceCardB = "D-02", resultCardId = "F-07",
                    description = "击退距离+100%，被击退敌人撞墙受到额外伤害",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.5f
                },
                // FUSE-08: 灵魂链接(B-04) + 生命窃取(E-07) → 圣光进化·链接(F-08)
                new FusionDefinition
                {
                    fusionId = "FUSE-08", fusionName = "圣光进化·链接",
                    sourceCardA = "B-04", sourceCardB = "E-07", resultCardId = "F-08",
                    description = "链接伤害转化率+50%，同时获得生命窃取效果",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.3f
                },
                // FUSE-09: 攻击光环(C-01) + 回复光环(C-03) → 圣光进化·战复(F-09)
                new FusionDefinition
                {
                    fusionId = "FUSE-09", fusionName = "圣光进化·战复",
                    sourceCardA = "C-01", sourceCardB = "C-03", resultCardId = "F-09",
                    description = "攻击光环和回复光环效果+50%且叠加生效",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.5f
                },
                // FUSE-10: 防御光环(C-02) + 速度光环(C-04) → 圣光进化·守护(F-10)
                new FusionDefinition
                {
                    fusionId = "FUSE-10", fusionName = "圣光进化·守护",
                    sourceCardA = "C-02", sourceCardB = "C-04", resultCardId = "F-10",
                    description = "防御光环和速度光环效果+50%且获得闪避率加成",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.3f
                },
                // FUSE-11: 荆棘光环(C-05) + 圣火之墙(D-03) → 圣光进化·荆棘(F-11)
                new FusionDefinition
                {
                    fusionId = "FUSE-11", fusionName = "圣光进化·荆棘",
                    sourceCardA = "C-05", sourceCardB = "D-03", resultCardId = "F-11",
                    description = "反伤效果+50%，火墙附带荆棘反伤",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.5f
                },
                // FUSE-12: 恐惧光环(C-06) + 恐惧之触(G-03) → 圣光进化·恐惧(F-12)
                new FusionDefinition
                {
                    fusionId = "FUSE-12", fusionName = "圣光进化·恐惧",
                    sourceCardA = "C-06", sourceCardB = "G-03", resultCardId = "F-12",
                    description = "恐惧效果+100%，恐惧中的敌人受到伤害+50%",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.5f
                },
                // FUSE-13: 幸运光环(C-08) + 致命节奏(E-11) → 圣光进化·幸运(F-13)
                new FusionDefinition
                {
                    fusionId = "FUSE-13", fusionName = "圣光进化·幸运",
                    sourceCardA = "C-08", sourceCardB = "E-11", resultCardId = "F-13",
                    description = "暴击率+20%，暴击时攻速叠加翻倍",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.4f
                },
                // FUSE-14: 圣光地面(D-01) + 冰封之地(D-05) → 圣光进化·冰光(F-14)
                new FusionDefinition
                {
                    fusionId = "FUSE-14", fusionName = "圣光进化·冰光",
                    sourceCardA = "D-01", sourceCardB = "D-05", resultCardId = "F-14",
                    description = "圣光地面与冰封之地融合，兼具回复和冰冻效果",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    durationBonus = 0.5f
                },
                // FUSE-15: 荆棘之地(D-02) + 毒雾沼泽(D-06) → 圣光进化·毒棘(F-15)
                new FusionDefinition
                {
                    fusionId = "FUSE-15", fusionName = "圣光进化·毒棘",
                    sourceCardA = "D-02", sourceCardB = "D-06", resultCardId = "F-15",
                    description = "荆棘和毒雾融合，造成双重持续伤害且可无限叠加中毒",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.8f
                },
                // FUSE-16: 坚韧之心(E-01) + 不屈意志(E-02) → 圣光进化·坚韧(F-16)
                new FusionDefinition
                {
                    fusionId = "FUSE-16", fusionName = "圣光进化·坚韧",
                    sourceCardA = "E-01", sourceCardB = "E-02", resultCardId = "F-16",
                    description = "最大生命+25%，控制抵抗+50%，低血量时减伤翻倍",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.2f
                },
                // FUSE-17: 精准打击(E-04) + 致命节奏(E-11) → 圣光进化·精准(F-17)
                new FusionDefinition
                {
                    fusionId = "FUSE-17", fusionName = "圣光进化·精准",
                    sourceCardA = "E-04", sourceCardB = "E-11", resultCardId = "F-17",
                    description = "暴击率+15%，暴击伤害+40%，暴击时攻速翻倍",
                    resultCategory = CardCategory.F_Growth, resultRarity = CardRarity.SR,
                    damageMultiplier = 1.6f
                }
            };
        }

        #endregion

        #region ─── Public API – Fusion Queries ──────────

        /// <summary>
        /// Finds a fusion definition for the given card pair (order independent).
        /// </summary>
        public FusionDefinition FindFusion(string cardIdA, string cardIdB)
        {
            return _fusionDefinitions.FirstOrDefault(f =>
                (f.sourceCardA == cardIdA && f.sourceCardB == cardIdB) ||
                (f.sourceCardA == cardIdB && f.sourceCardB == cardIdA));
        }

        /// <summary>
        /// Checks if a fusion between two cards is available.
        /// Both source cards must be owned and the fusion must not have been executed.
        /// </summary>
        public bool CheckFusionAvailable(string cardIdA, string cardIdB)
        {
            FusionDefinition fusion = FindFusion(cardIdA, cardIdB);
            if (fusion == null) return false;

            // Can only execute once per run
            if (fusion.hasBeenExecuted) return false;

            // Both source cards must be owned
            CardManager cm = CardManager.Instance;
            if (cm == null) return false;

            return cm.HasCard(cardIdA) && cm.HasCard(cardIdB);
        }

        /// <summary>
        /// Returns all currently available fusions based on owned cards.
        /// </summary>
        public List<FusionDefinition> GetAvailableFusions()
        {
            CardManager cm = CardManager.Instance;
            if (cm == null) return new List<FusionDefinition>();

            return _fusionDefinitions.Where(f =>
                !f.hasBeenExecuted &&
                cm.HasCard(f.sourceCardA) &&
                cm.HasCard(f.sourceCardB)
            ).ToList();
        }

        /// <summary>
        /// Gets a fusion by its fusion ID.
        /// </summary>
        public FusionDefinition GetFusionById(string fusionId)
        {
            return _fusionDefinitions.FirstOrDefault(f => f.fusionId == fusionId);
        }

        #endregion

        #region ─── Public API – Fusion Execution ────────

        /// <summary>
        /// Executes a fusion: removes source cards, creates result card.
        /// </summary>
        /// <param name="cardIdA">First source card ID.</param>
        /// <param name="cardIdB">Second source card ID.</param>
        /// <returns>The resulting CardData, or null if fusion failed.</returns>
        public CardData ExecuteFusion(string cardIdA, string cardIdB)
        {
            FusionDefinition fusion = FindFusion(cardIdA, cardIdB);
            if (fusion == null)
            {
                Debug.LogWarning($"[FusionSystem] No fusion found for: {cardIdA}+{cardIdB}");
                return null;
            }

            if (fusion.hasBeenExecuted)
            {
                Debug.LogWarning($"[FusionSystem] Fusion {fusion.fusionId} already executed this run.");
                return null;
            }

            CardManager cm = CardManager.Instance;
            if (cm == null)
            {
                Debug.LogError("[FusionSystem] CardManager not found.");
                return null;
            }

            if (!cm.HasCard(cardIdA) || !cm.HasCard(cardIdB))
            {
                Debug.LogWarning($"[FusionSystem] Missing source cards for fusion {fusion.fusionId}.");
                return null;
            }

            // Get the result card data from database
            CardData resultData = cm.CardDatabase?.GetCard(fusion.resultCardId);
            if (resultData == null)
            {
                Debug.LogError($"[FusionSystem] Result card {fusion.resultCardId} not found in database.");
                return null;
            }

            // Mark as executed
            fusion.hasBeenExecuted = true;
            _totalFusionsExecuted++;
            _executedFusionIds.Add(fusion.fusionId);

            Debug.Log($"[FusionSystem] ✨ FUSION EXECUTED: {fusion.fusionId} {fusion.fusionName} | " +
                      $"{cardIdA}+{cardIdB} → {fusion.resultCardId}");

            OnFusionExecuted?.Invoke(fusion);

            return resultData;
        }

        /// <summary>
        /// Executes a fusion by fusion ID (instead of source card IDs).
        /// </summary>
        public CardData ExecuteFusionById(string fusionId)
        {
            FusionDefinition fusion = GetFusionById(fusionId);
            if (fusion == null) return null;
            return ExecuteFusion(fusion.sourceCardA, fusion.sourceCardB);
        }

        #endregion

        #region ─── Fusion Bonus Calculation ─────────────

        /// <summary>
        /// Calculates the total fusion bonus multiplier for a specific effect type.
        /// All executed fusions contribute their bonuses cumulatively.
        /// </summary>
        public float GetFusionBonusDamage()
        {
            float bonus = 1f;
            foreach (var fusion in _fusionDefinitions)
            {
                if (fusion.hasBeenExecuted)
                    bonus *= fusion.damageMultiplier;
            }
            return bonus;
        }

        /// <summary>
        /// Gets the total duration bonus from all executed fusions.
        /// </summary>
        public float GetFusionBonusDuration()
        {
            float bonus = 0f;
            foreach (var fusion in _fusionDefinitions)
            {
                if (fusion.hasBeenExecuted)
                    bonus += fusion.durationBonus;
            }
            return bonus;
        }

        /// <summary>
        /// Gets the total range bonus from all executed fusions.
        /// </summary>
        public float GetFusionBonusRange()
        {
            float bonus = 0f;
            foreach (var fusion in _fusionDefinitions)
            {
                if (fusion.hasBeenExecuted)
                    bonus += fusion.rangeBonus;
            }
            return bonus;
        }

        #endregion

        #region ─── Reset ────────────────────────────────

        /// <summary>
        /// Resets all fusion state for a new run.
        /// </summary>
        public void ResetAll()
        {
            foreach (var fusion in _fusionDefinitions)
            {
                fusion.hasBeenExecuted = false;
            }
            _totalFusionsExecuted = 0;
            _executedFusionIds.Clear();
        }

        #endregion

        #region ─── Debug ────────────────────────────────

        [ContextMenu("Log All Fusions")]
        private void LogAllFusions()
        {
            CardManager cm = CardManager.Instance;
            Debug.Log($"[FusionSystem] === Fusion Status (Executed: {_totalFusionsExecuted}) ===");
            foreach (var fusion in _fusionDefinitions)
            {
                bool canFuse = cm != null && cm.HasCard(fusion.sourceCardA) && cm.HasCard(fusion.sourceCardB);
                string status = fusion.hasBeenExecuted ? "EXECUTED" : (canFuse ? "READY" : "LOCKED");
                Debug.Log($"  [{fusion.fusionId}] {fusion.fusionName}: {status} | " +
                          $"{fusion.sourceCardA}+{fusion.sourceCardB} → {fusion.resultCardId}");
            }
        }

        #endregion
    }
}
