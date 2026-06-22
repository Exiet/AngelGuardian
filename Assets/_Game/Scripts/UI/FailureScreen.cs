using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using AngelGuardian.Core;
using AngelGuardian.Baby;

namespace AngelGuardian.UI
{
    /// <summary>
    /// 失败界面 —— 游戏结束时显示。
    /// 4种失败类型各有不同的视觉表现：
    /// - 世界毁灭：屏幕碎裂→婴儿大哭→白光→碎片飘散
    /// - Boss吞噬：Boss阴影→婴儿被包裹→天使伸手→黑屏
    /// - 自愿放弃：天使合拢翅膀→圣光笼罩→淡出
    /// - 时间耗尽：沙漏碎裂→画面老化→敌人消失→婴儿安全
    /// 包含结算数据、Meta进度展示、"再来一次"和"返回主菜单"按钮。
    /// </summary>
    public class FailureScreen : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static FailureScreen _instance;
        public static FailureScreen Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<FailureScreen>();
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

        #region ─── Failure Types ────────────────────────

        public enum FailureType
        {
            WorldDestruction,   // 世界毁灭
            BossDevour,         // Boss吞噬
            VoluntaryGiveUp,    // 自愿放弃
            TimeExhausted       // 时间耗尽
        }

        #endregion

        #region ─── Inspector: Root ──────────────────────

        [Header("Root")]
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Canvas _mainCanvas;

        #endregion

        #region ─── Inspector: Failure Animations ────────

        [Header("World Destruction")]
        [SerializeField] private GameObject _worldDestructionRoot;
        [SerializeField] private Image _screenCrackImage;
        [SerializeField] private Image _whiteFlashImage;
        [SerializeField] private RectTransform[] _debrisPieces;
        [SerializeField] private float _crackDuration = 1.5f;
        [SerializeField] private float _flashDuration = 0.5f;
        [SerializeField] private float _debrisDuration = 2f;

        [Header("Boss Devour")]
        [SerializeField] private GameObject _bossDevourRoot;
        [SerializeField] private Image _bossShadowImage;
        [SerializeField] private Image _babyWrapImage;
        [SerializeField] private Image _angelReachImage;
        [SerializeField] private Image _blackScreenImage;
        [SerializeField] private float _shadowGrowDuration = 2f;
        [SerializeField] private float _wrapDuration = 1.5f;
        [SerializeField] private float _fadeToBlackDuration = 1f;

        [Header("Voluntary Give Up")]
        [SerializeField] private GameObject _voluntaryGiveUpRoot;
        [SerializeField] private Image _angelWingsImage;
        [SerializeField] private Image _holyLightImage;
        [SerializeField] private float _wingsFoldDuration = 2f;
        [SerializeField] private float _fadeOutDuration = 1.5f;

        [Header("Time Exhausted")]
        [SerializeField] private GameObject _timeExhaustedRoot;
        [SerializeField] private Image _hourglassImage;
        [SerializeField] private Image _screenAgingOverlay;
        [SerializeField] private float _hourglassBreakDuration = 1.5f;
        [SerializeField] private float _agingDuration = 2f;

        #endregion

        #region ─── Inspector: Settlement Data ───────────

        [Header("Settlement Data")]
        [SerializeField] private TMP_Text _survivalTimeText;
        [SerializeField] private TMP_Text _wavesClearedText;
        [SerializeField] private TMP_Text _totalKillsText;
        [SerializeField] private TMP_Text _buildSummaryText;
        [SerializeField] private TMP_Text _maxAwakeningLevelText;

        #endregion

        #region ─── Inspector: Meta Progress ─────────────

        [Header("Meta Progress")]
        [SerializeField] private TMP_Text _expGainedText;
        [SerializeField] private TMP_Text _levelUpText;
        [SerializeField] private Slider _metaProgressSlider;
        [SerializeField] private TMP_Text _permanentUnlockText;

        #endregion

        #region ─── Inspector: Buttons ───────────────────

        [Header("Buttons")]
        [SerializeField] private Button _retryButton;
        [SerializeField] private TMP_Text _retryButtonText;
        [SerializeField] private Button _mainMenuButton;

        [Header("Emotion-Driven Text")]
        [SerializeField] private string[] _retryMessages = new string[]
        {
            "婴儿在等你拯救TA",
            "TA还在那里...再试一次吧",
            "你不能放弃TA",
            "守护天使，再次飞翔"
        };

        #endregion

        #region ─── Runtime State ────────────────────────

        private FailureType _currentFailureType;
        private bool _isPlaying;

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

            HideAllFailureRoots();

            // 绑定按钮
            if (_retryButton != null)
                _retryButton.onClick.AddListener(OnRetryClicked);
            if (_mainMenuButton != null)
                _mainMenuButton.onClick.AddListener(OnMainMenuClicked);

