using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using AngelGuardian.Core;

namespace AngelGuardian.UI
{
    /// <summary>
    /// 主菜单 —— 游戏入口界面。
    /// 包含Logo动画（天使翅膀扇动循环）、导航按钮、
    /// Meta Progression总览展示、版本号显示。
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static MainMenu _instance;
        public static MainMenu Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<MainMenu>();
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
        [SerializeField] private Canvas _mainCanvas;
        [SerializeField] private CanvasGroup _rootCanvasGroup;

        #endregion

        #region ─── Inspector: Logo Animation ────────────

        [Header("Logo Animation")]
        [SerializeField] private RectTransform _logoTransform;
        [SerializeField] private Image _leftWing;
        [SerializeField] private Image _rightWing;
        [SerializeField] private float _wingFlapSpeed = 1.5f;
        [SerializeField] private float _wingFlapAmplitude = 15f;
        [SerializeField] private float _logoFloatAmplitude = 8f;
        [SerializeField] private float _logoFloatSpeed = 0.8f;

        #endregion

        #region ─── Inspector: Navigation Buttons ────────

        [Header("Navigation Buttons")]
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _characterStatsButton;
        [SerializeField] private Button _cardGalleryButton;
        [SerializeField] private Button _weaponGalleryButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _achievementsButton;

        [Header("Button Animation")]
        [SerializeField] private float _buttonSlideInDelay = 0.1f;
        [SerializeField] private float _buttonSlideInDuration = 0.4f;

        #endregion

        #region ─── Inspector: Meta Progression Overview ─

        [Header("Meta Progression Overview")]
        [SerializeField] private TMP_Text _soulShardsText;
        [SerializeField] private TMP_Text _angelTearsText;
        [SerializeField] private TMP_Text _totalUpgradesText;
        [SerializeField] private TMP_Text _playerLevelText;
        [SerializeField] private Slider _totalProgressionSlider;
        [SerializeField] private GameObject _metaProgressionPanel;

        #endregion

        #region ─── Inspector: Sub Panels ────────────────

        [Header("Sub Panels")]
        [SerializeField] private GameObject _characterStatsPanel;
        [SerializeField] private GameObject _cardGalleryPanel;
        [SerializeField] private GameObject _weaponGalleryPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _achievementsPanel;

        #endregion

        #region ─── Inspector: Version ───────────────────

        [Header("Version")]
        [SerializeField] private TMP_Text _versionText;
        [SerializeField] private string _versionString = "v1.0.0";

        #endregion

        #region ─── Inspector: Background ────────────────

        [Header("Background")]
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private float _bgParallaxStrength = 0.02f;

        #endregion

        #region ─── Unity Lifecycle ──────────────────────

        private void Start()
        {
            // 确保游戏时间正常
            Time.timeScale = 1f;

            // 版本号
            if (_versionText != null)
                _versionText.text = _versionString;

            // 隐藏所有子面板
            HideAllSubPanels();

            // 绑定按钮
            if (_startGameButton != null)
                _startGameButton.onClick.AddListener(OnStartGameClicked);
            if (_characterStatsButton != null)
                _characterStatsButton.onClick.AddListener(OnCharacterStatsClicked);
            if (_cardGalleryButton != null)
                _cardGalleryButton.onClick.AddListener(OnCardGalleryClicked);
            if (_weaponGalleryButton != null)
                _weaponGalleryButton.onClick.AddListener(OnWeaponGalleryClicked);
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_achievementsButton != null)
                _achievementsButton.onClick.AddListener(OnAchievementsClicked);

            // 刷新Meta进度
            RefreshMetaProgression();

