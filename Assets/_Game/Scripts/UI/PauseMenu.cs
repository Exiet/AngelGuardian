using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using AngelGuardian.Core;
using AngelGuardian.Player;
using AngelGuardian.Baby;
using AngelGuardian.Cards;
using AngelGuardian.Weapons;

namespace AngelGuardian.UI
{
    /// <summary>
    /// 暂停菜单 —— ESC/返回键/暂停按钮触发。
    /// 包含半透明遮罩、菜单按钮、角色属性面板、技能列表。
    /// 动画：弹出0.3s ease-out，关闭0.2s ease-in。
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static PauseMenu _instance;
        public static PauseMenu Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<PauseMenu>();
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
        [SerializeField] private Image _overlay;       // 半透明黑色遮罩(70%不透明度)

        [Header("Animation")]
        [SerializeField] private RectTransform _menuPanel;
        [SerializeField] private float _openDuration = 0.3f;
        [SerializeField] private float _closeDuration = 0.2f;
        [SerializeField] private AnimationCurve _openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _closeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        #endregion

        #region ─── Inspector: Menu Buttons ──────────────

        [Header("Menu Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _characterStatsButton;
        [SerializeField] private Button _skillListButton;
        [SerializeField] private Button _weaponListButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        #endregion

        #region ─── Inspector: Character Stats Panel ─────

        [Header("Character Stats Panel")]
        [SerializeField] private GameObject _characterStatsPanel;
        [SerializeField] private Transform _angelStatsContainer;
        [SerializeField] private Transform _babyStatsContainer;
        [SerializeField] private GameObject _statRowPrefab;
        [SerializeField] private TMP_Text _currentEmotionText;
        [SerializeField] private Image _emotionIcon;

        [System.Serializable]
        public class StatRow
        {
            public TMP_Text statName;
            public TMP_Text statValue;
        }

        #endregion

        #region ─── Inspector: Skill List Panel ──────────

        [Header("Skill List Panel")]
        [SerializeField] private GameObject _skillListPanel;
        [SerializeField] private Transform _skillListContainer;
        [SerializeField] private GameObject _skillItemPrefab;
        [SerializeField] private TMP_Text _skillCountText;

        [System.Serializable]
        public class SkillItemUI
        {
            public Image cardIcon;
            public TMP_Text cardName;
            public TMP_Text cardLevel;
            public Image categoryBadge;
        }

        #endregion

        #region ─── Inspector: Weapon List Panel ─────────

        [Header("Weapon List Panel")]
        [SerializeField] private GameObject _weaponListPanel;
        [SerializeField] private Transform _weaponListContainer;
        [SerializeField] private GameObject _weaponItemPrefab;

        #endregion

        #region ─── Inspector: Settings Panel ────────────

        [Header("Settings Panel")]
        [SerializeField] private GameObject _settingsPanel;

        #endregion

        #region ─── Runtime State ────────────────────────

        private bool _isOpen;
        private GameObject _activeSubPanel;

        #endregion

        #region ─── Unity Lifecycle ──────────────────────

        private void Start()
        {
            // 初始隐藏
            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = false;
            }

            // 遮罩颜色
            if (_overlay != null)
                _overlay.color = new Color(0f, 0f, 0f, 0.7f);

            // 隐藏子面板
            HideAllSubPanels();

            // 绑定按钮
            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_characterStatsButton != null)
                _characterStatsButton.onClick.AddListener(OnCharacterStatsClicked);
            if (_skillListButton != null)
                _skillListButton.onClick.AddListener(OnSkillListClicked);
            if (_weaponListButton != null)
                _weaponListButton.onClick.AddListener(OnWeaponListClicked);
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void Update()
        {
            // ESC/返回键触发
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isOpen)
                {
                    // 如果有子面板打开，先关闭子面板
                    if (_activeSubPanel != null)
                    {
                        HideAllSubPanels();
                    }
                    else
                    {
                        OnResumeClicked();
                    }
                }
                else
                {
                    // 检查是否在游戏中
                    var gm = GameManager.Instance;
                    if (gm != null && gm.CurrentState == GameManager.GameState.Playing)
                    {
                        Open();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_characterStatsButton != null) _characterStatsButton.onClick.RemoveListener(OnCharacterStatsClicked);
            if (_skillListButton != null) _skillListButton.onClick.RemoveListener(OnSkillListClicked);
            if (_weaponListButton != null) _weaponListButton.onClick.RemoveListener(OnWeaponListClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        #endregion

        #region ─── Open / Close ─────────────────────────

        /// <summary>
        /// 打开暂停菜单
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.PauseGame();
            _isOpen = true;

            HideAllSubPanels();
            StartCoroutine(OpenAnimation());
        }

        /// <summary>
        /// 关闭暂停菜单
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            HideAllSubPanels();
            StartCoroutine(CloseAnimation(() =>
            {
                GameManager.Instance?.ResumeGame();
            }));
        }

        private IEnumerator OpenAnimation()
        {
            if (_rootCanvasGroup == null) yield break;

            _rootCanvasGroup.interactable = true;
            _rootCanvasGroup.blocksRaycasts = true;

            // 弹出动画 0.3s ease-out
            float elapsed = 0f;
            while (elapsed < _openDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _openCurve.Evaluate(Mathf.Clamp01(elapsed / _openDuration));

                _rootCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

                if (_menuPanel != null)
                    _menuPanel.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, t);

                yield return null;
            }

            _rootCanvasGroup.alpha = 1f;
            if (_menuPanel != null)
                _menuPanel.localScale = Vector3.one;
        }

