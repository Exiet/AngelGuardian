using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AngelGuardian.Core;
using AngelGuardian.Data;
using AngelGuardian.Cards;

namespace AngelGuardian.UI
{
    /// <summary>
    /// 升级选卡界面 —— 升级时触发，3选1卡牌面板。
    /// 包含卡牌Icon、名称、稀有度边框、类别角标、
    /// 卡牌预览（点击放大）、重roll、跳过选择。
    /// </summary>
    public class CardSelectUI : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static CardSelectUI _instance;
        public static CardSelectUI Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<CardSelectUI>();
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

        #region ─── Inspector: Root ──────────────────────

        [Header("Root")]
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Canvas _mainCanvas;

        #endregion

        #region ─── Inspector: Card Panels (3选1) ───────

        [Header("Card Select Panels (3)")]
        [SerializeField] private CardSelectPanel[] _cardPanels = new CardSelectPanel[3];

        [System.Serializable]
        public class CardSelectPanel
        {
            public GameObject root;
            public Button selectButton;
            public Image cardIcon;              // 256x256
            public Image rarityBorder;
            public TMP_Text cardNameText;
            public TMP_Text baseEffectText;
            public Image categoryBadge;
            public TMP_Text categoryLabel;

            [Header("Preview (click to enlarge)")]
            public GameObject previewRoot;
            public Image previewIcon;           // 200x280
            public TMP_Text previewDescription;
            public TMP_Text upgradePathText;    // Lv2-Lv5
            public TMP_Text fusionPathText;     // 融合进化路径

            public CardData currentCardData;

            public void Setup(CardData data, System.Action<CardData> onSelect)
            {
                currentCardData = data;
                if (data == null)
                {
                    root.SetActive(false);
                    return;
                }

                root.SetActive(true);

                // 名称
                if (cardNameText != null)
                    cardNameText.text = data.cardName;

                // 基础效果
                if (baseEffectText != null)
                    baseEffectText.text = data.baseEffect;

                // 稀有度边框颜色
                if (rarityBorder != null)
                    rarityBorder.color = GetRarityColor(data.rarity);

                // 类别角标
                if (categoryBadge != null)
                    categoryBadge.gameObject.SetActive(true);
                if (categoryLabel != null)
                    categoryLabel.text = GetCategoryShortLabel(data.category);

                // 预览信息
                if (upgradePathText != null)
                    upgradePathText.text = GenerateUpgradePathText(data);
                if (fusionPathText != null)
                    fusionPathText.text = data.fusionPath ?? "无融合路径";

                if (previewDescription != null)
                    previewDescription.text = $"{data.baseEffect}\n\n满级效果:\n{data.maxEffect}";

                // 绑定按钮
                if (selectButton != null)
                {
                    selectButton.onClick.RemoveAllListeners();
                    selectButton.onClick.AddListener(() => onSelect?.Invoke(data));
                }
            }

            public void ShowPreview(bool show)
            {
                if (previewRoot != null)
                    previewRoot.SetActive(show);
            }

            private string GenerateUpgradePathText(CardData data)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("升级路径:");
                for (int lv = 2; lv <= data.maxLevel; lv++)
                {
                    float val = data.baseValue + data.valuePerLevel * (lv - 1);
                    sb.AppendLine($"  Lv{lv}: {data.baseEffect.Replace(data.baseValue.ToString("F1"), val.ToString("F1"))}");
                }
                return sb.ToString();
            }

            public static Color GetRarityColor(CardRarity rarity)
            {
                return rarity switch
                {
                    CardRarity.N => new Color(0.5f, 0.5f, 0.5f),      // 灰色
                    CardRarity.R => new Color(0.2f, 0.5f, 1f),        // 蓝色
                    CardRarity.SR => new Color(0.6f, 0.2f, 1f),       // 紫色
                    CardRarity.SSR => new Color(1f, 0.55f, 0f),       // 橙色
                    _ => Color.white
                };
            }

