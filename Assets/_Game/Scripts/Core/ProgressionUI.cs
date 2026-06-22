using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AngelGuardian.Core;

namespace AngelGuardian.Core
{
    // ──────────────────────────────────────────────────
    //  ProgressionUI – Meta Progression User Interface
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Meta Progression UI —— 6项永久升级的交互界面
    /// 
    /// 功能:
    /// - 显示6项永久升级: 初始精神力+20/初始武器+1/幸运基础值+5/卡牌重roll/武器重roll/属性解锁
    /// - 每项显示当前等级和升级消耗
    /// - 升级按钮 + 资源显示(灵魂碎片 + 天使之泪)
    /// - 解锁进度条
    /// </summary>
    public class ProgressionUI : MonoBehaviour
    {
        #region ─── Inspector ───────────────────────────

        [Header("UI References – Resource Display")]
        [SerializeField] private Text _soulShardText;
        [SerializeField] private Text _angelTearText;

        [Header("UI References – Upgrade Items (6)")]
        [SerializeField] private UpgradeItemUI[] _upgradeItems = new UpgradeItemUI[6];

        [Header("UI References – Progress")]
        [SerializeField] private Slider _totalProgressSlider;
        [SerializeField] private Text _totalProgressText;

        [Header("Animation")]
        [SerializeField] private float _upgradeAnimationDuration = 0.5f;
        [SerializeField] private Color _canAffordColor = new Color(0.2f, 0.8f, 1f);     // 可升级=蓝色
        [SerializeField] private Color _cannotAffordColor = new Color(0.5f, 0.5f, 0.5f); // 不可升级=灰色
        [SerializeField] private Color _maxLevelColor = new Color(1f, 0.85f, 0f);        // 满级=金色

        #endregion

        #region ─── Runtime Data ───────────────────────

        /// <summary>MetaProgression数据引用</summary>
        private MetaProgression _metaData;

        /// <summary>6项升级类型定义 (按规格)</summary>
        private readonly MetaProgression.UpgradeType[] _upgradeTypes = new MetaProgression.UpgradeType[6]
        {
            MetaProgression.UpgradeType.InitialMentalBonus,    // 初始精神力+20
            MetaProgression.UpgradeType.InitialWeaponSlot,     // 初始武器+1
            MetaProgression.UpgradeType.BaseLuckBonus,         // 幸运基础值+5
            MetaProgression.UpgradeType.CardRerollCount,       // 卡牌重roll
            MetaProgression.UpgradeType.WeaponRerollFree,      // 武器重roll
            MetaProgression.UpgradeType.ExtraAttributes        // 属性解锁
        };

        /// <summary>升级名称 (中文)</summary>
        private readonly string[] _upgradeNames = new string[6]
        {
            "初始精神力",     // InitialMentalBonus
            "初始武器",       // InitialWeaponSlot
            "幸运基础值",     // BaseLuckBonus
            "卡牌重Roll",    // CardRerollCount
            "武器重Roll",    // WeaponRerollFree
            "属性解锁"       // ExtraAttributes
        };

        /// <summary>升级效果描述</summary>
        private readonly string[] _upgradeDescriptions = new string[6]
        {
            "每级+20精神力起始值",
            "每级+1武器起始槽位",
            "每级+5幸运基础值",
            "每级+1卡牌重Roll次数",
            "每级+1免费武器重Roll",
            "每级+1额外属性点"
        };

        /// <summary>升级图标名称</summary>
        private readonly string[] _upgradeIconNames = new string[6]
        {
            "icon_mental",
            "icon_weapon",
            "icon_luck",
            "icon_card_reroll",
            "icon_weapon_reroll",
            "icon_attribute"
        };

        #endregion

        #region ─── Unity Lifecycle ──────────────────────

        private void Start()
        {
            _metaData = MetaProgression.Instance;
            RefreshAllUI();
        }

        private void OnEnable()
        {
            RefreshAllUI();
        }

        #endregion

        #region ─── Public API ──────────────────────────

        /// <summary>
        /// 刷新所有UI显示
        /// </summary>
        public void RefreshAllUI()
        {
            if (_metaData == null)
            {
                _metaData = MetaProgression.Instance;
                if (_metaData == null) return;
            }

            // 资源显示
            UpdateResourceDisplay();

            // 6项升级
            for (int i = 0; i < 6; i++)
            {
                UpdateUpgradeItem(i);
            }

            // 总进度
            UpdateTotalProgress();
        }

        /// <summary>
        /// 尝试升级指定项目
        /// </summary>
        /// <param name="index">升级项目索引 (0-5)</param>
        public void TryUpgrade(int index)
        {
            if (index < 0 || index >= 6) return;
            if (_metaData == null) return;

            MetaProgression.UpgradeType type = _upgradeTypes[index];
            Vector2Int cost = _metaData.GetUpgradeCost(type);

            // 检查是否可支付
            if (!_metaData.CanAfford(cost))
            {
                Debug.LogWarning($"[ProgressionUI] Cannot afford upgrade: {_upgradeNames[index]} (Cost: {cost.x} shards, {cost.y} tears)");
                return;
            }

            // 支付
            bool spent = _metaData.SpendResources(cost);
            if (!spent) return;

            // 升级
            _metaData.LevelUpUpgrade(type);

            // 刷新UI
            RefreshAllUI();

            Debug.Log($"[ProgressionUI] Upgrade success: {_upgradeNames[index]} → Level {_metaData.GetUpgradeLevel(type)}");
        }