        private IEnumerator CloseAnimation(System.Action onComplete = null)
        {
            if (_rootCanvasGroup == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            // 关闭动画 0.2s ease-in
            float elapsed = 0f;
            float startAlpha = _rootCanvasGroup.alpha;
            Vector3 startScale = _menuPanel != null ? _menuPanel.localScale : Vector3.one;

            while (elapsed < _closeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _closeCurve.Evaluate(Mathf.Clamp01(elapsed / _closeDuration));

                _rootCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

                if (_menuPanel != null)
                    _menuPanel.localScale = Vector3.Lerp(startScale, Vector3.one * 0.85f, t);

                yield return null;
            }

            _rootCanvasGroup.alpha = 0f;
            _rootCanvasGroup.interactable = false;
            _rootCanvasGroup.blocksRaycasts = false;

            if (_menuPanel != null)
                _menuPanel.localScale = Vector3.one;

            onComplete?.Invoke();
        }

        #endregion

        #region ─── Button Handlers ──────────────────────

        private void OnResumeClicked()
        {
            Close();
        }

        private void OnCharacterStatsClicked()
        {
            HideAllSubPanels();
            ShowCharacterStats();
        }

        private void OnSkillListClicked()
        {
            HideAllSubPanels();
            ShowSkillList();
        }

        private void OnWeaponListClicked()
        {
            HideAllSubPanels();
            ShowWeaponList();
        }

        private void OnSettingsClicked()
        {
            HideAllSubPanels();
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(true);
                _activeSubPanel = _settingsPanel;
            }
        }

        private void OnQuitClicked()
        {
            // 放弃并退出
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.GameOver("PlayerQuit");
            }
            Close();

            // 返回主菜单
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        #endregion

        #region ─── Sub Panel Management ─────────────────

        private void HideAllSubPanels()
        {
            if (_characterStatsPanel != null) _characterStatsPanel.SetActive(false);
            if (_skillListPanel != null) _skillListPanel.SetActive(false);
            if (_weaponListPanel != null) _weaponListPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            _activeSubPanel = null;
        }

        #endregion

        #region ─── Character Stats Display ──────────────

        private void ShowCharacterStats()
        {
            if (_characterStatsPanel == null) return;
            _characterStatsPanel.SetActive(true);
            _activeSubPanel = _characterStatsPanel;

            // 清空旧数据
            if (_angelStatsContainer != null)
            {
                foreach (Transform child in _angelStatsContainer)
                    Destroy(child.gameObject);
            }
            if (_babyStatsContainer != null)
            {
                foreach (Transform child in _babyStatsContainer)
                    Destroy(child.gameObject);
            }

            // 显示天使属性（20个可见属性）
            var angelAttr = FindObjectOfType<AngelAttributes>();
            if (angelAttr != null && _angelStatsContainer != null)
            {
                var angelStats = angelAttr.GetVisibleStatsSnapshot();
                foreach (var kvp in angelStats)
                {
                    CreateStatRow(_angelStatsContainer, FormatStatName(kvp.Key), $"{kvp.Value:F1}");
                }
            }

            // 显示婴儿属性（13个可见属性）
            var babyAttr = FindObjectOfType<BabyAttributes>();
            if (babyAttr != null && _babyStatsContainer != null)
            {
                var babyStats = babyAttr.GetStatsSnapshot();
                foreach (var kvp in babyStats)
                {
                    CreateStatRow(_babyStatsContainer, FormatStatName(kvp.Key), $"{kvp.Value:F1}");
                }
            }

            // 当前情感状态
            var emotion = FindObjectOfType<EmotionStateMachine>();
            if (emotion != null && _currentEmotionText != null)
            {
                _currentEmotionText.text = $"当前情感: {emotion.CurrentStateName}";
            }
        }

