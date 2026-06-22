using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AngelGuardian.Core;
using AngelGuardian.Data;

namespace AngelGuardian.Core
{
    // ──────────────────────────────────────────────────
    //  Collection Filter/Sort Enums
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 图鉴筛选方式
    /// </summary>
    public enum CollectionFilter
    {
        All,            // 全部
        ByCategory,     // 按类别 (A-I)
        UnlockedOnly,   // 仅已解锁
        LockedOnly      // 仅未解锁
    }

    /// <summary>
    /// 图鉴排序方式
    /// </summary>
    public enum CollectionSort
    {
        ByID,           // 按ID
        ByName,         // 按名称
        ByRarity        // 按稀有度
    }

    // ──────────────────────────────────────────────────
    //  CollectionUI – Card & Weapon Collection Interface
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 图鉴系统UI —— 卡牌图鉴(70张) + 武器图鉴(18把)
    /// 
    /// 功能:
    /// - 卡牌图鉴: 70张，已解锁/未解锁(🔒)网格显示
    /// - 筛选: 按类别(A-I)
    /// - 排序: 按ID/名称/稀有度
    /// - 点击查看详情
    /// - 武器图鉴: 18把，含成长阶段展示
    /// - 收集进度: 已解锁/总数
    /// </summary>
    public class CollectionUI : MonoBehaviour
    {
        #region ─── Inspector ───────────────────────────

        [Header("Data References")]
        [SerializeField] private CardDatabase _cardDatabase;
        [SerializeField] private WeaponDatabase _weaponDatabase;

        [Header("UI – Card Collection")]
        [SerializeField] private Transform _cardGridContainer;
        [SerializeField] private GameObject _cardCellPrefab;
        [SerializeField] private int _cardsPerRow = 7;

        [Header("UI – Weapon Collection")]
        [SerializeField] private Transform _weaponGridContainer;
        [SerializeField] private GameObject _weaponCellPrefab;
        [SerializeField] private int _weaponsPerRow = 3;