            // 按钮入场动画
            StartCoroutine(ButtonsSlideIn());
        }

        private void Update()
        {
            // Logo动画：翅膀扇动循环
            AnimateLogo();

            // 背景视差
            AnimateBackground();
        }

        private void OnDestroy()
        {
            if (_startGameButton != null) _startGameButton.onClick.RemoveListener(OnStartGameClicked);
            if (_characterStatsButton != null) _characterStatsButton.onClick.RemoveListener(OnCharacterStatsClicked);
            if (_cardGalleryButton != null) _cardGalleryButton.onClick.RemoveListener(OnCardGalleryClicked);
            if (_weaponGalleryButton != null) _weaponGalleryButton.onClick.RemoveListener(OnWeaponGalleryClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (_achievementsButton != null) _achievementsButton.onClick.RemoveListener(OnAchievementsClicked);
        }

        #endregion

        #region ─── Logo Animation ───────────────────────

        private void AnimateLogo()
        {
            // 翅膀扇动
            if (_leftWing != null)
            {
                float leftAngle = Mathf.Sin(Time.time * _wingFlapSpeed * Mathf.PI * 2f) * _wingFlapAmplitude;
                _leftWing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, leftAngle);
            }

            if (_rightWing != null)
            {
                float rightAngle = -Mathf.Sin(Time.time * _wingFlapSpeed * Mathf.PI * 2f) * _wingFlapAmplitude;
                _rightWing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rightAngle);
            }

            // Logo上下浮动
            if (_logoTransform != null)
            {
                float floatOffset = Mathf.Sin(Time.time * _logoFloatSpeed * Mathf.PI * 2f) * _logoFloatAmplitude;
                _logoTransform.anchoredPosition = new Vector2(
                    _logoTransform.anchoredPosition.x,
                    floatOffset);
            }
        }

        private void AnimateBackground()
        {
            if (_backgroundImage == null) return;

            // 鼠标视差效果
            Vector2 mousePos = Input.mousePosition;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 offset = (mousePos - screenCenter) * _bgParallaxStrength;

            _backgroundImage.rectTransform.anchoredPosition = offset;
        }

        #endregion

        #region ─── Button Animations ────────────────────

        private IEnumerator ButtonsSlideIn()
        {
            Button[] buttons = { _startGameButton, _characterStatsButton, _cardGalleryButton,
                                 _weaponGalleryButton, _settingsButton, _achievementsButton };

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                var rt = buttons[i].GetComponent<RectTransform>();
                if (rt == null) continue;

                // 初始位置（从右方滑入）
                Vector2 targetPos = rt.anchoredPosition;
                rt.anchoredPosition = targetPos + new Vector2(200f, 0f);

                // 透明
                var cg = buttons[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;

                StartCoroutine(SlideInButton(rt, cg, targetPos, _buttonSlideInDuration));
                yield return new WaitForSecondsRealtime(_buttonSlideInDelay);
            }
        }

        private IEnumerator SlideInButton(RectTransform rt, CanvasGroup cg, Vector2 targetPos, float duration)
        {
            Vector2 startPos = rt.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // Ease-out cubic
                t = 1f - Mathf.Pow(1f - t, 3f);

                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                if (cg != null)
                    cg.alpha = Mathf.Lerp(0f, 1f, t);

                yield return null;
            }

            rt.anchoredPosition = targetPos;
            if (cg != null) cg.alpha = 1f;
        }

        #endregion

        #region ─── Button Handlers ──────────────────────

        private void OnStartGameClicked()
        {
            // 场景过渡效果（简化：直接加载）
            StartCoroutine(StartGameTransition());
        }

        private IEnumerator StartGameTransition()
        {
            // 可选：淡出效果
            if (_rootCanvasGroup != null)
            {
                float elapsed = 0f;
                float duration = 0.5f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _rootCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                    yield return null;
                }
            }

            // 加载游戏场景
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        }

        private void OnCharacterStatsClicked()
        {
            HideAllSubPanels();
            if (_characterStatsPanel != null)
                _characterStatsPanel.SetActive(true);
            RefreshMetaProgression();
        }

        private void OnCardGalleryClicked()
        {
            HideAllSubPanels();
            if (_cardGalleryPanel != null)
                _cardGalleryPanel.SetActive(true);
            PopulateCardGallery();
        }

        private void OnWeaponGalleryClicked()
        {
            HideAllSubPanels();
            if (_weaponGalleryPanel != null)
                _weaponGalleryPanel.SetActive(true);
            PopulateWeaponGallery();
        }

        private void OnSettingsClicked()
        {
            HideAllSubPanels();
            if (_settingsPanel != null)
                _settingsPanel.SetActive(true);
        }

        private void OnAchievementsClicked()
        {
            HideAllSubPanels();
            if (_achievementsPanel != null)
                _achievementsPanel.SetActive(true);
        }

        #endregion

        #region ─── Meta Progression ─────────────────────

        private void RefreshMetaProgression()
        {
            var meta = MetaProgression.Instance;
            if (meta == null) return;

            if (_soulShardsText != null)
                _soulShardsText.text = $"灵魂碎片: {meta.SoulShards}";

            if (_angelTearsText != null)
                _angelTearsText.text = $"天使之泪: {meta.AngelTears}";

            int totalUpgrades =
                meta.GetUpgradeLevel(MetaProgression.UpgradeType.InitialMentalBonus)
                + meta.GetUpgradeLevel(MetaProgression.UpgradeType.InitialWeaponSlot)
                + meta.GetUpgradeLevel(MetaProgression.UpgradeType.BaseLuckBonus)
                + meta.GetUpgradeLevel(MetaProgression.UpgradeType.CardRerollCount)
                + meta.GetUpgradeLevel(MetaProgression.UpgradeType.WeaponRerollFree)
                + meta.GetUpgradeLevel(MetaProgression.UpgradeType.ExtraAttributes);

            if (_totalUpgradesText != null)
                _totalUpgradesText.text = $"总升级数: {totalUpgrades}";

            // 总进度（简化：假设最大升级数为60）
            if (_totalProgressionSlider != null)
            {
                _totalProgressionSlider.maxValue = 60;
                _totalProgressionSlider.value = totalUpgrades;
            }

            // 玩家等级（基于灵魂碎片推算）
            int playerLevel = 1 + meta.SoulShards / 100;
            if (_playerLevelText != null)
                _playerLevelText.text = $"守护者等级: {playerLevel}";
        }

        #endregion

        #region ─── Card Gallery ─────────────────────────

        private void PopulateCardGallery()
        {
            // 由CardGallery子组件处理
            var gallery = _cardGalleryPanel?.GetComponent<CardGalleryUI>();
            gallery?.Refresh();
        }

        #endregion

        #region ─── Weapon Gallery ───────────────────────

        private void PopulateWeaponGallery()
        {
            // 由WeaponGallery子组件处理
            var gallery = _weaponGalleryPanel?.GetComponent<WeaponGalleryUI>();
            gallery?.Refresh();
        }

        #endregion

        #region ─── Sub Panel Management ─────────────────

        private void HideAllSubPanels()
        {
            if (_characterStatsPanel != null) _characterStatsPanel.SetActive(false);
            if (_cardGalleryPanel != null) _cardGalleryPanel.SetActive(false);
            if (_weaponGalleryPanel != null) _weaponGalleryPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_achievementsPanel != null) _achievementsPanel.SetActive(false);
        }

        #endregion

        #region ─── Public API ───────────────────────────

        /// <summary>
        /// 从其他场景返回时刷新主菜单
        /// </summary>
        public void RefreshOnReturn()
        {
            RefreshMetaProgression();
            HideAllSubPanels();

            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 1f;
                _rootCanvasGroup.interactable = true;
                _rootCanvasGroup.blocksRaycasts = true;
            }

            // 重新播放按钮动画
            StartCoroutine(ButtonsSlideIn());
        }

        #endregion
    }

    /// <summary>
    /// 卡牌图鉴UI（由MainMenu的子面板使用）
    /// </summary>
    public class CardGalleryUI : MonoBehaviour
    {
        [SerializeField] private Transform _cardGridContainer;
        [SerializeField] private GameObject _cardThumbnailPrefab;
        [SerializeField] private TMP_Text _collectionProgressText;

        public void Refresh()
        {
            var db = Cards.CardManager.Instance?.CardDatabase;
            if (db == null) return;

            // 清空
            if (_cardGridContainer != null)
            {
                foreach (Transform child in _cardGridContainer)
                    Destroy(child.gameObject);
            }

            int collected = 0;
            foreach (var card in db.cards)
            {
                if (_cardThumbnailPrefab != null && _cardGridContainer != null)
                {
                    var go = Instantiate(_cardThumbnailPrefab, _cardGridContainer);
                    var nameText = go.transform.Find("CardName")?.GetComponent<TMP_Text>();
                    var rarityBorder = go.transform.Find("RarityBorder")?.GetComponent<Image>();

                    if (nameText != null)
                        nameText.text = card.cardName;
                    if (rarityBorder != null)
                        rarityBorder.color = CardSelectUI.CardSelectPanel.GetRarityColor(card.rarity);
                }
                collected++;
            }

            if (_collectionProgressText != null)
                _collectionProgressText.text = $"收集进度: {collected}/{db.cards.Count}";
        }
    }

    /// <summary>
    /// 武器图鉴UI（由MainMenu的子面板使用）
    /// </summary>
    public class WeaponGalleryUI : MonoBehaviour
    {
        [SerializeField] private Transform _weaponGridContainer;
        [SerializeField] private GameObject _weaponThumbnailPrefab;
        [SerializeField] private TMP_Text _collectionProgressText;

        public void Refresh()
        {
            var db = Weapons.WeaponManager.Instance?.WeaponDatabase;
            if (db == null) return;

            // 清空
            if (_weaponGridContainer != null)
            {
                foreach (Transform child in _weaponGridContainer)
                    Destroy(child.gameObject);
            }

            int collected = 0;
            foreach (var weapon in db.weapons)
            {
                if (_weaponThumbnailPrefab != null && _weaponGridContainer != null)
                {
                    var go = Instantiate(_weaponThumbnailPrefab, _weaponGridContainer);
                    var nameText = go.transform.Find("WeaponName")?.GetComponent<TMP_Text>();
                    var typeText = go.transform.Find("WeaponType")?.GetComponent<TMP_Text>();
                    var dpsText = go.transform.Find("DPS")?.GetComponent<TMP_Text>();

                    if (nameText != null) nameText.text = weapon.weaponName;
                    if (typeText != null) typeText.text = weapon.type.ToString();
                    if (dpsText != null) dpsText.text = $"DPS: {weapon.baseDPS:F0}";
                }
                collected++;
            }

            if (_collectionProgressText != null)
                _collectionProgressText.text = $"收集进度: {collected}/{db.weapons.Count}";
        }
    }
}