            public static string GetCategoryShortLabel(CardCategory category)
            {
                return category switch
                {
                    CardCategory.A_Attack => "剑",
                    CardCategory.B_BabyControl => "婴",
                    CardCategory.C_Aura => "环",
                    CardCategory.D_Terrain => "砖",
                    CardCategory.E_Passive => "闪",
                    CardCategory.F_Growth => "箭",
                    CardCategory.G_Emotion => "心",
                    CardCategory.H_Combo => "链",
                    CardCategory.I_TerrainActivation => "藤",
                    _ => "?"
                };
            }
        }

        #endregion

        #region ─── Inspector: Reroll Button ─────────────

        [Header("Reroll")]
        [SerializeField] private Button _rerollButton;
        [SerializeField] private TMP_Text _rerollCostText;
        [SerializeField] private RectTransform _rerollIcon;
        [SerializeField] private float _rerollRotateDuration = 0.6f;

        private int _rerollCost;

        #endregion

        #region ─── Inspector: Skip Button ───────────────

        [Header("Skip")]
        [SerializeField] private Button _skipButton;

        #endregion

        #region ─── Inspector: Selection Animation ───────

        [Header("Selection Animation")]
        [SerializeField] private float _selectAnimDuration = 0.3f;
        [SerializeField] private AnimationCurve _selectAnimCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #endregion

        #region ─── Runtime State ────────────────────────

        private CardData[] _currentChoices = new CardData[3];
        private bool _isVisible;
        private int _currentLevel;

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

            // 预览默认隐藏
            foreach (var panel in _cardPanels)
            {
                if (panel != null)
                    panel.ShowPreview(false);
            }

            // 绑定重roll按钮
            if (_rerollButton != null)
                _rerollButton.onClick.AddListener(OnRerollClicked);

            // 绑定跳过按钮
            if (_skipButton != null)
                _skipButton.onClick.AddListener(OnSkipClicked);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();

            if (_rerollButton != null)
                _rerollButton.onClick.RemoveListener(OnRerollClicked);
            if (_skipButton != null)
                _skipButton.onClick.RemoveListener(OnSkipClicked);
        }

        #endregion

        #region ─── Event Subscriptions ──────────────────

        private void SubscribeEvents()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnLevelUp.AddListener(OnLevelUp);
            }
        }

        private void UnsubscribeEvents()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnLevelUp.RemoveListener(OnLevelUp);
            }
        }

        #endregion

        #region ─── Level Up Handler ─────────────────────

        private void OnLevelUp(int newLevel)
        {
            _currentLevel = newLevel;
            GenerateCardChoices();
            Show();
        }

        /// <summary>
        /// 生成3个随机卡牌选项（基于稀有度加权）
        /// </summary>
        private void GenerateCardChoices()
        {
            var db = CardManager.Instance?.CardDatabase;
            if (db == null || db.cards == null || db.cards.Count == 0)
            {
                Debug.LogWarning("[CardSelectUI] CardDatabase is empty.");
                return;
            }

            var ownedCards = CardManager.Instance?.GetAllCards() ?? new List<CardInstance>();
            var ownedIds = new HashSet<string>(ownedCards.Select(c => c.cardId));

            // 获取未拥有的卡牌（或可升级的卡牌）
            var availableCards = db.cards
                .Where(c => !ownedIds.Contains(c.cardId) || ownedCards.Any(oc => oc.cardId == c.cardId && oc.CanUpgrade))
                .ToList();

            if (availableCards.Count < 3)
                availableCards = db.cards.ToList();

            // 加权随机选择（稀有度权重: N=4, R=3, SR=2, SSR=1）
            var weighted = new List<(CardData card, int weight)>();
            foreach (var card in availableCards)
            {
                int weight = card.rarity switch
                {
                    CardRarity.N => 4,
                    CardRarity.R => 3,
                    CardRarity.SR => 2,
                    CardRarity.SSR => 1,
                    _ => 1
                };
                weighted.Add((card, weight));
            }

            // Fisher-Yates加权随机选择3张不重复的卡
            var selected = new HashSet<string>();
            var result = new CardData[3];
            int count = 0;

            // 构建加权列表用于随机
            var pool = new List<CardData>();
            foreach (var (card, weight) in weighted)
            {
                for (int i = 0; i < weight; i++)
                    pool.Add(card);
            }

            // 随机选择
            for (int attempt = 0; attempt < 100 && count < 3; attempt++)
            {
                if (pool.Count == 0) break;
                int idx = Random.Range(0, pool.Count);
                var candidate = pool[idx];
                if (!selected.Contains(candidate.cardId))
                {
                    selected.Add(candidate.cardId);
                    result[count] = candidate;
                    count++;
                }
                else
                {
                    // 若已选，减少该卡在池中的出现概率
                    pool.RemoveAll(c => c.cardId == candidate.cardId);
                }
            }

            // 补足
            if (count < 3)
            {
                foreach (var card in availableCards)
                {
                    if (count >= 3) break;
                    if (!selected.Contains(card.cardId))
                    {
                        selected.Add(card.cardId);
                        result[count] = card;
                        count++;
                    }
                }
            }

            _currentChoices = result;
            _rerollCost = _currentLevel * 10;
            UpdateRerollCostDisplay();
        }

        #endregion

        #region ─── Show / Hide ──────────────────────────

        private void Show()
        {
            if (_rootCanvasGroup == null) return;

            // 填充面板
            for (int i = 0; i < _cardPanels.Length && i < _currentChoices.Length; i++)
            {
                if (_cardPanels[i] != null)
                    _cardPanels[i].Setup(_currentChoices[i], OnCardSelected);
            }

            // 淡入显示
            StartCoroutine(FadeIn());
            _isVisible = true;

            // 暂停游戏
            GameManager.Instance?.PauseGame();
        }

        private void Hide()
        {
            StartCoroutine(FadeOut(() =>
            {
                _isVisible = false;
                // 恢复游戏
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

        #region ─── Card Selection ───────────────────────

        private void OnCardSelected(CardData cardData)
        {
            if (!_isVisible) return;

            // 找到被选中的面板索引
            int selectedIndex = -1;
            for (int i = 0; i < _cardPanels.Length; i++)
            {
                if (_cardPanels[i]?.currentCardData == cardData)
                {
                    selectedIndex = i;
                    break;
                }
            }

            // 播放选择确认动画（弹性缩放）
            if (selectedIndex >= 0 && _cardPanels[selectedIndex]?.root != null)
            {
                StartCoroutine(SelectionBounceAnimation(_cardPanels[selectedIndex].root.transform));
            }

            // 添加/升级卡牌
            var cm = CardManager.Instance;
            if (cm != null)
            {
                cm.AddCard(cardData);
            }

            // 延迟关闭
            StartCoroutine(DelayedHide(0.5f));
        }

        /// <summary>
        /// 0.3秒弹性缩放确认动画
        /// </summary>
        private IEnumerator SelectionBounceAnimation(Transform target)
        {
            float elapsed = 0f;
            Vector3 originalScale = target.localScale;

            while (elapsed < _selectAnimDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _selectAnimDuration;
                float scale = 1f + Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 0.3f;
                target.localScale = originalScale * scale;
                yield return null;
            }

            target.localScale = originalScale;
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

            // 检查经验是否足够
            if (gm.CurrentExp < _rerollCost)
            {
                Debug.LogWarning($"[CardSelectUI] Not enough EXP for reroll. Need {_rerollCost}, have {gm.CurrentExp:F0}");
                // 抖动提示
                StartCoroutine(ShakeButton(_rerollButton.transform));
                return;
            }

            // 消耗经验
            gm.AddExp(-_rerollCost);

            // 旋转动画
            StartCoroutine(RerollAnimation());

            // 重新生成选项
            GenerateCardChoices();
            for (int i = 0; i < _cardPanels.Length && i < _currentChoices.Length; i++)
            {
                _cardPanels[i]?.Setup(_currentChoices[i], OnCardSelected);
            }

            _rerollCost = _currentLevel * 10;
            UpdateRerollCostDisplay();
        }

        private IEnumerator RerollAnimation()
        {
            if (_rerollIcon == null) yield break;

            float elapsed = 0f;
            while (elapsed < _rerollRotateDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _rerollRotateDuration;
                float angle = Mathf.Lerp(0f, 360f * 2f, t);
                _rerollIcon.localRotation = Quaternion.Euler(0f, 0f, angle);
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

        #region ─── Skip ──────────────────────────────────

        private void OnSkipClicked()
        {
            if (!_isVisible) return;
            Hide();
        }

        #endregion

        #region ─── Card Preview (Click to Enlarge) ──────

        /// <summary>
        /// 点击卡牌图标放大预览 (200×280pt)
        /// 由Button的OnClick调用或代码绑定
        /// </summary>
        public void TogglePreview(int panelIndex)
        {
            if (panelIndex < 0 || panelIndex >= _cardPanels.Length) return;
            var panel = _cardPanels[panelIndex];
            if (panel == null) return;

            // 关闭其他预览
            for (int i = 0; i < _cardPanels.Length; i++)
            {
                if (i != panelIndex && _cardPanels[i] != null)
                    _cardPanels[i].ShowPreview(false);
            }

            // 切换当前
            panel.ShowPreview(!panel.previewRoot.activeSelf);
        }

        #endregion

        #region ─── Public API ───────────────────────────

        /// <summary>
        /// 手动触发选卡界面（用于测试或特殊触发）
        /// </summary>
        public void TriggerCardSelect(int level)
        {
            _currentLevel = level;
            GenerateCardChoices();
            Show();
        }

        #endregion
    }
}
