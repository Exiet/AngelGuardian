using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AngelGuardian.Audio
{
    // ──────────────────────────────────────────────────
    //  BGM Type Definitions
    // ──────────────────────────────────────────────────

    /// <summary>
    /// BGM类型枚举 —— 5首背景音乐
    /// </summary>
    public enum BGMType
    {
        MainMenu,       // 主菜单
        BattleEarly,    // 战斗前期
        BattleClimax,   // 战斗高潮
        BossBattle,     // Boss战
        Failure         // 失败
    }

    // ──────────────────────────────────────────────────
    //  Intensity Layer Definitions
    // ──────────────────────────────────────────────────

    /// <summary>
    /// BGM强度层级 (8层)
    /// 每层在特定敌人数量阈值时激活
    /// </summary>
    public enum IntensityLayer
    {
        Layer1 = 0,     // 敌人<5: 弦乐轻柔
        Layer2 = 1,     // 5-10: +木管
        Layer3 = 2,     // 10-20: +圆号
        Layer4 = 3,     // 20-30: +小号
        Layer5 = 4,     // 30-50: +定音鼓
        Layer6 = 5,     // 50-80: +钹片+合唱
        Layer7 = 6,     // 80-120: +管风琴
        Layer8 = 7      // >120: +全管弦乐+全场合唱
    }

    // ──────────────────────────────────────────────────
    //  Boss Music Phase Definitions
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Boss战音乐阶段 (按HP百分比变化)
    /// </summary>
    public enum BossMusicPhase
    {
        Normal,         // 100-70%: 正常Boss BGM
        Uneasy,         // 70-40%: +不安低音
        Accelerated,    // 40-10%: 加速1.2倍+合唱增强
        Climax          // <10%: 最高潮
    }

    // ──────────────────────────────────────────────────
    //  BGMManager – Background Music Manager
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 背景音乐管理器 —— 8层Intensity系统 + Boss战动态音乐
    /// 
    /// 功能:
    /// - 5首BGM管理 (MainMenu/BattleEarly/BattleClimax/BossBattle/Failure)
    /// - 8层Intensity层叠系统 (弦乐→木管→圆号→小号→定音鼓→钹片+合唱→管风琴→全管弦乐+合唱)
    /// - 层叠过渡: 0.5秒淡入淡出
    /// - Boss战动态音乐 (按HP变化4阶段)
    /// - CrossFade BGM切换
    /// </summary>
    public class BGMManager : MonoBehaviour
    {
        #region ─── Singleton ───────────────────────────

        private static BGMManager _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static BGMManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[BGMManager] Instance accessed after application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<BGMManager>();

                        if (_instance == null)
                        {
                            var go = new GameObject("[BGMManager]");
                            _instance = go.AddComponent<BGMManager>();
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

            InitializeLayerSources();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                StopAllLayers();
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            StopAllLayers();
            _instance = null;
        }

        #endregion

        #region ─── Inspector ───────────────────────────

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer _bgmMixer;

        [Header("CrossFade")]
        [SerializeField] private float _defaultCrossFadeDuration = 2f;
        [SerializeField] private float _layerFadeDuration = 0.5f;  // 层叠过渡时间

        [Header("BGM Clips")]
        [SerializeField] private AudioClip _mainMenuBGM;
        [SerializeField] private AudioClip _battleEarlyBGM;
        [SerializeField] private AudioClip _battleClimaxBGM;
        [SerializeField] private AudioClip _bossBattleBGM;
        [SerializeField] private AudioClip _failureBGM;

        [Header("Intensity Layer Clips (8 layers)")]
        [SerializeField] private AudioClip _layer1Strings;         // 弦乐轻柔
        [SerializeField] private AudioClip _layer2Woodwinds;       // +木管
        [SerializeField] private AudioClip _layer3Horn;            // +圆号
        [SerializeField] private AudioClip _layer4Trumpet;         // +小号
        [SerializeField] private AudioClip _layer5Timpani;         // +定音鼓
        [SerializeField] private AudioClip _layer6CymbalsChoir;    // +钹片+合唱
        [SerializeField] private AudioClip _layer7Organ;           // +管风琴
        [SerializeField] private AudioClip _layer8FullOrchestra;   // +全管弦乐+全场合唱

        [Header("Boss Battle Layer Clips")]
        [SerializeField] private AudioClip _bossUneasyBass;        // 不安低音 (70-40%)
        [SerializeField] private AudioClip _bossAccelerated;       // 加速阶段 (40-10%)
        [SerializeField] private AudioClip _bossClimax;            // 最高潮 (<10%)

        [Header("Boss Music Settings")]
        [SerializeField] private float _bossAcceleratedPitch = 1.2f;  // 加速1.2倍

        [Header("Intensity Thresholds (enemy count)")]
        [SerializeField] private int _thresholdLayer2 = 5;
        [SerializeField] private int _thresholdLayer3 = 10;
        [SerializeField] private int _thresholdLayer4 = 20;
        [SerializeField] private int _thresholdLayer5 = 30;
        [SerializeField] private int _thresholdLayer6 = 50;
        [SerializeField] private int _thresholdLayer7 = 80;
        [SerializeField] private int _thresholdLayer8 = 120;

        [Header("Volume")]
        [SerializeField] private float _bgmVolume = 0.4f;
        [SerializeField] private float _layerVolume = 0.3f;

        #endregion

        #region ─── Runtime Data ───────────────────────

        /// <summary>当前BGM类型</summary>
        private BGMType _currentBGM = BGMType.MainMenu;

        /// <summary>当前活跃Intensity层</summary>
        private int _activeLayerCount = 1;

        /// <summary>当前Boss音乐阶段</summary>
        private BossMusicPhase _bossPhase = BossMusicPhase.Normal;

        /// <summary>当前敌人数量</summary>
        private int _enemyCount = 0;

        /// <summary>Boss当前HP百分比</summary>
        private float _bossHPPercent = 1f;

        /// <summary>是否正在Boss战中</summary>
        private bool _isBossBattle = false;

        /// <summary>8层AudioSource</summary>
        private AudioSource[] _layerSources = new AudioSource[8];

        /// <summary>BGM主AudioSource</summary>
        private AudioSource _bgmSourceA;

        /// <summary>BGM过渡AudioSource</summary>
        private AudioSource _bgmSourceB;

        /// <summary>Boss动态层AudioSource</summary>
        private AudioSource _bossDynamicSource;

        /// <summary>CrossFade活跃标记</summary>
        private bool _crossFading;

        /// <summary>当前使用A或B通道</summary>
        private bool _usingChannelA = true;

        #endregion

        #region ─── Properties ──────────────────────────

        /// <summary>当前BGM类型</summary>
        public BGMType CurrentBGM => _currentBGM;

        /// <summary>当前活跃Intensity层数</summary>
        public int ActiveLayerCount => _activeLayerCount;

        /// <summary>当前Boss音乐阶段</summary>
        public BossMusicPhase CurrentBossPhase => _bossPhase;

        /// <summary>是否正在Boss战中</summary>
        public bool IsBossBattle => _isBossBattle;

        #endregion

        #region ─── Initialization ──────────────────────

        /// <summary>
        /// 初始化层级AudioSource
        /// </summary>
        private void InitializeLayerSources()
        {
            // 创建8层AudioSource
            for (int i = 0; i < 8; i++)
            {
                GameObject layerGo = new GameObject($"BGM_Layer{i + 1}");
                layerGo.transform.SetParent(transform);
                _layerSources[i] = layerGo.AddComponent<AudioSource>();
                _layerSources[i].loop = true;
                _layerSources[i].playOnAwake = false;
                _layerSources[i].volume = 0f; // 默认静音
                _layerSources[i].spatialBlend = 0f; // 2D

                if (_bgmMixer != null)
                {
                    var groups = _bgmMixer.FindMatchingGroups("BGM");
                    if (groups.Length > 0)
                        _layerSources[i].outputAudioMixerGroup = groups[0];
                }
            }

            // 创建BGM双通道AudioSource (用于CrossFade)
            GameObject bgmGoA = new GameObject("BGM_ChannelA");
            bgmGoA.transform.SetParent(transform);
            _bgmSourceA = bgmGoA.AddComponent<AudioSource>();
            _bgmSourceA.loop = true;
            _bgmSourceA.playOnAwake = false;
            _bgmSourceA.volume = 0f;
            _bgmSourceA.spatialBlend = 0f;

            GameObject bgmGoB = new GameObject("BGM_ChannelB");
            bgmGoB.transform.SetParent(transform);
            _bgmSourceB = bgmGoB.AddComponent<AudioSource>();
            _bgmSourceB.loop = true;
            _bgmSourceB.playOnAwake = false;
            _bgmSourceB.volume = 0f;
            _bgmSourceB.spatialBlend = 0f;

            // Boss动态层AudioSource
            GameObject bossGo = new GameObject("BGM_BossDynamic");
            bossGo.transform.SetParent(transform);
            _bossDynamicSource = bossGo.AddComponent<AudioSource>();
            _bossDynamicSource.loop = true;
            _bossDynamicSource.playOnAwake = false;
            _bossDynamicSource.volume = 0f;
            _bossDynamicSource.spatialBlend = 0f;

            // Mixer分组
            if (_bgmMixer != null)
            {
                var bgmGroups = _bgmMixer.FindMatchingGroups("BGM");
                if (bgmGroups.Length > 0)
                {
                    _bgmSourceA.outputAudioMixerGroup = bgmGroups[0];
                    _bgmSourceB.outputAudioMixerGroup = bgmGroups[0];
                    _bossDynamicSource.outputAudioMixerGroup = bgmGroups[0];
                }
            }

            Debug.Log("[BGMManager] Initialized with 8 intensity layers + dual BGM channels.");
        }

        #endregion

        #region ─── Public API – BGM Playback ──────────

        /// <summary>
        /// CrossFade切换BGM
        /// </summary>
        /// <param name="target">目标BGM类型</param>
        /// <param name="duration">过渡时间(秒)</param>
        public void CrossFadeTo(BGMType target, float duration = 0f)
        {
            if (duration <= 0f) duration = _defaultCrossFadeDuration;
            if (_crossFading) return; // 防止同时多个CrossFade

            AudioClip targetClip = GetBGMClip(target);
            if (targetClip == null)
            {
                Debug.LogWarning($"[BGMManager] No clip for BGM type: {target}");
                return;
            }

            _crossFading = true;
            StartCoroutine(CrossFadeRoutine(targetClip, duration, target));
        }

        /// <summary>
        /// 直接播放BGM (无过渡)
        /// </summary>
        public void PlayBGM(BGMType type)
        {
            AudioClip clip = GetBGMClip(type);
            if (clip == null) return;

            AudioSource activeSource = _usingChannelA ? _bgmSourceA : _bgmSourceB;
            StopAllLayers();

            activeSource.clip = clip;
            activeSource.volume = _bgmVolume;
            activeSource.Play();

            _currentBGM = type;
        }

        /// <summary>
        /// 停止BGM
        /// </summary>
        public void StopBGM()
        {
            _bgmSourceA.Stop();
            _bgmSourceB.Stop();
            StopAllLayers();
            _bossDynamicSource.Stop();
            _isBossBattle = false;
        }

        #endregion

        #region ─── Public API – Intensity Layers ───────

        /// <summary>
        /// 根据敌人数量更新Intensity层级
        /// 自动激活/淡出对应层
        /// </summary>
        /// <param name="enemyCount">当前场景敌人数量</param>
        public void UpdateIntensity(int enemyCount)
        {
            _enemyCount = enemyCount;

            // Boss战中不使用普通Intensity层
            if (_isBossBattle) return;

            int targetLayers = CalculateTargetLayers(enemyCount);

            if (targetLayers != _activeLayerCount)
            {
                AdjustLayers(targetLayers);
            }
        }

        /// <summary>
        /// 计算目标层级数（基于敌人数量阈值）
        /// </summary>
        private int CalculateTargetLayers(int enemyCount)
        {
            if (enemyCount < _thresholdLayer2) return 1;
            if (enemyCount < _thresholdLayer3) return 2;
            if (enemyCount < _thresholdLayer4) return 3;
            if (enemyCount < _thresholdLayer5) return 4;
            if (enemyCount < _thresholdLayer6) return 5;
            if (enemyCount < _thresholdLayer7) return 6;
            if (enemyCount < _thresholdLayer8) return 7;
            return 8;
        }

        /// <summary>
        /// 调整层级（0.5秒淡入淡出）
        /// </summary>
        private void AdjustLayers(int targetLayers)
        {
            // 淡出不再需要的层
            for (int i = _activeLayerCount; i < targetLayers; i++)
            {
                // 新增层 → 淡入
                FadeInLayer(i);
            }

            // 淡出多余的层（如果减少了）
            for (int i = targetLayers; i < _activeLayerCount; i++)
            {
                FadeOutLayer(i);
            }

            _activeLayerCount = targetLayers;
        }

        /// <summary>
        /// 淡入指定层
        /// </summary>
        private void FadeInLayer(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= 8) return;

            AudioClip clip = GetLayerClip(layerIndex);
            if (clip == null) return;

            AudioSource source = _layerSources[layerIndex];
            source.clip = clip;

            if (!source.isPlaying)
                source.Play();

            StartCoroutine(FadeVolumeRoutine(source, 0f, _layerVolume, _layerFadeDuration));
        }

        /// <summary>
        /// 淡出指定层
        /// </summary>
        private void FadeOutLayer(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= 8) return;

            AudioSource source = _layerSources[layerIndex];
            StartCoroutine(FadeVolumeRoutine(source, source.volume, 0f, _layerFadeDuration, stopAfterFade: true));
        }

        /// <summary>
        /// 停止所有层
        /// </summary>
        private void StopAllLayers()
        {
            foreach (var source in _layerSources)
            {
                if (source != null)
                {
                    source.Stop();
                    source.volume = 0f;
                }
            }
            _activeLayerCount = 0;
        }

        #endregion

        #region ─── Public API – Boss Battle Music ──────

        /// <summary>
        /// 开始Boss战音乐
        /// </summary>
        /// <param name="bossName">Boss名称</param>
        public void StartBossBattle(string bossName)
        {
            _isBossBattle = true;
            _bossHPPercent = 1f;
            _bossPhase = BossMusicPhase.Normal;

            // 停止普通层
            StopAllLayers();

            // CrossFade到Boss BGM
            CrossFadeTo(BGMType.BossBattle, 1.5f);

            Debug.Log($"[BGMManager] Boss battle started: {bossName}");
        }

        /// <summary>
        /// 更新Boss战音乐（按HP变化）
        /// 100-70%: 正常Boss BGM
        /// 70-40%: +不安低音
        /// 40-10%: 加速1.2倍+合唱增强
        /// <10%: 最高潮
        /// </summary>
        /// <param name="bossHPPercent">Boss当前HP百分比 (0-1)</param>
        public void UpdateBossBattleMusic(float bossHPPercent)
        {
            if (!_isBossBattle) return;

            float oldPercent = _bossHPPercent;
            _bossHPPercent = Mathf.Clamp01(bossHPPercent);

            BossMusicPhase newPhase = DetermineBossPhase(_bossHPPercent);

            if (newPhase != _bossPhase)
            {
                TransitionBossPhase(_bossPhase, newPhase);
                _bossPhase = newPhase;
            }
        }

        /// <summary>
        /// 结束Boss战音乐
        /// </summary>
        public void EndBossBattle()
        {
            _isBossBattle = false;
            _bossPhase = BossMusicPhase.Normal;
            _bossDynamicSource.Stop();
            _bossDynamicSource.volume = 0f;

            // 恢复BattleEarly或BattleClimax
            CrossFadeTo(BGMType.BattleClimax, 2f);

            Debug.Log("[BGMManager] Boss battle ended.");
        }

        /// <summary>
        /// 根据HP百分比判断Boss音乐阶段
        /// </summary>
        private BossMusicPhase DetermineBossPhase(float hpPercent)
        {
            if (hpPercent > 0.7f) return BossMusicPhase.Normal;
            if (hpPercent > 0.4f) return BossMusicPhase.Uneasy;
            if (hpPercent > 0.1f) return BossMusicPhase.Accelerated;
            return BossMusicPhase.Climax;
        }

        /// <summary>
        /// Boss音乐阶段过渡
        /// </summary>
        private void TransitionBossPhase(BossMusicPhase from, BossMusicPhase to)
        {
            // 清理上一阶段特效
            _bossDynamicSource.pitch = 1f;

            switch (to)
            {
                case BossMusicPhase.Normal:
                    // 恢复正常Boss BGM
                    _bossDynamicSource.Stop();
                    _bossDynamicSource.volume = 0f;
                    break;

                case BossMusicPhase.Uneasy:
                    // 70-40%: +不安低音
                    if (_bossUneasyBass != null)
                    {
                        _bossDynamicSource.clip = _bossUneasyBass;
                        _bossDynamicSource.pitch = 1f;
                        _bossDynamicSource.volume = 0f;
                        _bossDynamicSource.Play();
                        StartCoroutine(FadeVolumeRoutine(_bossDynamicSource, 0f, _layerVolume, _layerFadeDuration));
                    }
                    break;

                case BossMusicPhase.Accelerated:
                    // 40-10%: 加速1.2倍+合唱增强
                    _bossDynamicSource.pitch = _bossAcceleratedPitch; // 1.2倍速

                    // BGM主音源也加速
                    AudioSource mainSource = _usingChannelA ? _bgmSourceA : _bgmSourceB;
                    mainSource.pitch = _bossAcceleratedPitch;

                    if (_bossAccelerated != null)
                    {
                        _bossDynamicSource.clip = _bossAccelerated;
                        _bossDynamicSource.volume = 0f;
                        _bossDynamicSource.Play();
                        StartCoroutine(FadeVolumeRoutine(_bossDynamicSource, 0f, _layerVolume * 1.2f, _layerFadeDuration));
                    }
                    break;

                case BossMusicPhase.Climax:
                    // <10%: 最高潮
                    if (_bossClimax != null)
                    {
                        _bossDynamicSource.clip = _bossClimax;
                        _bossDynamicSource.pitch = _bossAcceleratedPitch;
                        _bossDynamicSource.volume = 0f;
                        _bossDynamicSource.Play();
                        StartCoroutine(FadeVolumeRoutine(_bossDynamicSource, 0f, _bgmVolume * 1.5f, _layerFadeDuration));
                    }

                    // 最高潮层所有Intensity层激活
                    for (int i = 0; i < 8; i++)
                    {
                        FadeInLayer(i);
                    }
                    break;
            }

            Debug.Log($"[BGMManager] Boss phase: {from} → {to} (HP: {_bossHPPercent:P0})");
        }

        #endregion

        #region ─── Internal – CrossFade ────────────────

        /// <summary>
        /// CrossFade协程
        /// </summary>
        private IEnumerator CrossFadeRoutine(AudioClip targetClip, float duration, BGMType targetType)
        {
            AudioSource fadeOutSource = _usingChannelA ? _bgmSourceA : _bgmSourceB;
            AudioSource fadeInSource = _usingChannelA ? _bgmSourceB : _bgmSourceA;

            // 设置新clip
            fadeInSource.clip = targetClip;
            fadeInSource.volume = 0f;
            fadeInSource.pitch = 1f;
            fadeInSource.Play();

            // 双向渐变
            float startVolumeOut = fadeOutSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 淡出旧
                fadeOutSource.volume = Mathf.Lerp(startVolumeOut, 0f, t);
                // 淡入新
                fadeInSource.volume = Mathf.Lerp(0f, _bgmVolume, t);

                yield return null;
            }

            // 完成
            fadeOutSource.Stop();
            fadeOutSource.volume = 0f;
            fadeInSource.volume = _bgmVolume;

            _usingChannelA = !_usingChannelA;
            _currentBGM = targetType;
            _crossFading = false;

            Debug.Log($"[BGMManager] CrossFade complete: {_currentBGM}");
        }

        #endregion

        #region ─── Internal – Volume Fade ──────────────

        /// <summary>
        /// 音量渐变协程
        /// </summary>
        private IEnumerator FadeVolumeRoutine(AudioSource source, float fromVolume, float toVolume, float duration, bool stopAfterFade = false)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                source.volume = Mathf.Lerp(fromVolume, toVolume, t);
                yield return null;
            }

            source.volume = toVolume;

            if (stopAfterFade && toVolume <= 0f)
            {
                source.Stop();
            }
        }

        #endregion

        #region ─── Internal – Clip Lookup ──────────────

        /// <summary>
        /// 获取BGM Clip
        /// </summary>
        private AudioClip GetBGMClip(BGMType type)
        {
            return type switch
            {
                BGMType.MainMenu => _mainMenuBGM,
                BGMType.BattleEarly => _battleEarlyBGM,
                BGMType.BattleClimax => _battleClimaxBGM,
                BGMType.BossBattle => _bossBattleBGM,
                BGMType.Failure => _failureBGM,
                _ => null
            };
        }

        /// <summary>
        /// 获取层级Clip
        /// </summary>
        private AudioClip GetLayerClip(int layerIndex)
        {
            return layerIndex switch
            {
                0 => _layer1Strings,
                1 => _layer2Woodwinds,
                2 => _layer3Horn,
                3 => _layer4Trumpet,
                4 => _layer5Timpani,
                5 => _layer6CymbalsChoir,
                6 => _layer7Organ,
                7 => _layer8FullOrchestra,
                _ => null
            };
        }

        #endregion

        #region ─── Debug ───────────────────────────────

        [ContextMenu("Log BGM State")]
        private void LogBGMState()
        {
            Debug.Log($"[BGMManager] BGM: {_currentBGM} | Layers: {_activeLayerCount} | " +
                      $"Boss: {_isBossBattle} (Phase: {_bossPhase}, HP: {_bossHPPercent:P0}) | " +
                      $"Enemies: {_enemyCount} | CrossFading: {_crossFading}");
        }

        [ContextMenu("Simulate Boss Battle")]
        private void SimulateBossBattle()
        {
            StartBossBattle("TestBoss");
        }

        [ContextMenu("Simulate Boss HP 50%")]
        private void SimulateBossHP50()
        {
            UpdateBossBattleMusic(0.5f);
        }

        [ContextMenu("Simulate Boss HP 5%")]
        private void SimulateBossHP5()
        {
            UpdateBossBattleMusic(0.05f);
        }

        #endregion
    }
}