        private void CreateStatRow(Transform parent, string name, string value)
        {
            if (_statRowPrefab == null || parent == null) return;

            var go = Instantiate(_statRowPrefab, parent);
            var nameText = go.transform.Find("StatName")?.GetComponent<TMP_Text>();
            var valueText = go.transform.Find("StatValue")?.GetComponent<TMP_Text>();

            if (nameText != null) nameText.text = name;
            if (valueText != null) valueText.text = value;
        }

        private string FormatStatName(string camelCase)
        {
            // 简单驼峰命名格式化
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < camelCase.Length; i++)
            {
                if (i > 0 && char.IsUpper(camelCase[i]))
                    sb.Append(' ');
                sb.Append(camelCase[i]);
            }
            return sb.ToString();
        }

        #endregion

        #region ─── Skill List Display ───────────────────

        private void ShowSkillList()
        {
            if (_skillListPanel == null) return;
            _skillListPanel.SetActive(true);
            _activeSubPanel = _skillListPanel;

            // 清空旧数据
            if (_skillListContainer != null)
            {
                foreach (Transform child in _skillListContainer)
                    Destroy(child.gameObject);
            }

            var cm = CardManager.Instance;
            if (cm == null) return;

            var cards = cm.GetAllCards();
            if (_skillCountText != null)
                _skillCountText.text = $"持有卡牌: {cards.Count}/{cm.maxCards}";

            foreach (var card in cards)
            {
                if (_skillItemPrefab != null && _skillListContainer != null)
                {
                    var go = Instantiate(_skillItemPrefab, _skillListContainer);
                    var nameText = go.transform.Find("CardName")?.GetComponent<TMP_Text>();
                    var levelText = go.transform.Find("CardLevel")?.GetComponent<TMP_Text>();
                    var categoryImg = go.transform.Find("CategoryBadge")?.GetComponent<Image>();

                    if (nameText != null)
                        nameText.text = card.cardData?.cardName ?? card.cardId;
                    if (levelText != null)
                        levelText.text = $"Lv.{card.currentLevel}/{card.cardData?.maxLevel}";
                }
            }
        }

        #endregion

        #region ─── Weapon List Display ──────────────────

        private void ShowWeaponList()
        {
            if (_weaponListPanel == null) return;
            _weaponListPanel.SetActive(true);
            _activeSubPanel = _weaponListPanel;

            // 清空旧数据
            if (_weaponListContainer != null)
            {
                foreach (Transform child in _weaponListContainer)
                    Destroy(child.gameObject);
            }

            var wm = WeaponManager.Instance;
            if (wm == null) return;

            var weapons = wm.GetAllWeapons();
            foreach (var weapon in weapons)
            {
                if (_weaponItemPrefab != null && _weaponListContainer != null)
                {
                    var go = Instantiate(_weaponItemPrefab, _weaponListContainer);
                    var nameText = go.transform.Find("WeaponName")?.GetComponent<TMP_Text>();
                    var rarityText = go.transform.Find("Rarity")?.GetComponent<TMP_Text>();
                    var dpsText = go.transform.Find("DPS")?.GetComponent<TMP_Text>();
                    var growthText = go.transform.Find("GrowthStage")?.GetComponent<TMP_Text>();

                    if (nameText != null)
                        nameText.text = weapon.weaponName;
                    if (rarityText != null)
                        rarityText.text = weapon.EffectiveRarity.ToString();
                    if (dpsText != null)
                        dpsText.text = $"DPS: {weapon.EffectiveDamage / Mathf.Max(weapon.attackInterval, 0.1f):F0}";
                    if (growthText != null)
                        growthText.text = $"成长: {weapon.Growth}";
                }
            }
        }

        #endregion
    }
}
