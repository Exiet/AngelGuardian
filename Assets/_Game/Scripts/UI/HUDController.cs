using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using AngelGuardian.Core;
using AngelGuardian.Baby;
using AngelGuardian.Weapons;
using AngelGuardian.Cards;

namespace AngelGuardian.UI
{
    /// <summary>
    /// 战斗内HUD控制器 —— 管理所有战斗界面的显示与交互。
    /// 包括精神力条、波次指示器、武器栏、卡牌状态、连携指示器、
    /// 婴儿情感状态图标、暂停按钮和移动端虚拟摇杆。
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static HUDController _instance;
        public static HUDController Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<HUDController>();
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

        #region ─── Inspector: Canvas & CanvasGroup ───────

        [Header("Root Canvas")]
        [SerializeField] private Canvas _mainCanvas;
        [SerializeField] private CanvasGroup _hudCanvasGroup;

        #endregion

        #region ─── Inspector: Mental HP Bar ─────────────

        [Header("Mental HP Bar")]
        [SerializeField] private Slider _mentalHPSlider;
        [SerializeField] private Image _mentalHPFill;
        [SerializeField] private TMP_Text _mentalHPText;
        [SerializeField] private TMP_Text _mentalHPLabel;

        [Header("Mental HP Colors")]
        [SerializeField] private Color _hpGreen = new Color(0.3f, 0.85f, 0.3f);
        [SerializeField] private Color _hpYellow = new Color(0.95f, 0.85f, 0.2f);
        [SerializeField] private Color _hpRed = new Color(0.9f, 0.2f, 0.15f);
        [SerializeField] private float _flashInterval = 0.3f;
        [SerializeField] private float _alertThreshold = 5f; // 归零前5秒开始闪烁

        private bool _isFlashing;
        private Coroutine _flashCoroutine;
        private bool _alertActive;

        #endregion

        #region ─── Inspector: Wave Indicator ────────────

        [Header("Wave Indicator")]
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private CanvasGroup _waveAnnounceGroup;
        [SerializeField] private TMP_Text _waveAnnounceText;
        [SerializeField] private float _waveAnnounceDuration = 1.5f;

        #endregion

        #region ─── Inspector: Weapon Bar ────────────────

        [Header("Weapon Bar")]
        [SerializeField] private Transform _weaponSlotParent;
        [SerializeField] private GameObject _weaponSlotPrefab;
        [SerializeField] private int _maxWeaponSlots = 6;

        private List<WeaponSlotUI> _weaponSlots = new List<WeaponSlotUI>();
        private int _currentActiveWeaponIndex = 0;

        [System.Serializable]
        public class WeaponSlotUI
        {
            public GameObject root;
            public Image icon;
            public Image border;
            public Image rarityGlow;
            public CanvasGroup canvasGroup;

            public void SetActive(bool active, Color borderColor)
            {
                border.color = active ? borderColor : Color.clear;
                if (rarityGlow != null)
                    rarityGlow.gameObject.SetActive(active);
            }

            public void SetIcon(Sprite sprite)
            {
                if (icon != null && sprite != null)
                    icon.sprite = sprite;
            }

            public void SetVisible(bool visible)
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = visible ? 1f : 0.3f;
                else if (root != null)
                    root.SetActive(visible);
            }

