using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AngelGuardian.Core;
using AngelGuardian.Data;
using AngelGuardian.Weapons;
using WeaponRarity = AngelGuardian.Data.WeaponRarity;
using WeaponType = AngelGuardian.Data.WeaponType;
using WeaponsWeaponRarity = AngelGuardian.Weapons.WeaponRarity;
using WeaponsWeaponType = AngelGuardian.Weapons.WeaponType;

namespace AngelGuardian.UI
{
    /// <summary>
    /// 武器商店界面 —— 每10级触发（通过EventBus），3选1武器面板。
    /// 包含武器Icon(512×512)、稀有度光效、DPS显示、
    /// 武器对比、AI推荐评分、重roll、武器替换、成长提示。
    /// </summary>
    public class WeaponShopUI : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static WeaponShopUI _instance;
        public static WeaponShopUI Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<WeaponShopUI>();
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

        #region ─── Inspector: Root ───���──────────────────

        [Header("Root")]
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Canvas _mainCanvas;

        #endregion

        #region ─── Inspector: Weapon Panels (3选1) ─────

        [Header("Weapon Panels (3)")]
        [SerializeField] private WeaponShopPanel[] _weaponPanels = new WeaponShopPanel[3];

        [System.Serializable]
        public class WeaponShopPanel
        {
            public GameObject root;
            public Button selectButton;
            public Image weaponIcon;            // 512x512
            public Image rarityGlow;
            public TMP_Text weaponNameText;
            public TMP_Text dpsText;
            public TMP_Text descriptionText;
            public TMP_Text typeLabel;

            [Header("AI Recommendation")]
            public GameObject aiRecommendRoot;
            public Image[] aiStars;             // 5颗星
            public TMP_Text aiCommentText;

            [Header("Comparison (replace mode)")]
            public GameObject comparisonRoot;
            public TMP_Text currentWeaponName;
            public TMP_Text currentDPS;
            public TMP_Text newDPS;
            public Image dpsArrow;              // 红降绿升
            public Color dpsUpColor = new Color(0.3f, 0.85f, 0.3f);
            public Color dpsDownColor = new Color(0.9f, 0.2f, 0.15f);

            public WeaponData currentWeaponData;
            public int aiScore;                 // 1-5

            public void Setup(WeaponData data, int aiScore, bool showComparison, WeaponBase currentWeapon, System.Action<WeaponData> onSelect)
            {
                currentWeaponData = data;
                this.aiScore = aiScore;

                if (data == null)
                {
                    root.SetActive(false);
                    return;
                }

                root.SetActive(true);

                // 名称
                if (weaponNameText != null)
                    weaponNameText.text = data.weaponName;

                // DPS
                if (dpsText != null)
                    dpsText.text = $"DPS: {data.baseDPS:F0}";

                // 描述
                if (descriptionText != null)
                    descriptionText.text = data.description;

                // 类型
                if (typeLabel != null)
                    typeLabel.text = data.type.ToString();

                // 稀有度光效
                if (rarityGlow != null)
                    rarityGlow.color = GetRarityGlowColor(data.rarity);

                // AI推荐
                if (aiRecommendRoot != null)
                    aiRecommendRoot.SetActive(true);
                if (aiStars != null)
                {
                    for (int i = 0; i < aiStars.Length; i++)
                    {
                        aiStars[i].gameObject.SetActive(i < aiScore);
                        if (aiStars[i] != null)
                            aiStars[i].color = i < aiScore ? new Color(1f, 0.85f, 0f) : new Color(0.3f, 0.3f, 0.3f);
                    }
                }
                if (aiCommentText != null)
                    aiCommentText.text = GetAIComment(aiScore);

                // 武器对比
                if (comparisonRoot != null)
                    comparisonRoot.SetActive(showComparison);
                if (showComparison && currentWeapon != null)
                {
                    float currentDPSVal = currentWeapon.EffectiveDamage / Mathf.Max(currentWeapon.attackInterval, 0.1f);
                    float newDPSVal = data.baseDPS;

                    if (currentWeaponName != null)
                        currentWeaponName.text = currentWeapon.weaponName;
                    if (currentDPS != null)
                        currentDPS.text = $"DPS: {currentDPSVal:F0}";
                    if (newDPS != null)
                        newDPS.text = $"DPS: {newDPSVal:F0}";

                    if (dpsArrow != null)
                    {
                        dpsArrow.gameObject.SetActive(true);
                        dpsArrow.color = newDPSVal >= currentDPSVal ? dpsUpColor : dpsDownColor;
                        dpsArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, newDPSVal >= currentDPSVal ? -90f : 90f);
                    }
                }
                else
                {
                    if (dpsArrow != null) dpsArrow.gameObject.SetActive(false);
                }