        [Header("UI – Detail Panel")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Text _detailTitleText;
        [SerializeField] private Text _detailDescriptionText;
        [SerializeField] private Text _detailStatsText;
        [SerializeField] private Image _detailIconImage;
        [SerializeField] private Slider _detailGrowthSlider;       // 武器成长阶段展示
        [SerializeField] private Text _detailGrowthStageText;

        [Header("UI – Filter & Sort")]
        [SerializeField] private Dropdown _categoryFilterDropdown;  // 类别筛选(A-I)
        [SerializeField] private Dropdown _sortDropdown;           // 排序方式
        [SerializeField] private Toggle _showUnlockedToggle;       // 显示已解锁
        [SerializeField] private Toggle _showLockedToggle;         // 显示未解锁

        [Header("UI – Progress")]
        [SerializeField] private Slider _cardProgressSlider;
        [SerializeField] private Text _cardProgressText;           // "已解锁42/70"
        [SerializeField] private Slider _weaponProgressSlider;
        [SerializeField] private Text _weaponProgressText;         // "已解锁12/18"
        [SerializeField] private Text _totalProgressText;          // 总进度

        [Header("UI – Tabs")]
        [SerializeField] private Button _cardTabButton;
        [SerializeField] private Button _weaponTabButton;
        [SerializeField] private GameObject _cardCollectionPanel;
        [SerializeField] private GameObject _weaponCollectionPanel;

        [Header("Colors")]
        [SerializeField] private Color _unlockedColor = new Color(1f, 1f, 1f);     // 已解锁=白色
        [SerializeField] private Color _lockedColor = new Color(0.3f, 0.3f, 0.3f);  // 未解锁=灰色
        [SerializeField] private Color _lockIconColor = new Color(0.8f, 0.8f, 0f);   // 锁图标=暗金色

        #endregion

        #region ─── Runtime Data ───────────────────────

        /// <summary>卡牌解锁记录 (cardId → 是否解锁)</summary>
        private Dictionary<string, bool> _cardUnlockStatus = new Dictionary<string, bool>();

        /// <summary>武器解锁记录 (weaponId → 是否解锁)</summary>
        private Dictionary<string, bool> _weaponUnlockStatus = new Dictionary<string, bool>();

        /// <summary>当前筛选类别</summary>
        private CardCategory _currentCategoryFilter = CardCategory.A_Attack;

        /// <summary>当前筛选模式</summary>
        private CollectionFilter _currentFilter = CollectionFilter.All;

        /// <summary>当前排序方式</summary>
        private CollectionSort _currentSort = CollectionSort.ByID;

        /// <summary>当前活跃标签 (卡牌/武器)</summary>
        private bool _showingCards = true;

        /// <summary>实例化的卡牌格子UI列表</summary>
        private List<GameObject> _cardCells = new List<GameObject>();

        /// <summary>实例化的武器格子UI列表</summary>
        private List<GameObject> _weaponCells = new List<GameObject>();

        /// <summary>卡牌总数</summary>
        private const int TOTAL_CARDS = 70;

        /// <summary>武器总数</summary>
        private const int TOTAL_WEAPONS = 18;

        #endregion

        #region ─── Unity Lifecycle ──────────────────────

        private void Start()
        {
            InitializeDatabases();
            InitializeUnlockStatus();
            InitializeDropdowns();
            SetupTabButtons();
            RefreshAllUI();
        }

        private void OnEnable()
        {
            RefreshAllUI();
        }

        #endregion

        #region ─── Initialization ──────────────────────

        /// <summary>
        /// 初始化数据库引用
        /// </summary>
        private void InitializeDatabases()
        {
            if (_cardDatabase == null)
                _cardDatabase = Resources.Load<CardDatabase>("CardDatabase");

            if (_weaponDatabase == null)
                _weaponDatabase = Resources.Load<WeaponDatabase>("WeaponDatabase");
        }

        /// <summary>
        /// 初始化解锁状态
        /// 从PlayerPrefs读取，未记录的默认为锁定
        /// </summary>
        private void InitializeUnlockStatus()
        {
            // 卡牌解锁状态
            _cardUnlockStatus.Clear();
            if (_cardDatabase != null)
            {
                foreach (CardData card in _cardDatabase.cards)
                {
                    string key = GetCardUnlockKey(card.cardId);
                    _cardUnlockStatus[card.cardId] = PlayerPrefs.GetInt(key, 0) == 1;
                }
            }

            // 武器解锁状态
            _weaponUnlockStatus.Clear();
            if (_weaponDatabase != null)
            {
                foreach (WeaponData weapon in _weaponDatabase.weapons)
                {
                    string key = GetWeaponUnlockKey(weapon.weaponId);
                    _weaponUnlockStatus[weapon.weaponId] = PlayerPrefs.GetInt(key, 0) == 1;
                }
            }

            Debug.Log($"[CollectionUI] Unlock status loaded: " +
                      $"Cards {CountUnlockedCards()}/{TOTAL_CARDS}, Weapons {CountUnlockedWeapons()}/{TOTAL_WEAPONS}");
        }

        /// <summary>
        /// 初始化下拉菜单
        /// </summary>
        private void InitializeDropdowns()
        {
            // 类别筛选 (A-I + 全部)
            if (_categoryFilterDropdown != null)
            {
                _categoryFilterDropdown.ClearOptions();
                var options = new List<string>
                {
                    "全部", "A-攻击防御", "B-婴灵控制", "C-光环", "D-地形",
                    "E-被动", "F-成长", "G-情感", "H-连携", "I-地形活化"
                };
                _categoryFilterDropdown.AddOptions(options);
                _categoryFilterDropdown.onValueChanged.AddListener(OnCategoryFilterChanged);
            }

            // 排序方式
            if (_sortDropdown != null)
            {
                _sortDropdown.ClearOptions();
                var options = new List<string> { "按ID", "按名称", "按稀有度" };
                _sortDropdown.AddOptions(options);
                _sortDropdown.onValueChanged.AddListener(OnSortChanged);
            }

            // Toggle
            if (_showUnlockedToggle != null)
                _showUnlockedToggle.onValueChanged.AddListener(OnFilterToggleChanged);

            if (_showLockedToggle != null)
                _showLockedToggle.onValueChanged.AddListener(OnFilterToggleChanged);
        }

        /// <summary>
        /// 设置标签按钮
        /// </summary>
        private void SetupTabButtons()
        {
            if (_cardTabButton != null)
                _cardTabButton.onClick.AddListener(() => SwitchTab(true));

            if (_weaponTabButton != null)
                _weaponTabButton.onClick.AddListener(() => SwitchTab(false));
        }

        #endregion

        #region ─── Public API ──────────────────────────

        /// <summary>
        /// 刷新所有UI显示
        /// </summary>
        public void RefreshAllUI()
        {
            if (_showingCards)
                RefreshCardGrid();
            else
                RefreshWeaponGrid();

            UpdateProgressDisplay();
            HideDetailPanel();
        }

        /// <summary>
        /// 解锁卡牌
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        public void UnlockCard(string cardId)
        {
            if (_cardUnlockStatus.ContainsKey(cardId))
            {
                _cardUnlockStatus[cardId] = true;
                PlayerPrefs.SetInt(GetCardUnlockKey(cardId), 1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 解锁武器
        /// </summary>
        /// <param name="weaponId">武器ID</param>
        public void UnlockWeapon(string weaponId)
        {
            if (_weaponUnlockStatus.ContainsKey(weaponId))
            {
                _weaponUnlockStatus[weaponId] = true;
                PlayerPrefs.SetInt(GetWeaponUnlockKey(weaponId), 1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 检查卡牌是否已解锁
        /// </summary>
        public bool IsCardUnlocked(string cardId)
        {
            return _cardUnlockStatus.TryGetValue(cardId, out bool unlocked) && unlocked;
        }

        /// <summary>
        /// 检查武器是否已解锁
        /// </summary>
        public bool IsWeaponUnlocked(string weaponId)
        {
            return _weaponUnlockStatus.TryGetValue(weaponId, out bool unlocked) && unlocked;
        }

        /// <summary>
        /// 切换卡牌/武器标签
        /// </summary>
        /// <param name="showCards">true=卡牌, false=武器</param>
        public void SwitchTab(bool showCards)
        {
            _showingCards = showCards;

            if (_cardCollectionPanel != null)
                _cardCollectionPanel.SetActive(showCards);

            if (_weaponCollectionPanel != null)
                _weaponCollectionPanel.SetActive(!showCards);

            RefreshAllUI();
        }

        /// <summary>
        /// 获取已解锁卡牌数量
        /// </summary>
        public int CountUnlockedCards()
        {
            int count = 0;
            foreach (bool unlocked in _cardUnlockStatus.Values)
            {
                if (unlocked) count++;
            }
            return count;
        }

        /// <summary>
        /// 获取已解锁武器数量
        /// </summary>
        public int CountUnlockedWeapons()
        {
            int count = 0;
            foreach (bool unlocked in _weaponUnlockStatus.Values)
            {
                if (unlocked) count++;
            }
            return count;
        }

        #endregion

        #region ─── Internal – Card Grid ────────────────

        /// <summary>
        /// 刷新卡牌网格显示
        /// </summary>
        private void RefreshCardGrid()
        {
            // 清除旧格子
            ClearCardCells();

            if (_cardDatabase == null || _cardGridContainer == null || _cardCellPrefab == null) return;

            // 获取筛选后的卡牌列表
            List<CardData> filteredCards = GetFilteredCards();

            // 排序
            SortCards(filteredCards, _currentSort);

            // 创建网格格子
            for (int i = 0; i < filteredCards.Count; i++)
            {
                CardData card = filteredCards[i];
                bool unlocked = IsCardUnlocked(card.cardId);

                GameObject cell = Instantiate(_cardCellPrefab, _cardGridContainer);
                _cardCells.Add(cell);

                // 配置格子UI
                ConfigureCardCell(cell, card, unlocked, i);
            }
        }

        /// <summary>
        /// 配置卡牌格子UI
        /// </summary>
        private void ConfigureCardCell(GameObject cell, CardData card, bool unlocked, int index)
        {
            // 名称文本
            Text nameText = cell.GetComponentInChildren<Text>();
            if (nameText != null)
            {
                if (unlocked)
                {
                    nameText.text = card.cardName;
                    nameText.color = _unlockedColor;
                }
                else
                {
                    nameText.text = "🔒"; // 未解锁显示锁图标
                    nameText.color = _lockIconColor;
                }
            }

            // 背景/图标
            Image bgImage = cell.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = unlocked ? GetRarityColor(card.rarity) : _lockedColor;
            }

            // 稀有度标签
            Text rarityLabel = FindChildText(cell, "RarityLabel");
            if (rarityLabel != null)
            {
                rarityLabel.text = unlocked ? card.rarity.ToString() : "";
            }

            // ID标签
            Text idLabel = FindChildText(cell, "IDLabel");
            if (idLabel != null)
            {
                idLabel.text = unlocked ? card.cardId : "";
            }

            // 点击事件 → 显示详情
            Button cellButton = cell.GetComponent<Button>();
            if (cellButton != null)
            {
                cellButton.onClick.RemoveAllListeners();
                if (unlocked)
                {
                    cellButton.onClick.AddListener(() => ShowCardDetail(card));
                }
                else
                {
                    cellButton.onClick.AddListener(() => ShowLockedDetail(card.cardId));
                }
            }
        }

        /// <summary>
        /// 获取筛选后的卡牌列表
        /// </summary>
        private List<CardData> GetFilteredCards()
        {
            if (_cardDatabase == null) return new List<CardData>();

            List<CardData> cards;

            // 按类别筛选
            if (_currentFilter == CollectionFilter.ByCategory)
            {
                cards = _cardDatabase.GetCardsByCategory(_currentCategoryFilter);
            }
            else
            {
                cards = _cardDatabase.cards;
            }

            // 按解锁状态筛选
            if (_currentFilter == CollectionFilter.UnlockedOnly)
            {
                cards = cards.FindAll(c => IsCardUnlocked(c.cardId));
            }
            else if (_currentFilter == CollectionFilter.LockedOnly)
            {
                cards = cards.FindAll(c => !IsCardUnlocked(c.cardId));
            }

            return cards;
        }

        /// <summary>
        /// 排序卡牌列表
        /// </summary>
        private void SortCards(List<CardData> cards, CollectionSort sortMethod)
        {
            switch (sortMethod)
            {
                case CollectionSort.ByID:
                    cards.Sort((a, b) => string.Compare(a.cardId, b.cardId, StringComparison.Ordinal));
                    break;
                case CollectionSort.ByName:
                    cards.Sort((a, b) => string.Compare(a.cardName, b.cardName, StringComparison.Ordinal));
                    break;
                case CollectionSort.ByRarity:
                    cards.Sort((a, b) => ((int)b.rarity) - ((int)a.rarity)); // 高稀有度先
                    break;
            }
        }

        /// <summary>
        /// 清除卡牌格子
        /// </summary>
        private void ClearCardCells()
        {
            foreach (GameObject cell in _cardCells)
            {
                if (cell != null) Destroy(cell);
            }
            _cardCells.Clear();
        }

        #endregion

        #region ─── Internal – Weapon Grid ──────────────

        /// <summary>
        /// 刷新武器网格显示
        /// </summary>
        private void RefreshWeaponGrid()
        {
            // 清除旧格子
            ClearWeaponCells();

            if (_weaponDatabase == null || _weaponGridContainer == null || _weaponCellPrefab == null) return;

            List<WeaponData> weapons = GetFilteredWeapons();
            SortWeapons(weapons, _currentSort);

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponData weapon = weapons[i];
                bool unlocked = IsWeaponUnlocked(weapon.weaponId);

                GameObject cell = Instantiate(_weaponCellPrefab, _weaponGridContainer);
                _weaponCells.Add(cell);

                ConfigureWeaponCell(cell, weapon, unlocked, i);
            }
        }

        /// <summary>
        /// 配置武器格子UI（含成长阶段展示）
        /// </summary>
        private void ConfigureWeaponCell(GameObject cell, WeaponData weapon, bool unlocked, int index)
        {
            // 名称
            Text nameText = cell.GetComponentInChildren<Text>();
            if (nameText != null)
            {
                if (unlocked)
                {
                    nameText.text = weapon.weaponName;
                    nameText.color = _unlockedColor;
                }
                else
                {
                    nameText.text = "🔒";
                    nameText.color = _lockIconColor;
                }
            }

            // 背景
            Image bgImage = cell.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = unlocked ? GetWeaponRarityColor(weapon.rarity) : _lockedColor;
            }

            // 类型标签
            Text typeLabel = FindChildText(cell, "TypeLabel");
            if (typeLabel != null)
            {
                typeLabel.text = unlocked ? weapon.type.ToString() : "";
            }

            // 成长阶段展示 (武器有5级成长)
            Slider growthSlider = FindChildSlider(cell, "GrowthSlider");
            if (growthSlider != null && unlocked)
            {
                // 从WeaponGrowth读取当前成长等级
                int growthLevel = GetWeaponGrowthLevel(weapon.weaponId);
                growthSlider.value = growthLevel / 5f; // 5级成长
            }

            // 点击事件
            Button cellButton = cell.GetComponent<Button>();
            if (cellButton != null)
            {
                cellButton.onClick.RemoveAllListeners();
                if (unlocked)
                {
                    cellButton.onClick.AddListener(() => ShowWeaponDetail(weapon));
                }
                else
                {
                    cellButton.onClick.AddListener(() => ShowLockedDetail(weapon.weaponId));
                }
            }
        }

        /// <summary>
        /// 获取筛选后的武器列表
        /// </summary>
        private List<WeaponData> GetFilteredWeapons()
        {
            if (_weaponDatabase == null) return new List<WeaponData>();

            List<WeaponData> weapons = _weaponDatabase.weapons;

            if (_currentFilter == CollectionFilter.UnlockedOnly)
                weapons = weapons.FindAll(w => IsWeaponUnlocked(w.weaponId));
            else if (_currentFilter == CollectionFilter.LockedOnly)
                weapons = weapons.FindAll(w => !IsWeaponUnlocked(w.weaponId));

            return weapons;
        }

        /// <summary>
        /// 排序武器列表
        /// </summary>
        private void SortWeapons(List<WeaponData> weapons, CollectionSort sortMethod)
        {
            switch (sortMethod)
            {
                case CollectionSort.ByID:
                    weapons.Sort((a, b) => string.Compare(a.weaponId, b.weaponId, StringComparison.Ordinal));
                    break;
                case CollectionSort.ByName:
                    weapons.Sort((a, b) => string.Compare(a.weaponName, b.weaponName, StringComparison.Ordinal));
                    break;
                case CollectionSort.ByRarity:
                    weapons.Sort((a, b) => ((int)b.rarity) - ((int)a.rarity));
                    break;
            }
        }

        /// <summary>
        /// 清除武器格子
        /// </summary>
        private void ClearWeaponCells()
        {
            foreach (GameObject cell in _weaponCells)
            {
                if (cell != null) Destroy(cell);
            }
            _weaponCells.Clear();
        }

        /// <summary>
        /// 获取武器成长等级（从PlayerPrefs读取）
        /// </summary>
        private int GetWeaponGrowthLevel(string weaponId)
        {
            return PlayerPrefs.GetInt($"WeaponGrowth_{weaponId}", 0);
        }

        #endregion

        #region ─── Internal – Detail Panel ─────────────

        /// <summary>
        /// 显示卡牌详情
        /// </summary>
        private void ShowCardDetail(CardData card)
        {
            if (_detailPanel == null) return;
            _detailPanel.SetActive(true);

            if (_detailTitleText != null)
                _detailTitleText.text = $"{card.cardId} - {card.cardName}";

            if (_detailDescriptionText != null)
                _detailDescriptionText.text = card.baseEffect;

            if (_detailStatsText != null)
                _detailStatsText.text = FormatCardStats(card);

            // 稀有度颜色
            if (_detailIconImage != null)
                _detailIconImage.color = GetRarityColor(card.rarity);

            // 卡牌无成长阶段，隐藏成长滑块
            if (_detailGrowthSlider != null)
                _detailGrowthSlider.gameObject.SetActive(false);

            if (_detailGrowthStageText != null)
                _detailGrowthStageText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 显示武器详情（含成长阶段）
        /// </summary>
        private void ShowWeaponDetail(WeaponData weapon)
        {
            if (_detailPanel == null) return;
            _detailPanel.SetActive(true);

            if (_detailTitleText != null)
                _detailTitleText.text = $"{weapon.weaponId} - {weapon.weaponName}";

            if (_detailDescriptionText != null)
                _detailDescriptionText.text = weapon.description;

            if (_detailStatsText != null)
                _detailStatsText.text = FormatWeaponStats(weapon);

            // 武器成长阶段展示
            if (_detailGrowthSlider != null)
            {
                _detailGrowthSlider.gameObject.SetActive(true);
                int growthLevel = GetWeaponGrowthLevel(weapon.weaponId);
                _detailGrowthSlider.value = growthLevel / 5f;
            }

            if (_detailGrowthStageText != null)
            {
                _detailGrowthStageText.gameObject.SetActive(true);
                int growthLevel = GetWeaponGrowthLevel(weapon.weaponId);
                _detailGrowthStageText.text = $"成长阶段: {growthLevel}/5";
            }
        }

        /// <summary>
        /// 显示锁定详情
        /// </summary>
        private void ShowLockedDetail(string itemId)
        {
            if (_detailPanel == null) return;
            _detailPanel.SetActive(true);

            if (_detailTitleText != null)
                _detailTitleText.text = "🔒 未解锁";

            if (_detailDescriptionText != null)
                _detailDescriptionText.text = $"该物品尚未解锁。\nID: {itemId}\n在游戏中获得此物品后可解锁详情。";

            if (_detailStatsText != null)
                _detailStatsText.text = "???";

            if (_detailGrowthSlider != null)
                _detailGrowthSlider.gameObject.SetActive(false);

            if (_detailGrowthStageText != null)
                _detailGrowthStageText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 隐藏详情面板
        /// </summary>
        private void HideDetailPanel()
        {
            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        #endregion

        #region ─── Internal – Progress Display ─────────

        /// <summary>
        /// 更新收集进度显示
        /// </summary>
        private void UpdateProgressDisplay()
        {
            int unlockedCards = CountUnlockedCards();
            int unlockedWeapons = CountUnlockedWeapons();

            // 卡牌进度
            float cardProgress = unlockedCards / (float)TOTAL_CARDS;
            if (_cardProgressSlider != null)
                _cardProgressSlider.value = cardProgress;
            if (_cardProgressText != null)
                _cardProgressText.text = $"卡牌: {unlockedCards}/{TOTAL_CARDS}";

            // 武器进度
            float weaponProgress = unlockedWeapons / (float)TOTAL_WEAPONS;
            if (_weaponProgressSlider != null)
                _weaponProgressSlider.value = weaponProgress;
            if (_weaponProgressText != null)
                _weaponProgressText.text = $"武器: {unlockedWeapons}/{TOTAL_WEAPONS}";

            // 总进度
            int totalItems = TOTAL_CARDS + TOTAL_WEAPONS;
            int totalUnlocked = unlockedCards + unlockedWeapons;
            float totalProgress = totalUnlocked / (float)totalItems;

            if (_totalProgressText != null)
                _totalProgressText.text = $"总进度: {totalUnlocked}/{totalItems} ({totalProgress:P0})";
        }

        #endregion

        #region ─── Internal – Event Handlers ───────────

        /// <summary>
        /// 类别筛选变更
        /// </summary>
        private void OnCategoryFilterChanged(int index)
        {
            if (index == 0)
            {
                _currentFilter = CollectionFilter.All;
            }
            else
            {
                _currentFilter = CollectionFilter.ByCategory;
                _currentCategoryFilter = (CardCategory)(index - 1); // A=0对应dropdown index=1
            }

            RefreshAllUI();
        }

        /// <summary>
        /// 排序方式变更
        /// </summary>
        private void OnSortChanged(int index)
        {
            _currentSort = (CollectionSort)index;
            RefreshAllUI();
        }

        /// <summary>
        /// 筛选Toggle变更
        /// </summary>
        private void OnFilterToggleChanged(bool value)
        {
            bool showUnlocked = _showUnlockedToggle != null && _showUnlockedToggle.isOn;
            bool showLocked = _showLockedToggle != null && _showLockedToggle.isOn;

            if (showUnlocked && !showLocked)
                _currentFilter = CollectionFilter.UnlockedOnly;
            else if (!showUnlocked && showLocked)
                _currentFilter = CollectionFilter.LockedOnly;
            else
                _currentFilter = CollectionFilter.All;

            RefreshAllUI();
        }

        #endregion

        #region ─── Internal – Utility ──────────────────

        /// <summary>
        /// 获取稀有度对应颜色
        /// </summary>
        private Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.N => new Color(0.8f, 0.8f, 0.8f),     // 白色
                CardRarity.R => new Color(0.2f, 0.6f, 1f),        // 蓝色
                CardRarity.SR => new Color(1f, 0.5f, 0f),         // 紫色
                CardRarity.SSR => new Color(1f, 0.85f, 0f),       // 金色
                _ => Color.white
            };
        }

        /// <summary>
        /// 获取武器稀有度对应颜色
        /// </summary>
        private Color GetWeaponRarityColor(WeaponRarity rarity)
        {
            return rarity switch
            {
                WeaponRarity.Normal => new Color(0.8f, 0.8f, 0.8f),
                WeaponRarity.Rare => new Color(0.2f, 0.6f, 1f),
                WeaponRarity.Epic => new Color(1f, 0.5f, 0f),
                WeaponRarity.Legendary => new Color(1f, 0.85f, 0f),
                WeaponRarity.Mythic => new Color(1f, 0.2f, 0.5f),
                _ => Color.white
            };
        }

        /// <summary>
        /// 格式化卡牌统计数据
        /// </summary>
        private string FormatCardStats(CardData card)
        {
            return $"类别: {card.category}\n" +
                   $"稀有度: {card.rarity}\n" +
                   $"目标: {card.target}\n" +
                   $"基础值: {card.baseValue}\n" +
                   $"每级提升: +{card.valuePerLevel}\n" +
                   $"最大等级: {card.maxLevel}\n" +
                   $"满级效果: {card.maxEffect}\n" +
                   $"融合路径: {card.fusionPath}\n" +
                   $"玩法流派: {card.playStyle}";
        }

        /// <summary>
        /// 格式化武器统计数据
        /// </summary>
        private string FormatWeaponStats(WeaponData weapon)
        {
            return $"类型: {weapon.type}\n" +
                   $"稀有度: {weapon.rarity}\n" +
                   $"基础伤害: {weapon.baseDamage}\n" +
                   $"攻击间隔: {weapon.attackInterval}s\n" +
                   $"弹道速度: {weapon.projectileSpeed}\n" +
                   $"弹道数量: {weapon.projectileCount}\n" +
                   $"穿透数: {weapon.pierceCount}\n" +
                   $"基础DPS: {weapon.baseDPS}\n" +
                   $"推荐Build: {weapon.recommendedBuild}";
        }

        /// <summary>
        /// PlayerPrefs键: 卡牌解锁
        /// </summary>
        private string GetCardUnlockKey(string cardId) => $"Collection_Card_{cardId}";

        /// <summary>
        /// PlayerPrefs键: 武器解锁
        /// </summary>
        private string GetWeaponUnlockKey(string weaponId) => $"Collection_Weapon_{weaponId}";

        /// <summary>
        /// 查找子对象中的Text组件
        /// </summary>
        private Text FindChildText(GameObject parent, string childName)
        {
            Transform child = parent.transform.Find(childName);
            return child != null ? child.GetComponent<Text>() : null;
        }

        /// <summary>
        /// 查找子对象中的Slider组件
        /// </summary>
        private Slider FindChildSlider(GameObject parent, string childName)
        {
            Transform child = parent.transform.Find(childName);
            return child != null ? child.GetComponent<Slider>() : null;
        }

        #endregion

        #region ─── Debug ───────────────────────────────

        [ContextMenu("Log Collection State")]
        private void LogCollectionState()
        {
            Debug.Log($"[CollectionUI] Cards: {CountUnlockedCards()}/{TOTAL_CARDS} | " +
                      $"Weapons: {CountUnlockedWeapons()}/{TOTAL_WEAPONS} | " +
                      "Filter: " + _currentFilter.ToString() + " | Sort: " + _currentSort.ToString() + " | Tab: " + (_showingCards ? "Cards" : "Weapons"));
        }

        [ContextMenu("Unlock All (Debug)")]
        private void UnlockAllDebug()
        {
            if (_cardDatabase != null)
            {
                foreach (CardData card in _cardDatabase.cards)
                    UnlockCard(card.cardId);
            }

            if (_weaponDatabase != null)
            {
                foreach (WeaponData weapon in _weaponDatabase.weapons)
                    UnlockWeapon(weapon.weaponId);
            }

            RefreshAllUI();
            Debug.Log("[CollectionUI] All items unlocked (Debug)");
        }

        #endregion
    }
}