        /// <summary>
        /// 获取指定升级项目的数据
        /// </summary>
        public UpgradeItemData GetUpgradeItemData(int index)
        {
            if (index < 0 || index >= 6 || _metaData == null)
                return default;

            MetaProgression.UpgradeType type = _upgradeTypes[index];
            int currentLevel = _metaData.GetUpgradeLevel(type);
            Vector2Int cost = _metaData.GetUpgradeCost(type);
            bool canAfford = _metaData.CanAfford(cost);

            return new UpgradeItemData
            {
                name = _upgradeNames[index],
                description = _upgradeDescriptions[index],
                currentLevel = currentLevel,
                upgradeCost = cost,
                canAfford = canAfford,
                iconName = _upgradeIconNames[index]
            };
        }

        #endregion

        #region ─── Internal – Resource Display ─────────

        /// <summary>
        /// 更新资源显示
        /// </summary>
        private void UpdateResourceDisplay()
        {
            if (_soulShardText != null)
                _soulShardText.text = $"灵魂碎片: {_metaData.SoulShards}";

            if (_angelTearText != null)
                _angelTearText.text = $"天使之泪: {_metaData.AngelTears}";
        }

        #endregion

        #region ─── Internal – Upgrade Items ────────────

        /// <summary>
        /// 更新单个升级项目UI
        /// </summary>
        private void UpdateUpgradeItem(int index)
        {
            if (index >= _upgradeItems.Length || _upgradeItems[index] == null) return;

            UpgradeItemUI itemUI = _upgradeItems[index];
            UpgradeItemData data = GetUpgradeItemData(index);

            // 名称
            if (itemUI.nameText != null)
                itemUI.nameText.text = data.name;

            // 描述
            if (itemUI.descriptionText != null)
                itemUI.descriptionText.text = data.description;

            // 当前等级
            if (itemUI.levelText != null)
                itemUI.levelText.text = $"Lv.{data.currentLevel}";

            // 升级消耗
            if (itemUI.costText != null)
                itemUI.costText.text = data.upgradeCost.y > 0
                    ? $"{data.upgradeCost.x} 碎片 + {data.upgradeCost.y} 之泪"
                    : $"{data.upgradeCost.x} 碎片";

            // 按钮状态颜色
            if (itemUI.upgradeButton != null)
            {
                ColorBlock colors = itemUI.upgradeButton.colors;
                if (data.currentLevel >= 10) // 满级假设10
                {
                    colors.normalColor = _maxLevelColor;
                    itemUI.upgradeButton.interactable = false;
                }
                else if (data.canAfford)
                {
                    colors.normalColor = _canAffordColor;
                    itemUI.upgradeButton.interactable = true;
                }
                else
                {
                    colors.normalColor = _cannotAffordColor;
                    itemUI.upgradeButton.interactable = false;
                }
                itemUI.upgradeButton.colors = colors;
            }

            // 等级进度条
            if (itemUI.levelSlider != null)
            {
                itemUI.levelSlider.value = data.currentLevel / 10f; // 满级=10
            }
        }

        #endregion

        #region ─── Internal – Total Progress ───────────

        /// <summary>
        /// 更新总进度显示
        /// 6项各10级 → 总60级
        /// </summary>
        private void UpdateTotalProgress()
        {
            int totalLevels = 0;
            int maxTotalLevels = 60; // 6项×10级

            for (int i = 0; i < 6; i++)
            {
                totalLevels += _metaData.GetUpgradeLevel(_upgradeTypes[i]);
            }

            float progress = maxTotalLevels > 0 ? totalLevels / (float)maxTotalLevels : 0f;

            if (_totalProgressSlider != null)
                _totalProgressSlider.value = progress;

            if (_totalProgressText != null)
                _totalProgressText.text = $"总进度: {totalLevels}/{maxTotalLevels} ({progress:P0})";
        }

        #endregion

        #region ─── Inner Types ─────────────────────────

        /// <summary>
        /// 升级项目UI引用结构
        /// </summary>
        [Serializable]
        public class UpgradeItemUI
        {
            [Tooltip("名称文本")] public Text nameText;
            [Tooltip("描述文本")] public Text descriptionText;
            [Tooltip("等级文本")] public Text levelText;
            [Tooltip("消耗文本")] public Text costText;
            [Tooltip("升级按钮")] public Button upgradeButton;
            [Tooltip("等级进度条")] public Slider levelSlider;
            [Tooltip("图标")] public Image iconImage;
        }

        /// <summary>
        /// 升级项目数据结构
        /// </summary>
        public struct UpgradeItemData
        {
            public string name;
            public string description;
            public int currentLevel;
            public Vector2Int upgradeCost;   // x=灵魂碎片, y=天使之泪
            public bool canAfford;
            public string iconName;
        }

        #endregion
    }
}