            // 订阅GameOver事件
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnGameOver.AddListener(OnGameOver);
            }
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnGameOver.RemoveListener(OnGameOver);
            if (_retryButton != null) _retryButton.onClick.RemoveListener(OnRetryClicked);
            if (_mainMenuButton != null) _mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
        }

        #endregion

        #region ─── Game Over Handler ────────────────────

        private void OnGameOver(string failType)
        {
            _currentFailureType = ParseFailureType(failType);
            Show();
        }

        private FailureType ParseFailureType(string failType)
        {
            return failType switch
            {
                "BabyMentalZero" => FailureType.WorldDestruction,
                "PlayerDied" => FailureType.BossDevour,
                "PlayerQuit" => FailureType.VoluntaryGiveUp,
                "TimeExpired" => FailureType.TimeExhausted,
                _ => FailureType.WorldDestruction
            };
        }

        #endregion

        #region ─── Show ──────────────────────────────────

        private void Show()
        {
            if (_rootCanvasGroup == null) return;

            _isPlaying = true;
            _rootCanvasGroup.alpha = 1f;
            _rootCanvasGroup.interactable = true;
            _rootCanvasGroup.blocksRaycasts = true;

            // 隐藏按钮，等动画播完再显示
            if (_retryButton != null) _retryButton.gameObject.SetActive(false);
            if (_mainMenuButton != null) _mainMenuButton.gameObject.SetActive(false);

            // 设置情感驱动文字
            if (_retryButtonText != null)
                _retryButtonText.text = _retryMessages[Random.Range(0, _retryMessages.Length)];

            // 填充结算数据
            PopulateSettlementData();

            // 填充Meta进度
            PopulateMetaProgress();

            // 播放对应失败动画
            switch (_currentFailureType)
            {
                case FailureType.WorldDestruction:
                    StartCoroutine(PlayWorldDestruction());
                    break;
                case FailureType.BossDevour:
                    StartCoroutine(PlayBossDevour());
                    break;
                case FailureType.VoluntaryGiveUp:
                    StartCoroutine(PlayVoluntaryGiveUp());
                    break;
                case FailureType.TimeExhausted:
                    StartCoroutine(PlayTimeExhausted());
                    break;
            }
        }

        #endregion

        #region ─── Failure Animations ───────────────────

        /// <summary>
        /// 世界毁灭：屏幕碎裂→婴儿大哭→白光→碎片飘散
        /// </summary>
        private IEnumerator PlayWorldDestruction()
        {
            if (_worldDestructionRoot != null)
                _worldDestructionRoot.SetActive(true);

            // 阶段1: 屏幕碎裂
            if (_screenCrackImage != null)
            {
                _screenCrackImage.gameObject.SetActive(true);
                _screenCrackImage.color = new Color(1f, 1f, 1f, 0f);

                float elapsed = 0f;
                while (elapsed < _crackDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / _crackDuration;
                    _screenCrackImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.9f, t));
                    yield return null;
                }
            }

            // 阶段2: 白光
            if (_whiteFlashImage != null)
            {
                _whiteFlashImage.gameObject.SetActive(true);
                _whiteFlashImage.color = new Color(1f, 1f, 1f, 0f);

                float elapsed = 0f;
                while (elapsed < _flashDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / _flashDuration;
                    // 快速闪白后降低
                    float alpha = t < 0.3f ? Mathf.Lerp(0f, 1f, t / 0.3f) : Mathf.Lerp(1f, 0.3f, (t - 0.3f) / 0.7f);
                    _whiteFlashImage.color = new Color(1f, 1f, 1f, alpha);
                    yield return null;
                }
            }

            // 阶段3: 碎片飘散
            if (_debrisPieces != null)
            {
                foreach (var piece in _debrisPieces)
                {
                    if (piece != null)
                    {
                        piece.gameObject.SetActive(true);
                        StartCoroutine(DebrisDrift(piece, _debrisDuration));
                    }
                }
                yield return new WaitForSecondsRealtime(_debrisDuration);
            }

            ShowButtons();
        }

        private IEnumerator DebrisDrift(RectTransform piece, float duration)
        {
            Vector2 startPos = piece.anchoredPosition;
            Vector2 driftDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            float driftDistance = Random.Range(100f, 300f);
            float rotationSpeed = Random.Range(-180f, 180f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                piece.anchoredPosition = startPos + driftDir * driftDistance * t;
                piece.Rotate(0f, 0f, rotationSpeed * Time.unscaledDeltaTime);

                // 淡出
                var canvasGroup = piece.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

                yield return null;
            }
        }

        /// <summary>
        /// Boss吞噬：Boss阴影→婴儿被包裹→天使伸手→黑屏
        /// </summary>
        private IEnumerator PlayBossDevour()
        {
            if (_bossDevourRoot != null)
                _bossDevourRoot.SetActive(true);

            // 阶段1: Boss阴影渐显
            if (_bossShadowImage != null)
            {
                _bossShadowImage.color = new Color(0f, 0f, 0f, 0f);
                float elapsed = 0f;
                while (elapsed < _shadowGrowDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / _shadowGrowDuration;
                    _bossShadowImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.8f, t));
                    _bossShadowImage.rectTransform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one * 1.5f, t);
                    yield return null;
                }
            }

            // 阶段2: 婴儿被包裹
            if (_babyWrapImage != null)
            {
                _babyWrapImage.gameObject.SetActive(true);
                _babyWrapImage.color = new Color(1f, 1f, 1f, 0f);
                float elapsed = 0f;
                while (elapsed < _wrapDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / _wrapDuration;
                    _babyWrapImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, t));
                    yield return null;
                }
            }

            // 阶段3: 天使伸手
            if (_angelReachImage != null)
            {
                _angelReachImage.gameObject.SetActive(true);
                float elapsed = 0f;
                while (elapsed < 1f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _angelReachImage.rectTransform.anchoredPosition = Vector2.Lerp(
                        new Vector2(0f, -200f), Vector2.zero, elapsed / 1f);
                    yield return null;
                }
            }

            // 阶段4: 黑屏
            if (_blackScreenImage != null)
            {
                _blackScreenImage.gameObject.SetActive(true);
                _blackScreenImage.color = new Color(0f, 0f, 0f, 0f);
                float elapsed = 0f;
                while (elapsed < _fadeToBlackDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / _fadeToBlackDuration;
                    _blackScreenImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 1f, t));
                    yield return null;
                }
            }

            ShowButtons();
        }

        /// <summary>
        /// 自愿放弃：天使合拢翅膀→圣光笼罩→淡出
        /// </summary>
        private IEnumerator PlayVoluntaryGiveUp()
        {
            if (_voluntaryGiveUpRoot != null)
                _voluntaryGiveUpRoot.SetActive(true);

            // 阶段1: 天使合拢翅膀
            if (_angelWingsImage != null)
            {
                float elapsed = 0f;
                while (elapsed < _wingsFoldDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / _wingsFoldDuration;
                    // 翅膀从展开到合拢
                    _angelWingsImage.rectTransform.localScale = new Vector3(
                        Mathf.Lerp(1.5f, 0.3f, t),
                        Mathf.Lerp(1f, 1.5f, t),
                        1f);
                    yield return null;
                }
            }

            // 阶段2: 圣光笼罩
            if (_holyLightImage != null)
            {
                _holyLightImage.gameObject.SetActive(true);
                _holyLightImage.color = new Color(1f, 0.95f, 0.8f, 0f);
                float elapsed = 0f;
                while (elapsed < 1f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _holyLightImage.color = new Color(1f, 0.95f, 0.8f, Mathf.Lerp(0f, 0.9f, elapsed / 1f));
                    yield return null;
                }
            }

            // 阶段3: 整体淡出
            if (_rootCanvasGroup != null)
            {
                float elapsed = 0f;
                float startAlpha = _rootCanvasGroup.alpha;
                while (elapsed < _fadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _rootCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0.3f, elapsed / _fadeOutDuration);
                    yield return null;
                }
                _rootCanvasGroup.alpha = 1f; // 恢复以便显示按钮
            }

            ShowButtons();
        }

        /// <summary>
        /// 时间耗尽：沙漏碎裂→画面老化→敌人消失→婴儿安全
        /// </summary>
        private IEnumerator PlayTimeExhausted()
        {
            if (_timeExhaustedRoot != null)
                _timeExhaustedRoot.SetActive(true);

            // 阶段1: 沙漏碎裂
            if (_hourglassImage != null)
            {
                float elapsed = 0f;
                while (elapsed < _hourglassBreakDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / _hourglassBreakDuration;
                    // 震动效果
                    _hourglassImage.rectTransform.localPosition = new Vector3(
                        Mathf.Sin(t * 30f) * (1f - t) * 10f, 0f, 0f);
                    yield return null;
                }
                _hourglassImage.gameObject.SetActive(false);
            }

            // 阶段2: 画面老化
            if (_screenAgingOverlay != null)
            {
                _screenAgingOverlay.gameObject.SetActive(true);
                _screenAgingOverlay.color = new Color(0.8f, 0.7f, 0.5f, 0f);

                float elapsed = 0f;
                while (elapsed < _agingDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / _agingDuration;
                    _screenAgingOverlay.color = new Color(0.8f, 0.7f, 0.5f, Mathf.Lerp(0f, 0.6f, t));
                    yield return null;
                }
            }

            ShowButtons();
        }

        #endregion

        #region ─── Settlement Data ──────────────────────

        private void PopulateSettlementData()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 存活时间
            if (_survivalTimeText != null)
            {
                int minutes = Mathf.FloorToInt(gm.ElapsedTime / 60f);
                int seconds = Mathf.FloorToInt(gm.ElapsedTime % 60f);
                _survivalTimeText.text = $"存活时间: {minutes:D2}:{seconds:D2}";
            }

            // 波次
            if (_wavesClearedText != null)
                _wavesClearedText.text = $"完成波次: {gm.CurrentWave} / {GameManager.TotalWaves}";

            // 击杀数
            if (_totalKillsText != null)
                _totalKillsText.text = $"击杀数: {gm.TotalKills}";

            // Build图谱
            if (_buildSummaryText != null)
            {
                var wm = Weapons.WeaponManager.Instance;
                var cm = Cards.CardManager.Instance;
                string weaponSummary = wm != null ? string.Join("+", wm.GetAllWeapons().ConvertAll(w => w.weaponName)) : "无武器";
                string cardSummary = cm != null ? $"{cm.CardCount}张卡牌" : "";
                _buildSummaryText.text = $"Build: {weaponSummary}\n{cardSummary}";
            }

            // 婴儿最高觉醒等级
            if (_maxAwakeningLevelText != null)
            {
                var babyAttr = FindObjectOfType<BabyAttributes>();
                float awakenCharge = babyAttr != null ? babyAttr.BabyAwakenCharge : 0f;
                _maxAwakeningLevelText.text = $"婴儿觉醒充能: {awakenCharge:F0}%";
            }
        }

        private void PopulateMetaProgress()
        {
            var meta = MetaProgression.Instance;
            var gm = GameManager.Instance;
            if (meta == null || gm == null) return;

            // 获得经验（简化计算）
            int expGained = gm.TotalKills * 10 + gm.CurrentWave * 50;
            if (_expGainedText != null)
                _expGainedText.text = $"获得经验: +{expGained}";

            // 升级提示
            if (_levelUpText != null)
                _levelUpText.text = $"当前等级: {gm.CurrentLevel}";

            // Meta进度条
            if (_metaProgressSlider != null)
            {
                _metaProgressSlider.value = meta.SoulShards % 100; // 简化展示
                _metaProgressSlider.maxValue = 100;
            }

            // 永久解锁进度
            if (_permanentUnlockText != null)
            {
                int totalUpgrades = meta.GetUpgradeLevel(MetaProgression.UpgradeType.InitialMentalBonus)
                    + meta.GetUpgradeLevel(MetaProgression.UpgradeType.InitialWeaponSlot)
                    + meta.GetUpgradeLevel(MetaProgression.UpgradeType.BaseLuckBonus)
                    + meta.GetUpgradeLevel(MetaProgression.UpgradeType.CardRerollCount)
                    + meta.GetUpgradeLevel(MetaProgression.UpgradeType.WeaponRerollFree)
                    + meta.GetUpgradeLevel(MetaProgression.UpgradeType.ExtraAttributes);
                _permanentUnlockText.text = $"永久升级: {totalUpgrades}项";
            }
        }

        #endregion

        #region ─── Button Handlers ──────────────────────

        private void OnRetryClicked()
        {
            // 重新开始游戏
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StartGame();
            }

            // 隐藏失败界面
            Hide();
        }

        private void OnMainMenuClicked()
        {
            Hide();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        #endregion

        #region ─── Helpers ───────────────────────────────

        private void HideAllFailureRoots()
        {
            if (_worldDestructionRoot != null) _worldDestructionRoot.SetActive(false);
            if (_bossDevourRoot != null) _bossDevourRoot.SetActive(false);
            if (_voluntaryGiveUpRoot != null) _voluntaryGiveUpRoot.SetActive(false);
            if (_timeExhaustedRoot != null) _timeExhaustedRoot.SetActive(false);
        }

        private void ShowButtons()
        {
            if (_retryButton != null)
            {
                _retryButton.gameObject.SetActive(true);
                // 弹性出现动画
                StartCoroutine(BounceIn(_retryButton.transform));
            }
            if (_mainMenuButton != null)
            {
                _mainMenuButton.gameObject.SetActive(true);
                StartCoroutine(BounceIn(_mainMenuButton.transform));
            }
        }

        private IEnumerator BounceIn(Transform target)
        {
            target.localScale = Vector3.zero;
            float elapsed = 0f;
            float duration = 0.4f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float scale = 1f - Mathf.Exp(-t * 5f) * Mathf.Cos(t * Mathf.PI * 3f);
                target.localScale = Vector3.one * scale;
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private void Hide()
        {
            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = false;
            }
            _isPlaying = false;
            HideAllFailureRoots();
        }

        #endregion
    }
}