            public void SetFlash(bool flash)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = flash ? 0.5f + Mathf.PingPong(Time.time * 3f, 0.5f) : 1f;
                }
            }
        }

        #endregion

        #region ─── Inspector: Card Status Bar ───────────

        [Header("Card Status Bar")]
        [SerializeField] private TMP_Text _cardCountText;
        [SerializeField] private Image _cardWarningIcon;
        [SerializeField] private Color _cardNormalColor = Color.white;
        [SerializeField] private Color _cardFullColor = new Color(0.95f, 0.25f, 0.2f);

        #endregion

        #region ─── Inspector: Combo Indicator ───────────

        [Header("Combo Indicator")]
        [SerializeField] private CanvasGroup _comboGroup;
        [SerializeField] private TMP_Text _comboNameText;
        [SerializeField] private Slider _comboTimerSlider;
        [SerializeField] private Image _comboTimerFill;

        private Coroutine _comboTimerCoroutine;
        private float _comboRemainingTime;

        #endregion

        #region ─── Inspector: Baby Emotion Icon ─────────

        [Header("Baby Emotion Icon")]
        [SerializeField] private Image _emotionIcon;
        [SerializeField] private Sprite _curiousIcon;    // ?
        [SerializeField] private Sprite _fearIcon;        // ?
        [SerializeField] private Sprite _angerIcon;       // ?
        [SerializeField] private Sprite _tiredIcon;       // ?
        [SerializeField] private Sprite _awakeningIcon;   // ✨

        #endregion

        #region ─── Inspector: Pause Button ──────────────

        [Header("Pause Button")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private GameObject _pauseMenuPrefab;

        #endregion

        #region ─── Inspector: Virtual Joystick ──────────

        [Header("Virtual Joystick (Mobile)")]
        [SerializeField] private RectTransform _joystickRoot;
        [SerializeField] private RectTransform _joystickBackground;
        [SerializeField] private RectTransform _joystickHandle;
        [SerializeField] private float _joystickMaxRadius = 80f;

        private Vector2 _joystickInput;
        private bool _joystickActive;
        private int _joystickTouchId = -1;

        /// <summary>归一化的摇杆输入向量 (-1到1)</summary>
        public Vector2 JoystickInput => _joystickInput;

        #endregion

        #region ─── Inspector: Combo Accumulator ─────────

        [Header("Combo Accumulator")]
        [SerializeField] private Slider _comboAccumulatorSlider;
        [SerializeField] private Image _comboAccumulatorFill;

        #endregion

        #region ─── Unity Lifecycle ──────────────────────

        private void Start()
        {
            SubscribeEvents();
            InitializeWeaponSlots();
            InitializeJoystick();

            // 初始隐藏波次提示
            if (_waveAnnounceGroup != null)
                _waveAnnounceGroup.alpha = 0f;

            // 初始隐藏连携指示器
            if (_comboGroup != null)
                _comboGroup.alpha = 0f;

            // 检测移动端
            bool isMobile = Application.isMobilePlatform;
            if (_joystickRoot != null)
                _joystickRoot.gameObject.SetActive(isMobile);

            // 绑定暂停按钮
            if (_pauseButton != null)
                _pauseButton.onClick.AddListener(OnPauseClicked);

            // 初始化卡牌状态
            UpdateCardStatus();
            UpdateMentalHPDisplay();
        }

        private void Update()
        {
            // 更新连携计时器
            if (_comboRemainingTime > 0f)
            {
                _comboRemainingTime -= Time.unscaledDeltaTime;
                if (_comboTimerSlider != null)
                    _comboTimerSlider.value = Mathf.Max(0f, _comboRemainingTime);

                if (_comboRemainingTime <= 0f)
                {
                    HideComboIndicator();
                }
            }

            // 精神力警报闪烁
            if (_alertActive)
            {
                UpdateMentalHPFlash();
            }

            // 武器栏闪烁（连携触发时）
            UpdateWeaponSlotFlash();

            // 移动端摇杆
            if (_joystickActive)
            {
                UpdateJoystick();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (_pauseButton != null)
                _pauseButton.onClick.RemoveListener(OnPauseClicked);
        }

        #endregion

        #region ─── Event Subscriptions ──────────────────

        private void SubscribeEvents()
        {
            if (EventBus.Instance == null) return;

            EventBus.Instance.OnBabyHurt.AddListener(OnBabyHurt);
            EventBus.Instance.OnBabyMentalZero.AddListener(OnBabyMentalZero);
            EventBus.Instance.OnWaveStart.AddListener(OnWaveStart);
            EventBus.Instance.OnComboTriggered.AddListener(OnComboTriggered);
            EventBus.Instance.OnBabyEmotionChanged.AddListener(OnBabyEmotionChanged);
            EventBus.Instance.OnWeaponPickedUp.AddListener(OnWeaponChanged);
            EventBus.Instance.OnCardPickedUp.AddListener(OnCardChanged);
            EventBus.Instance.OnLevelUp.AddListener(OnLevelUp);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += OnGameStateChanged;
            }

            // 连携累加器
            var comboSystem = ComboSystem.Instance;
            if (comboSystem != null)
            {
                comboSystem.OnAccumulatorChanged += OnComboAccumulatorChanged;
            }

            // 武器切换
            var weaponManager = WeaponManager.Instance;
            if (weaponManager != null)
            {
                weaponManager.OnWeaponSwitched += OnWeaponSwitched;
                weaponManager.OnWeaponAdded += OnWeaponAddedLocal;
                weaponManager.OnWeaponReplaced += OnWeaponReplacedLocal;
            }

            // 卡牌变更
            var cardManager = CardManager.Instance;
            if (cardManager != null)
            {
                cardManager.OnCardAdded += OnCardAddedLocal;
                cardManager.OnCardUpgraded += OnCardUpgradedLocal;
                cardManager.OnCardRemoved += OnCardRemovedLocal;
            }
        }

        private void UnsubscribeEvents()
        {
            if (EventBus.Instance == null) return;

            EventBus.Instance.OnBabyHurt.RemoveListener(OnBabyHurt);
            EventBus.Instance.OnBabyMentalZero.RemoveListener(OnBabyMentalZero);
            EventBus.Instance.OnWaveStart.RemoveListener(OnWaveStart);
            EventBus.Instance.OnComboTriggered.RemoveListener(OnComboTriggered);
            EventBus.Instance.OnBabyEmotionChanged.RemoveListener(OnBabyEmotionChanged);
            EventBus.Instance.OnWeaponPickedUp.RemoveListener(OnWeaponChanged);
            EventBus.Instance.OnCardPickedUp.RemoveListener(OnCardChanged);
            EventBus.Instance.OnLevelUp.RemoveListener(OnLevelUp);

            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;

            var comboSystem = ComboSystem.Instance;
            if (comboSystem != null)
                comboSystem.OnAccumulatorChanged -= OnComboAccumulatorChanged;

            var weaponManager = WeaponManager.Instance;
            if (weaponManager != null)
            {
                weaponManager.OnWeaponSwitched -= OnWeaponSwitched;
                weaponManager.OnWeaponAdded -= OnWeaponAddedLocal;
                weaponManager.OnWeaponReplaced -= OnWeaponReplacedLocal;
            }

            var cardManager = CardManager.Instance;
            if (cardManager != null)
            {
                cardManager.OnCardAdded -= OnCardAddedLocal;
                cardManager.OnCardUpgraded -= OnCardUpgradedLocal;
                cardManager.OnCardRemoved -= OnCardRemovedLocal;
            }
        }

        #endregion

        #region ─── Mental HP Bar ────────────────────────

        private void OnBabyHurt(float damage, float currentMentalHP)
        {
            UpdateMentalHPDisplay();
        }

        private void OnBabyMentalZero()
        {
            UpdateMentalHPDisplay();
            StopMentalHPFlash();
        }

        private void UpdateMentalHPDisplay()
        {
            var babyAttr = FindObjectOfType<BabyAttributes>();
            if (babyAttr == null) return;

            float maxHP = babyAttr.babyMaxMentalPower;
            float currentHP = babyAttr.CurrentMentalHP;
            float percent = babyAttr.MentalHPPercent;

            // 更新滑条
            if (_mentalHPSlider != null)
            {
                _mentalHPSlider.maxValue = maxHP;
                _mentalHPSlider.value = currentHP;
            }

            // 更新文字
            if (_mentalHPText != null)
                _mentalHPText.text = $"{currentHP:F0} / {maxHP:F0}";

            // 颜色：绿(100-70%)→黄(70-40%)→红(40-0%)
            if (_mentalHPFill != null)
            {
                if (percent > 0.7f)
                    _mentalHPFill.color = _hpGreen;
                else if (percent > 0.4f)
                    _mentalHPFill.color = _hpYellow;
                else
                    _mentalHPFill.color = _hpRed;
            }

            // 归零前5秒红色闪烁+警报
            float timeToZero = percent > 0f ? percent * maxHP / Mathf.Max(babyAttr.BabyRegenPerSec > 0f ? -babyAttr.BabyRegenPerSec : 1f, 0.001f) : 0f;

            // 简化判断：精神力<40%时开始关注，<20%时警报
            if (percent < 0.4f && percent > 0f)
            {
                if (!_alertActive)
                {
                    _alertActive = true;
                    StartMentalHPFlash();
                }
            }
            else
            {
                if (_alertActive)
                {
                    _alertActive = false;
                    StopMentalHPFlash();
                }
            }
        }

        private void StartMentalHPFlash()
        {
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(MentalHPFlashRoutine());
        }

        private void StopMentalHPFlash()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }
            _isFlashing = false;
            if (_mentalHPFill != null)
                _mentalHPFill.color = _hpRed;
        }

        private IEnumerator MentalHPFlashRoutine()
        {
            while (_alertActive)
            {
                _isFlashing = !_isFlashing;
                if (_mentalHPFill != null)
                {
                    _mentalHPFill.color = _isFlashing ? _hpRed : new Color(_hpRed.r, _hpRed.g, _hpRed.b, 0.4f);
                }
                if (_mentalHPSlider != null && _mentalHPSlider.fillRect != null)
                {
                    var fillImg = _mentalHPSlider.fillRect.GetComponent<Image>();
                    if (fillImg != null)
                        fillImg.color = _isFlashing ? _hpRed : new Color(_hpRed.r, _hpRed.g, _hpRed.b, 0.4f);
                }
                yield return new WaitForSecondsRealtime(_flashInterval);
            }
        }

        private void UpdateMentalHPFlash()
        {
            // 在Update中不做额外处理，由协程控制
        }

        #endregion

        #region ─── Wave Indicator ───────────────────────

        private void OnWaveStart(int waveNumber)
        {
            UpdateWaveText();
            ShowWaveAnnouncement(waveNumber);
        }

        private void UpdateWaveText()
        {
            if (_waveText == null) return;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                _waveText.text = $"WAVE {gm.CurrentWave} / {GameManager.TotalWaves}";
            }
        }

        private void ShowWaveAnnouncement(int waveNumber)
        {
            if (_waveAnnounceGroup == null || _waveAnnounceText == null) return;

            _waveAnnounceText.text = $"WAVE {waveNumber}";
            StopAllCoroutines();
            StartCoroutine(WaveAnnounceRoutine());
        }

        private IEnumerator WaveAnnounceRoutine()
        {
            // 淡入
            float elapsed = 0f;
            float fadeInDuration = 0.4f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _waveAnnounceGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }

            // 保持
            yield return new WaitForSecondsRealtime(_waveAnnounceDuration - fadeInDuration * 2);

            // 淡出
            elapsed = 0f;
            float fadeOutDuration = 0.4f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _waveAnnounceGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            _waveAnnounceGroup.alpha = 0f;
        }

        #endregion

        #region ─── Weapon Bar ───────────────────────────

        private void InitializeWeaponSlots()
        {
            if (_weaponSlotParent == null || _weaponSlotPrefab == null) return;

            // 清空已有
            foreach (Transform child in _weaponSlotParent)
            {
                Destroy(child.gameObject);
            }
            _weaponSlots.Clear();

            for (int i = 0; i < _maxWeaponSlots; i++)
            {
                var go = Instantiate(_weaponSlotPrefab, _weaponSlotParent);
                var slot = new WeaponSlotUI
                {
                    root = go,
                    icon = go.transform.Find("Icon")?.GetComponent<Image>(),
                    border = go.transform.Find("Border")?.GetComponent<Image>(),
                    rarityGlow = go.transform.Find("RarityGlow")?.GetComponent<Image>(),
                    canvasGroup = go.GetComponent<CanvasGroup>()
                };
                slot.SetVisible(i == 0); // 初始只有第0个可见
                _weaponSlots.Add(slot);
            }
        }

        private void RefreshWeaponBar()
        {
            var wm = WeaponManager.Instance;
            if (wm == null) return;

            var weapons = wm.GetAllWeapons();
            _currentActiveWeaponIndex = wm.CurrentWeaponIndex;

            for (int i = 0; i < _weaponSlots.Count; i++)
            {
                if (i < weapons.Count)
                {
                    _weaponSlots[i].SetVisible(true);
                    // TODO: 从武器数据加载图标
                    // _weaponSlots[i].SetIcon(weapons[i].icon);
                    bool isActive = (i == _currentActiveWeaponIndex);
                    _weaponSlots[i].SetActive(isActive, GetRarityColor(weapons[i].rarity));
                }
                else
                {
                    _weaponSlots[i].SetVisible(false);
                }
            }
        }

        private Color GetRarityColor(WeaponRarity rarity)
        {
            return rarity switch
            {
                WeaponRarity.Normal => new Color(0.5f, 0.5f, 0.5f),
                WeaponRarity.Rare => new Color(0.2f, 0.5f, 1f),
                WeaponRarity.Epic => new Color(0.6f, 0.2f, 1f),
                WeaponRarity.Legendary => new Color(1f, 0.55f, 0f),
                WeaponRarity.Mythic => new Color(1f, 0.1f, 0.3f),
                _ => Color.white
            };
        }

        private Color GetActiveBorderColor()
        {
            return new Color(1f, 0.85f, 0f); // 金色边框
        }

        private void OnWeaponChanged(string weaponId)
        {
            RefreshWeaponBar();
        }

        private void OnWeaponSwitched(int index, WeaponBase weapon)
        {
            _currentActiveWeaponIndex = index;
            RefreshWeaponBar();
        }

        private void OnWeaponAddedLocal(WeaponBase weapon)
        {
            RefreshWeaponBar();
        }

        private void OnWeaponReplacedLocal(int index, WeaponBase oldWeapon, WeaponBase newWeapon)
        {
            RefreshWeaponBar();
        }

        /// <summary>
        /// 连携触发时相关武器闪烁
        /// </summary>
        private void OnComboTriggered(string comboName, float duration)
        {
            var cs = ComboSystem.Instance;
            var wm = WeaponManager.Instance;
            if (cs == null || wm == null) return;

            // 找到当前触发的连携定义
            var combo = cs.FindCombo(
                wm.GetWeaponById(cs.ComboDefinitions.Find(c => c.comboName == comboName)?.weaponA ?? "")?.weaponId ?? "",
                wm.GetWeaponById(cs.ComboDefinitions.Find(c => c.comboName == comboName)?.weaponB ?? "")?.weaponId ?? ""
            );

            if (combo != null)
            {
                // 闪烁参与连携的武器槽
                for (int i = 0; i < _weaponSlots.Count && i < wm.GetAllWeapons().Count; i++)
                {
                    var w = wm.GetAllWeapons()[i];
                    if (w.weaponId == combo.weaponA || w.weaponId == combo.weaponB)
                    {
                        _weaponSlots[i].SetFlash(true);
                        StartCoroutine(StopWeaponFlashAfterDelay(i, duration));
                    }
                }
            }

            // 显示连携指示器
            ShowComboIndicator(comboName, duration);
        }

        private IEnumerator StopWeaponFlashAfterDelay(int slotIndex, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (slotIndex < _weaponSlots.Count)
                _weaponSlots[slotIndex].SetFlash(false);
            RefreshWeaponBar(); // 恢复边框
        }

        private void UpdateWeaponSlotFlash()
        {
            // 在Update中通过SetFlash的PingPong自动处理闪烁
        }

        #endregion

        #region ─── Card Status Bar ──────────────────────

        private void UpdateCardStatus()
        {
            var cm = CardManager.Instance;
            if (cm == null) return;

            int current = cm.CardCount;
            int max = cm.maxCards;
            bool isFull = cm.IsFull;

            if (_cardCountText != null)
            {
                _cardCountText.text = $"{current}/{max}";
                _cardCountText.color = isFull ? _cardFullColor : _cardNormalColor;
            }

            if (_cardWarningIcon != null)
                _cardWarningIcon.gameObject.SetActive(isFull);
        }

        private void OnCardChanged(string cardId) => UpdateCardStatus();
        private void OnCardAddedLocal(CardInstance card) => UpdateCardStatus();
        private void OnCardUpgradedLocal(CardInstance card, int newLevel) => UpdateCardStatus();
        private void OnCardRemovedLocal(CardInstance card) => UpdateCardStatus();

        #endregion

        #region ─── Combo Indicator ──────────────────────

        private void ShowComboIndicator(string comboName, float duration)
        {
            if (_comboGroup == null) return;

            if (_comboNameText != null)
                _comboNameText.text = comboName;

            _comboRemainingTime = duration;

            if (_comboTimerSlider != null)
            {
                _comboTimerSlider.maxValue = duration;
                _comboTimerSlider.value = duration;
            }

            _comboGroup.alpha = 1f;
        }

        private void HideComboIndicator()
        {
            if (_comboGroup == null) return;
            StartCoroutine(FadeOutComboIndicator());
        }

        private IEnumerator FadeOutComboIndicator()
        {
            float elapsed = 0f;
            float duration = 0.3f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _comboGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                yield return null;
            }
            _comboGroup.alpha = 0f;
        }

        #endregion

        #region ─── Combo Accumulator ────────────────────

        private void OnComboAccumulatorChanged(float value)
        {
            if (_comboAccumulatorSlider != null)
            {
                _comboAccumulatorSlider.maxValue = ComboSystem.COMBO_TRIGGER_THRESHOLD;
                _comboAccumulatorSlider.value = value;
            }
        }

        #endregion

        #region ─── Baby Emotion Icon ────────────────────

        private void OnBabyEmotionChanged(string emotionState)
        {
            if (_emotionIcon == null) return;

            Sprite targetSprite = emotionState switch
            {
                "CURIOUS" => _curiousIcon,
                "FEAR" => _fearIcon,
                "ANGER" => _angerIcon,
                "TIRED" => _tiredIcon,
                "AWAKENING" or "AWAKENING_START" => _awakeningIcon,
                _ => _curiousIcon
            };

            if (targetSprite != null)
                _emotionIcon.sprite = targetSprite;
        }

        #endregion

        #region ─── Pause Button ─────────────────────────

        private void OnPauseClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CurrentState == GameManager.GameState.Playing)
            {
                gm.PauseGame();
                // 实例化暂停菜单
                if (_pauseMenuPrefab != null)
                {
                    Instantiate(_pauseMenuPrefab);
                }
            }
        }

        #endregion

        #region ─── Virtual Joystick (Mobile) ────────────

        private void InitializeJoystick()
        {
            if (_joystickBackground == null || _joystickHandle == null) return;

            // 添加EventTrigger或使用IPointerDownHandler等
            // 这里使用简单的触摸检测方案
            _joystickHandle.anchoredPosition = Vector2.zero;
        }

        private void UpdateJoystick()
        {
            // 检测触摸
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);

                    if (touch.phase == TouchPhase.Began)
                    {
                        // 检测是否点击在摇杆区域内
                        if (RectTransformUtility.RectangleContainsScreenPoint(_joystickBackground, touch.position))
                        {
                            _joystickTouchId = touch.fingerId;
                            _joystickActive = true;
                            _joystickHandle.anchoredPosition = Vector2.zero;
                        }
                    }
                    else if (touch.fingerId == _joystickTouchId)
                    {
                        if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                        {
                            Vector2 localPos;
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                _joystickBackground, touch.position, null, out localPos);

                            localPos = Vector2.ClampMagnitude(localPos, _joystickMaxRadius);
                            _joystickHandle.anchoredPosition = localPos;
                            _joystickInput = localPos / _joystickMaxRadius;
                        }
                        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        {
                            _joystickTouchId = -1;
                            _joystickActive = false;
                            _joystickHandle.anchoredPosition = Vector2.zero;
                            _joystickInput = Vector2.zero;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前摇杆输入（由AngelController等读取）
        /// </summary>
        public Vector2 GetMovementInput()
        {
            if (!_joystickActive)
            {
                // 回退到键盘输入
                float h = Input.GetAxis("Horizontal");
                float v = Input.GetAxis("Vertical");
                return new Vector2(h, v);
            }
            return _joystickInput;
        }

        #endregion

        #region ─── Game State ───────────────────────────

        private void OnGameStateChanged(GameManager.GameState oldState, GameManager.GameState newState)
        {
            if (newState == GameManager.GameState.Playing)
            {
                if (_hudCanvasGroup != null)
                    _hudCanvasGroup.alpha = 1f;
                UpdateWaveText();
                RefreshWeaponBar();
                UpdateCardStatus();
                UpdateMentalHPDisplay();
            }
            else if (newState == GameManager.GameState.Paused)
            {
                if (_hudCanvasGroup != null)
                    _hudCanvasGroup.alpha = 0.5f;
            }
            else if (newState == GameManager.GameState.GameOver)
            {
                if (_hudCanvasGroup != null)
                    _hudCanvasGroup.alpha = 0f;
            }
        }

        private void OnLevelUp(int newLevel)
        {
            // 等级变化时更新可能需要的信息
            UpdateMentalHPDisplay();
        }

        #endregion

        #region ─── Public API ───────────────────────────

        /// <summary>
        /// 显示/隐藏HUD
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_hudCanvasGroup != null)
                _hudCanvasGroup.alpha = visible ? 1f : 0f;
            if (_hudCanvasGroup != null)
                _hudCanvasGroup.interactable = visible;
            if (_hudCanvasGroup != null)
                _hudCanvasGroup.blocksRaycasts = visible;
        }

        /// <summary>
        /// 强制刷新所有HUD元素
        /// </summary>
        public void ForceRefresh()
        {
            UpdateMentalHPDisplay();
            UpdateWaveText();
            RefreshWeaponBar();
            UpdateCardStatus();
        }

        #endregion
    }
}