                // 绑定按钮
                if (selectButton != null)
                {
                    selectButton.onClick.RemoveAllListeners();
                    selectButton.onClick.AddListener(() => onSelect?.Invoke(data));
                }
            }

            private static Color GetRarityGlowColor(WeaponRarity rarity)
            {
                return rarity switch
                {
                    WeaponRarity.Normal => new Color(0.4f, 0.4f, 0.4f, 0.5f),
                    WeaponRarity.Rare => new Color(0.2f, 0.4f, 1f, 0.5f),
                    WeaponRarity.Epic => new Color(0.5f, 0.15f, 0.9f, 0.6f),
                    WeaponRarity.Legendary => new Color(1f, 0.45f, 0f, 0.7f),
                    WeaponRarity.Mythic => new Color(1f, 0.1f, 0.2f, 0.8f),
                    _ => Color.white
                };
            }

            private static string GetAIComment(int score)
            {
                return score switch
                {
                    5 => "极力推荐！完美契合当前Build",
                    4 => "强烈推荐，显著提升战力",
                    3 => "不错的选择，稳定提升",
                    2 => "可用，但非最优选择",
                    1 => "暂不推荐，与当前Build契合度低",
                    _ => "无法评估"
                };
            }
        }

        #endregion

        #region ─── Inspector: Reroll Button ─────────────

        [Header("Reroll")]
        [SerializeField] private Button _rerollButton;
        [SerializeField] private TMP_Text _rerollCostText;
        [SerializeField] private RectTransform _rerollIcon;

        private int _rerollCost;
        private int _shopLevel;

        #endregion

        #region ─── Inspector: Growth Hints ──────────────

        [Header("Growth Hints")]
        [SerializeField] private GameObject _growthHintRoot;
        [SerializeField] private Slider _killEvolutionSlider;
        [SerializeField] private Slider _comboTemperSlider;
        [SerializeField] private Slider _limitBreakSlider;

        #endregion

        #region ─── Inspector: Weapon Replacement ────────

        [Header("Replacement Warning")]
        [SerializeField] private GameObject _replaceWarningRoot;
        [SerializeField] private TMP_Text _replaceWarningText;
        [SerializeField] private TMP_Text _aoeBurstHintText;

        #endregion

        #region ─── Runtime State ────────────────────────

        private WeaponData[] _currentChoices = new WeaponData[3];
        private bool _isVisible;
        private bool _isReplacing; // 满6把时替换模式

        #endregion

        #region ─── Unity Lifecycle ──────────────────────

        private void Start()
        {
            SubscribeEvents();

            // 初始隐藏
            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = false;
            }

            if (_growthHintRoot != null)
                _growthHintRoot.SetActive(false);
            if (_replaceWarningRoot != null)
                _replaceWarningRoot.SetActive(false);

            // 绑定按钮
            if (_rerollButton != null)
                _rerollButton.onClick.AddListener(OnRerollClicked);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (_rerollButton != null)
                _rerollButton.onClick.RemoveListener(OnRerollClicked);
        }

        #endregion

        #region ─── Event Subscriptions ──────────────────

        private void SubscribeEvents()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnShopOpened.AddListener(OnShopOpened);
                EventBus.Instance.OnLevelUp.AddListener(OnLevelUpCheck);
            }
        }

        private void UnsubscribeEvents()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnShopOpened.RemoveListener(OnShopOpened);
                EventBus.Instance.OnLevelUp.RemoveListener(OnLevelUpCheck);
            }
        }

        #endregion

        #region ─── Trigger Logic ─────────────────────────

        /// <summary>
        /// 每10级触发武器商店
        /// </summary>
        private void OnLevelUpCheck(int newLevel)
        {
            if (newLevel > 0 && newLevel % 10 == 0)
            {
                OpenShop(newLevel);
            }
        }

        private void OnShopOpened()
        {
            int level = GameManager.Instance?.CurrentLevel ?? 1;
            OpenShop(level);
        }

        #endregion

        #region ─── Shop Logic ────────────────────────────

        private void OpenShop(int level)
        {
            _shopLevel = level;
            _isReplacing = WeaponManager.Instance?.IsFull ?? false;

            GenerateWeaponChoices();
            Show();

            // 显示替换警告
            if (_replaceWarningRoot != null)
                _replaceWarningRoot.SetActive(_isReplacing);
            if (_replaceWarningText != null && _isReplacing)
            {
                var wm = WeaponManager.Instance;
                var current = wm?.CurrentWeapon;
                _replaceWarningText.text = $"武器槽已满(6/6)\n选择新武器将替换当前武器: {(current != null ? current.weaponName : "无")}\n旧武器会释放AOE爆发！";
            }
            if (_aoeBurstHintText != null)
                _aoeBurstHintText.gameObject.SetActive(_isReplacing);
        }

        /// <summary>
        /// 生成3个随机武器选项（AI评分基于兼容性矩阵）
        /// </summary>
        private void GenerateWeaponChoices()
        {
            var db = WeaponManager.Instance?.WeaponDatabase;
            if (db == null || db.weapons == null || db.weapons.Count == 0)
            {
                Debug.LogWarning("[WeaponShopUI] WeaponDatabase is empty.");
                return;
            }

            var wm = WeaponManager.Instance;
            var ownedIds = new HashSet<string>(wm?.GetAllWeapons().Select(w => w.weaponId) ?? System.Array.Empty<string>());

            // 排除已拥有的武器
            var available = db.weapons
                .Where(w => !ownedIds.Contains(w.weaponId))
                .ToList();

            if (available.Count < 3)
                available = db.weapons.ToList();

            // 加权随机（稀有度权重）
            var pool = new List<WeaponData>();
            foreach (var weapon in available)
            {
                int weight = weapon.rarity switch
                {
                    WeaponRarity.Normal => 5,
                    WeaponRarity.Rare => 4,
                    WeaponRarity.Epic => 2,
                    WeaponRarity.Legendary => 1,
                    WeaponRarity.Mythic => 1,
                    _ => 1
                };
                for (int i = 0; i < weight; i++)
                    pool.Add(weapon);
            }

            var selected = new HashSet<string>();
            var result = new WeaponData[3];
            int count = 0;

            for (int attempt = 0; attempt < 100 && count < 3; attempt++)
            {
                if (pool.Count == 0) break;
                int idx = Random.Range(0, pool.Count);
                var candidate = pool[idx];
                if (!selected.Contains(candidate.weaponId))
                {
                    selected.Add(candidate.weaponId);
                    result[count] = candidate;
                    count++;
                }
                else
                {
                    pool.RemoveAll(w => w.weaponId == candidate.weaponId);
                }
            }

            // 补足
            foreach (var w in available)
            {
                if (count >= 3) break;
                if (!selected.Contains(w.weaponId))
                {
                    selected.Add(w.weaponId);
                    result[count] = w;
                    count++;
                }
            }

            _currentChoices = result;
            _rerollCost = _shopLevel * 10;
            UpdateRerollCostDisplay();
        }

        /// <summary>
        /// AI评分：基于当前持有武器的兼容性矩阵计算
        /// </summary>
        private int CalculateAIScore(WeaponData candidate)
        {
            var wm = WeaponManager.Instance;
            var db = wm?.WeaponDatabase;
            if (wm == null || db == null) return 3;

            var ownedWeapons = wm.GetAllWeapons();
            if (ownedWeapons.Count == 0) return 3;

            // 计算平均兼容性
            float totalCompat = 0f;
            int compatCount = 0;

            foreach (var owned in ownedWeapons)
            {
                int ownedIdx = db.weapons.FindIndex(w => w.weaponId == owned.weaponId);
                int candidateIdx = db.weapons.FindIndex(w => w.weaponId == candidate.weaponId);

                if (ownedIdx >= 0 && candidateIdx >= 0)
                {
                    totalCompat += db.GetCompatibility(ownedIdx, candidateIdx);
                    compatCount++;
                }
            }

            if (compatCount == 0) return 3;

            float avgCompat = totalCompat / compatCount;

            // 转换为星级评分
            if (avgCompat >= 0.7f) return 5;
            if (avgCompat >= 0.5f) return 4;
            if (avgCompat >= 0.3f) return 3;
            if (avgCompat >= 0.15f) return 2;
            return 1;
        }

        #endregion

        #region ─── Show / Hide ──────────────────────────

        private void Show()
        {
            if (_rootCanvasGroup == null) return;

            var wm = WeaponManager.Instance;

            // 填充面板
            for (int i = 0; i < _weaponPanels.Length && i < _currentChoices.Length; i++)
            {
                if (_weaponPanels[i] != null)
                {
                    int aiScore = CalculateAIScore(_currentChoices[i]);
                    bool showComparison = _isReplacing && wm?.CurrentWeapon != null;
                    _weaponPanels[i].Setup(_currentChoices[i], aiScore, showComparison, wm?.CurrentWeapon, OnWeaponSelected);
                }
            }

            // 更新成长提示
            UpdateGrowthHints();

            StartCoroutine(FadeIn());
            _isVisible = true;

            GameManager.Instance?.PauseGame();
        }

        private void Hide()
        {
            StartCoroutine(FadeOut(() =>
            {
                _isVisible = false;
                GameManager.Instance?.ResumeGame();
            }));
        }

        private IEnumerator FadeIn()
        {
            if (_rootCanvasGroup == null) yield break;

            _rootCanvasGroup.alpha = 0f;
            _rootCanvasGroup.interactable = true;
            _rootCanvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            float duration = 0.3f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _rootCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            _rootCanvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut(System.Action onComplete = null)
        {
            if (_rootCanvasGroup == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            float duration = 0.2f;
            float startAlpha = _rootCanvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _rootCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }

            _rootCanvasGroup.alpha = 0f;
            _rootCanvasGroup.interactable = false;
            _rootCanvasGroup.blocksRaycasts = false;

            onComplete?.Invoke();
        }

        #endregion

        #region ─── Weapon Selection ─────────────────────

        private void OnWeaponSelected(WeaponData weaponData)
        {
            if (!_isVisible) return;

            var wm = WeaponManager.Instance;
            if (wm == null) return;

            // 从数据库查找对应WeaponBase
            // 注意：WeaponBase是ScriptableObject，需要从Resources加载
            var weaponBase = Resources.Load<WeaponBase>($"Weapons/{weaponData.weaponId}");
            if (weaponBase == null)
            {
                // 尝试直接从数据库关联
                Debug.LogWarning($"[WeaponShopUI] Cannot find WeaponBase for {weaponData.weaponId}. Using data-only add.");
                // 回退方案：通过名称查找
                weaponBase = Resources.FindObjectsOfTypeAll<WeaponBase>()
                    .FirstOrDefault(w => w.weaponId == weaponData.weaponId);
            }

            if (_isReplacing && wm.IsFull)
            {
                // 替换当前武器，旧武器AOE爆发
                wm.ReplaceWeapon(wm.CurrentWeaponIndex, weaponBase ?? CreateWeaponFromData(weaponData));
            }
            else
            {
                if (weaponBase != null)
                    wm.AddWeapon(weaponBase);
                else
                    wm.AddWeapon(CreateWeaponFromData(weaponData));
            }

            // 选择动画
            StartCoroutine(SelectionAnimation());

            // 延迟关闭
            StartCoroutine(DelayedHide(0.5f));
        }

        /// <summary>
        /// 从WeaponData创建WeaponBase（当ScriptableObject不可用时）
        /// </summary>
        private WeaponBase CreateWeaponFromData(WeaponData data)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponBase>();
            weapon.weaponId = data.weaponId;
            weapon.weaponName = data.weaponName;
            weapon.type = (WeaponsWeaponType)(int)data.type;
            weapon.rarity = (WeaponsWeaponRarity)((int)data.rarity - 1); // Data rarity starts at 1, Weapons rarity starts at 0
            weapon.baseDamage = data.baseDamage;
            weapon.attackInterval = data.attackInterval;
            weapon.projectileSpeed = data.projectileSpeed;
            weapon.projectileCount = data.projectileCount;
            weapon.pierceCount = data.pierceCount;
            weapon.specialParams = data.specialParams;
            weapon.buffTags = data.buffTags ?? new string[0];
            weapon.description = data.description;
            return weapon;
        }

        private IEnumerator SelectionAnimation()
        {
            // 简单的缩放脉冲
            yield return new WaitForSecondsRealtime(0.1f);
        }

        private IEnumerator DelayedHide(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Hide();
        }

        #endregion

        #region ─── Reroll ────────────────────────────────

        private void OnRerollClicked()
        {
            if (!_isVisible) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CurrentExp < _rerollCost)
            {
                StartCoroutine(ShakeButton(_rerollButton.transform));
                return;
            }

            gm.AddExp(-_rerollCost);

            // 旋转动画
            StartCoroutine(RerollAnimation());

            GenerateWeaponChoices();
            var wm = WeaponManager.Instance;
            for (int i = 0; i < _weaponPanels.Length && i < _currentChoices.Length; i++)
            {
                if (_weaponPanels[i] != null)
                {
                    int aiScore = CalculateAIScore(_currentChoices[i]);
                    bool showComparison = _isReplacing && wm?.CurrentWeapon != null;
                    _weaponPanels[i].Setup(_currentChoices[i], aiScore, showComparison, wm?.CurrentWeapon, OnWeaponSelected);
                }
            }

            _rerollCost = _shopLevel * 10;
            UpdateRerollCostDisplay();
        }

        private IEnumerator RerollAnimation()
        {
            if (_rerollIcon == null) yield break;

            float elapsed = 0f;
            float duration = 0.6f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                _rerollIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 720f, t));
                yield return null;
            }
            _rerollIcon.localRotation = Quaternion.identity;
        }

        private IEnumerator ShakeButton(Transform target)
        {
            Vector3 originalPos = target.localPosition;
            float elapsed = 0f;
            float duration = 0.3f;
            float amplitude = 5f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float x = Mathf.Sin(elapsed * 30f) * amplitude * (1f - elapsed / duration);
                target.localPosition = originalPos + new Vector3(x, 0f, 0f);
                yield return null;
            }
            target.localPosition = originalPos;
        }

        private void UpdateRerollCostDisplay()
        {
            if (_rerollCostText != null)
                _rerollCostText.text = $"重Roll ({_rerollCost} EXP)";
        }

        #endregion

        #region ─── Growth Hints ─────────────────────────

        private void UpdateGrowthHints()
        {
            if (_growthHintRoot == null) return;

            var wm = WeaponManager.Instance;
            var wg = WeaponGrowth.Instance;
            if (wm == null || wg == null)
            {
                _growthHintRoot.SetActive(false);
                return;
            }

            var weapons = wm.GetAllWeapons();
            if (weapons.Count == 0)
            {
                _growthHintRoot.SetActive(false);
                return;
            }

            _growthHintRoot.SetActive(true);

            // 使用第一个武器显示成长进度
            var weapon = weapons[0];

            if (_killEvolutionSlider != null)
                _killEvolutionSlider.value = (float)weapon.TotalKills / Mathf.Max(wg.killEvolutionThreshold, 1);

            if (_comboTemperSlider != null)
                _comboTemperSlider.value = (float)weapon.ComboTriggerCount / Mathf.Max(wg.comboTemperingThreshold, 1);

            if (_limitBreakSlider != null)
                _limitBreakSlider.value = weapon.TotalDamageDealt / Mathf.Max(wg.limitBreakDamageThreshold, 1);
        }

        #endregion

        #region ─── Public API ───────────────────────────

        /// <summary>
        /// 手动触发武器商店
        /// </summary>
        public void TriggerWeaponShop(int level)
        {
            OpenShop(level);
        }

        #endregion
    }
}
