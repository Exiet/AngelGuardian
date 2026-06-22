using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AngelGuardian.Audio
{
    // ──────────────────────────────────────────────────
    //  SFX Priority Levels
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 音效优先级系统 (0-4, 0最高)
    /// 
    /// 0: 婴儿恐惧音效（可打断其他）
    /// 1: Boss出场音效
    /// 2: 连携触发音效
    /// 3: 卡牌释放/武器命中音效
    /// 4: 环境音效（始终播放）
    /// </summary>
    public enum SFXPriority
    {
        BabyEmotion = 0,    // 婴儿情感音效 - 最高优先级，可打断其他
        BossEntrance = 1,   // Boss出场音效
        ComboTrigger = 2,   // 连携触发音效
        CardWeapon = 3,     // 卡牌释放/武器命中音效
        Ambient = 4         // 环境音效 - 始终播放
    }

    // ──────────────────────────────────────────────────
    //  Pool Category Definitions
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 音效对象池分类
    /// </summary>
    public enum SFXPoolCategory
    {
        WeaponHit,      // 武器命中 (10)
        CardRelease,    // 卡牌释放 (5)
        EnemyHit,       // 敌人受伤 (8)
        UI              // UI音效 (3)
    }

    // ──────────────────────────────────────────────────
    //  AudioManager – Singleton
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 音效管理器 —— 核心音频播放系统
    /// 
    /// 功能:
    /// - 音效优先级系统 (0-4级)
    /// - 对象池管理 (武器命中10/卡牌释放5/敌人受伤8/UI3)
    /// - 音量规范 (婴儿0.8/武器0.6/卡牌0.5/环境0.3/UI0.4/BGM0.4)
    /// - 平台适配: 移动端16轨 / PC端32轨
    /// - AudioMixer分组管理
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        #region ─── Singleton ───────────────────────────

        private static AudioManager _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static AudioManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[AudioManager] Instance accessed after application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<AudioManager>();

                        if (_instance == null)
                        {
                            var go = new GameObject("[AudioManager]");
                            _instance = go.AddComponent<AudioManager>();
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

            InitializePools();
            InitializeAudioMixerGroups();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                CleanupPools();
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            CleanupPools();
            _instance = null;
        }

        #endregion

        #region ─── Inspector ───────────────────────────

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer _masterMixer;

        [Header("Volume Presets (Specification)")]
        [SerializeField] private float _babyEmotionVolume = 0.8f;
        [SerializeField] private float _weaponHitVolume = 0.6f;
        [SerializeField] private float _cardVolume = 0.5f;
        [SerializeField] private float _ambientVolume = 0.3f;
        [SerializeField] private float _uiVolume = 0.4f;
        [SerializeField] private float _bgmVolume = 0.4f;

        [Header("Pool Sizes (Specification)")]
        [SerializeField] private int _weaponHitPoolSize = 10;
        [SerializeField] private int _cardReleasePoolSize = 5;
        [SerializeField] private int _enemyHitPoolSize = 8;
        [SerializeField] private int _uiPoolSize = 3;

        [Header("Platform Audio Tracks")]
        [SerializeField] private int _mobileTrackCount = 16;
        [SerializeField] private int _pcTrackCount = 32;

        [Header("Audio Clips – Baby Emotion")]
        [SerializeField] private AudioClip[] _babyCuriousClips;
        [SerializeField] private AudioClip[] _babyFearClips;
        [SerializeField] private AudioClip[] _babyAngerClips;
        [SerializeField] private AudioClip[] _babyTiredClips;
        [SerializeField] private AudioClip[] _babyAwakeningClips;

        [Header("Audio Clips – Boss")]
        [SerializeField] private AudioClip[] _bossEntranceClips;

        [Header("Audio Clips – Combo")]
        [SerializeField] private DictionaryAsset _comboClipDict;

        [Header("Audio Clips – Cards")]
        [SerializeField] private DictionaryAsset _cardClipDict;

        [Header("Audio Clips – Weapons")]
        [SerializeField] private DictionaryAsset _weaponClipDict;
        [SerializeField] private AudioClip[] _weaponCritHitClips;

        #endregion

        #region ─── Audio Mixer Group Names ──────────────

        private const string MIXER_MASTER = "Master";
        private const string MIXER_BGM = "BGM";
        private const string MIXER_SFX_BABY = "SFX_Baby";
        private const string MIXER_SFX_WEAPON = "SFX_Weapon";
        private const string MIXER_SFX_CARD = "SFX_Card";
        private const string MIXER_SFX_AMBIENT = "SFX_Ambient";
        private const string MIXER_SFX_UI = "SFX_UI";

        #endregion

        #region ─── Runtime Data ───────────────────────

        /// <summary>当前平台最大音轨数</summary>
        private int _maxTracks;

        /// <summary>当前活跃音轨数</summary>
        private int _activeTrackCount;

        /// <summary>对象池集合</summary>
        private Dictionary<SFXPoolCategory, SFXPool> _pools = new Dictionary<SFXPoolCategory, SFXPool>();

        /// <summary>正在播放的高优先级音效（可被P0打断）</summary>
        private List<ActiveSFXRecord> _activeSFX = new List<ActiveSFXRecord>();

        /// <summary>AudioMixer分组缓存</summary>
        private Dictionary<string, AudioMixerGroup> _mixerGroups = new Dictionary<string, AudioMixerGroup>();

        #endregion

        #region ─── Properties ──────────────────────────

        /// <summary>最大音轨数（平台自适应）</summary>
        public int MaxTracks => _maxTracks;

        /// <summary>当前活跃音轨数</summary>
        public int ActiveTrackCount => _activeTrackCount;

        /// <summary>是否为移动端</summary>
        public bool IsMobilePlatform =>
#if UNITY_ANDROID || UNITY_IOS
            true;
#else
            false;
#endif

        #endregion

        #region ─── Initialization ──────────────────────

        /// <summary>
        /// 初始化对象池
        /// </summary>
        private void InitializePools()
        {
            // 平台自适应轨道数
            _maxTracks = IsMobilePlatform ? _mobileTrackCount : _pcTrackCount;

            // 创建各分类的对象池
            _pools[SFXPoolCategory.WeaponHit] = new SFXPool(_weaponHitPoolSize, false);
            _pools[SFXPoolCategory.CardRelease] = new SFXPool(_cardReleasePoolSize, false);
            _pools[SFXPoolCategory.EnemyHit] = new SFXPool(_enemyHitPoolSize, false);
            _pools[SFXPoolCategory.UI] = new SFXPool(_uiPoolSize, false);

            Debug.Log($"[AudioManager] Initialized. Platform: {(IsMobilePlatform ? "Mobile" : "PC")}, " +
                      $"MaxTracks: {_maxTracks}, Pools: {_pools.Count}");
        }

        /// <summary>
        /// 初始化AudioMixer分组
        /// </summary>
        private void InitializeAudioMixerGroups()
        {
            if (_masterMixer == null)
            {
                Debug.LogWarning("[AudioManager] No AudioMixer assigned. Volume groups will use direct AudioSource control.");
                return;
            }

            // 查找所有分组
            AudioMixerGroup[] groups = _masterMixer.FindMatchingGroups("");
            foreach (var group in groups)
            {
                _mixerGroups[group.name] = group;
            }

            // 设置初始音量
            SetMixerVolume(MIXER_BGM, _bgmVolume);
            SetMixerVolume(MIXER_SFX_BABY, _babyEmotionVolume);
            SetMixerVolume(MIXER_SFX_WEAPON, _weaponHitVolume);
            SetMixerVolume(MIXER_SFX_CARD, _cardVolume);
            SetMixerVolume(MIXER_SFX_AMBIENT, _ambientVolume);
            SetMixerVolume(MIXER_SFX_UI, _uiVolume);

            Debug.Log("[AudioManager] AudioMixer groups initialized.");
        }

        /// <summary>
        /// 清理所有对象池
        /// </summary>
        private void CleanupPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            _pools.Clear();
            _activeSFX.Clear();
        }

        #endregion

        #region ─── Public API – Play SFX ───────────────

        /// <summary>
        /// 按优先级播放音效
        /// 优先级0(婴儿情感)可打断其他正在播放的音效
        /// </summary>
        /// <param name="clip">音效片段</param>
        /// <param name="priority">优先级 (0-4)</param>
        /// <param name="volume">音量 (0-1)</param>
        /// <param name="position">3D位置 (null则2D)</param>
        /// <param name="poolCategory">对象池分类</param>
        public void PlaySFX(AudioClip clip, int priority, float volume, Vector3? position = null, SFXPoolCategory poolCategory = SFXPoolCategory.WeaponHit)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] Attempted to play null AudioClip.");
                return;
            }

            // 检查轨道上限
            if (_activeTrackCount >= _maxTracks)
            {
                // 优先级0可以打断低优先级音效
                if (priority == (int)SFXPriority.BabyEmotion)
                {
                    InterruptLowPrioritySFX();
                }
                else
                {
                    // 性能保护：超过限制时合并而非丢弃
                    Debug.LogWarning($"[AudioManager] Track limit reached ({_maxTracks}). SFX (priority {priority}) skipped for performance.");
                    return;
                }
            }

            // 从对象池获取AudioSource
            SFXPool pool = GetPool(poolCategory);
            AudioSource source = pool.Get();

            if (source == null)
            {
                // 池耗尽时创建临时AudioSource
                source = CreateTempAudioSource();
            }

            // 配置AudioSource
            ConfigureAudioSource(source, clip, priority, volume, position);

            // 分配AudioMixer分组
            AssignMixerGroup(source, priority);

            // 记录活跃音效
            var record = new ActiveSFXRecord
            {
                source = source,
                priority = priority,
                category = poolCategory,
                startTime = Time.time,
                clipLength = clip.length
            };
            _activeSFX.Add(record);
            _activeTrackCount++;

            // 播放
            source.Play();

            // 自动回收协程
            StartCoroutine(AutoReleaseRoutine(source, clip.length, poolCategory, record));
        }

        /// <summary>
        /// 播放婴儿情感音效 (优先级0)
        /// </summary>
        /// <param name="state">情感状态</param>
        /// <param name="phase">阶段标识 (如 "enter", "loop", "exit")</param>
        public void PlayBabyEmotionSFX(EmotionState state, string phase)
        {
            AudioClip[] clips = GetBabyEmotionClips(state);
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[AudioManager] No clips for BabyEmotion: {state}");
                return;
            }

            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            PlaySFX(clip, (int)SFXPriority.BabyEmotion, _babyEmotionVolume, null, SFXPoolCategory.EnemyHit);
        }

        /// <summary>
        /// 播放连携触发音效 (优先级2)
        /// </summary>
        /// <param name="comboName">连携名称</param>
        public void PlayComboSFX(string comboName)
        {
            AudioClip clip = GetClipFromDict(_comboClipDict, comboName);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] No clip for Combo: {comboName}");
                return;
            }

            PlaySFX(clip, (int)SFXPriority.ComboTrigger, _weaponHitVolume, null, SFXPoolCategory.WeaponHit);
        }

        /// <summary>
        /// 播放卡牌释放音效 (优先级3)
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        public void PlayCardSFX(string cardId)
        {
            AudioClip clip = GetClipFromDict(_cardClipDict, cardId);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] No clip for Card: {cardId}");
                return;
            }

            PlaySFX(clip, (int)SFXPriority.CardWeapon, _cardVolume, null, SFXPoolCategory.CardRelease);
        }

        /// <summary>
        /// 播放武器命中音效 (优先级3)
        /// </summary>
        /// <param name="weaponId">武器ID</param>
        /// <param name="isCrit">是否暴击</param>
        /// <param name="position">3D命中位置</param>
        public void PlayWeaponHitSFX(string weaponId, bool isCrit, Vector3? position = null)
        {
            AudioClip clip = GetClipFromDict(_weaponClipDict, weaponId);

            if (isCrit && _weaponCritHitClips != null && _weaponCritHitClips.Length > 0)
            {
                // 暴击使用专用音效
                clip = _weaponCritHitClips[UnityEngine.Random.Range(0, _weaponCritHitClips.Length)];
            }

            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] No clip for Weapon: {weaponId}");
                return;
            }

            float volume = isCrit ? _weaponHitVolume * 1.3f : _weaponHitVolume; // 暴击音量略高
            PlaySFX(clip, (int)SFXPriority.CardWeapon, volume, position, SFXPoolCategory.WeaponHit);
        }

        /// <summary>
        /// 播放UI音效 (优先级4/环境级)
        /// </summary>
        /// <param name="clip">音效片段</param>
        public void PlayUISFX(AudioClip clip)
        {
            if (clip == null) return;
            PlaySFX(clip, (int)SFXPriority.Ambient, _uiVolume, null, SFXPoolCategory.UI);
        }

        #endregion

        #region ─── Public API – Volume Control ─────────

        /// <summary>
        /// 设置Master音量
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            if (_masterMixer != null)
            {
                _masterMixer.SetFloat("MasterVolume", LinearToDecibel(volume));
            }
        }

        /// <summary>
        /// 设置BGM音量
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            SetMixerVolume(MIXER_BGM, _bgmVolume);
        }

        /// <summary>
        /// 设置SFX分组音量
        /// </summary>
        public void SetSFXGroupVolume(SFXPriority priority, float volume)
        {
            string groupName = GetMixerGroupName(priority);
            SetMixerVolume(groupName, Mathf.Clamp01(volume));
        }

        /// <summary>
        /// 获取当前音量设置
        /// </summary>
        public float GetVolumeForPriority(SFXPriority priority)
        {
            return priority switch
            {
                SFXPriority.BabyEmotion => _babyEmotionVolume,
                SFXPriority.BossEntrance => _weaponHitVolume,
                SFXPriority.ComboTrigger => _weaponHitVolume,
                SFXPriority.CardWeapon => _cardVolume,
                SFXPriority.Ambient => _ambientVolume,
                _ => 1f
            };
        }

        #endregion

        #region ─── Internal – SFX Interruption ────────

        /// <summary>
        /// 打断低优先级音效（被P0婴儿情感音效触发）
        /// 按优先级从高到低打断，直到释放足够轨道
        /// </summary>
        private void InterruptLowPrioritySFX()
        {
            // 优先打断P4环境音效，然后P3, P2...
            for (int p = (int)SFXPriority.Ambient; p >= (int)SFXPriority.ComboTrigger; p--)
            {
                for (int i = _activeSFX.Count - 1; i >= 0; i--)
                {
                    if (_activeSFX[i].priority >= p)
                    {
                        ReleaseSFXRecord(_activeSFX[i]);
                        _activeSFX.RemoveAt(i);
                        _activeTrackCount--;

                        if (_activeTrackCount < _maxTracks)
                            return; // 释放了足够轨道
                    }
                }
            }
        }

        #endregion

        #region ─── Internal – Audio Source Management ─

        /// <summary>
        /// 配置AudioSource
        /// </summary>
        private void ConfigureAudioSource(AudioSource source, AudioClip clip, int priority, float volume, Vector3? position)
        {
            source.clip = clip;
            source.volume = volume;
            source.loop = false;
            source.playOnAwake = false;
            source.priority = 128 - priority * 25; // Unity priority (0=最高, 255=最低) 反向映射

            if (position.HasValue)
            {
                // 3D音效
                source.transform.position = position.Value;
                source.spatialBlend = 1.0f;
                source.minDistance = 1f;
                source.maxDistance = 50f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
            }
            else
            {
                // 2D音效
                source.spatialBlend = 0f;
            }
        }

        /// <summary>
        /// 分配AudioMixer分组
        /// </summary>
        private void AssignMixerGroup(AudioSource source, int priority)
        {
            if (_masterMixer == null) return;

            string groupName = GetMixerGroupName((SFXPriority)priority);
            if (_mixerGroups.TryGetValue(groupName, out var group))
            {
                source.outputAudioMixerGroup = group;
            }
        }

        /// <summary>
        /// 获取Mixer分组名称
        /// </summary>
        private string GetMixerGroupName(SFXPriority priority)
        {
            return priority switch
            {
                SFXPriority.BabyEmotion => MIXER_SFX_BABY,
                SFXPriority.BossEntrance => MIXER_SFX_WEAPON,
                SFXPriority.ComboTrigger => MIXER_SFX_WEAPON,
                SFXPriority.CardWeapon => MIXER_SFX_CARD,
                SFXPriority.Ambient => MIXER_SFX_AMBIENT,
                _ => MIXER_MASTER
            };
        }

        /// <summary>
        /// 设置Mixer音量 (线性→分贝转换)
        /// </summary>
        private void SetMixerVolume(string groupName, float linearVolume)
        {
            if (_masterMixer == null) return;

            string exposedParam = groupName + "Volume";
            float db = LinearToDecibel(linearVolume);
            _masterMixer.SetFloat(exposedParam, db);
        }

        /// <summary>
        /// 线性音量值转分贝值
        /// </summary>
        private float LinearToDecibel(float linear)
        {
            if (linear <= 0f) return -80f; // 静音
            return 20f * Mathf.Log10(linear);
        }

        /// <summary>
        /// 创建临时AudioSource（池耗尽时）
        /// </summary>
        private AudioSource CreateTempAudioSource()
        {
            GameObject tempGo = new GameObject("TempSFX");
            tempGo.transform.SetParent(transform);
            AudioSource source = tempGo.AddComponent<AudioSource>();
            return source;
        }

        #endregion

        #region ─── Internal – Auto Release ─────────────

        /// <summary>
        /// 音效播放完成后自动回收
        /// </summary>
        private System.Collections.IEnumerator AutoReleaseRoutine(AudioSource source, float clipLength, SFXPoolCategory category, ActiveSFXRecord record)
        {
            yield return new WaitForSeconds(clipLength + 0.1f); // 略长以防止截断

            // 从活跃列表移除
            _activeSFX.Remove(record);
            _activeTrackCount--;

            // 回收到对象池
            SFXPool pool = GetPool(category);
            pool.Release(source);
        }

        /// <summary>
        /// 释放SFX记录资源
        /// </summary>
        private void ReleaseSFXRecord(ActiveSFXRecord record)
        {
            if (record.source != null)
            {
                record.source.Stop();
                SFXPool pool = GetPool(record.category);
                pool.Release(record.source);
            }
        }

        #endregion

        #region ─── Internal – Clip Lookup ──────────────

        /// <summary>
        /// 获取婴儿情感音效片段数组
        /// </summary>
        private AudioClip[] GetBabyEmotionClips(EmotionState state)
        {
            return state switch
            {
                EmotionState.CURIOUS => _babyCuriousClips,
                EmotionState.FEAR => _babyFearClips,
                EmotionState.ANGER => _babyAngerClips,
                EmotionState.TIRED => _babyTiredClips,
                EmotionState.AWAKENING => _babyAwakeningClips,
                _ => null
            };
        }

        /// <summary>
        /// 从字典资产中查找音效片段
        /// </summary>
        private AudioClip GetClipFromDict(DictionaryAsset dict, string key)
        {
            if (dict == null) return null;
            return dict.GetClip(key);
        }

        #endregion

        #region ─── Internal – Pool Access ──────────────

        /// <summary>
        /// 获取指定分类的对象池
        /// </summary>
        private SFXPool GetPool(SFXPoolCategory category)
        {
            if (_pools.TryGetValue(category, out var pool))
                return pool;

            // 如果池不存在，创建一个默认池
            var newPool = new SFXPool(5, false);
            _pools[category] = newPool;
            return newPool;
        }

        #endregion

        #region ─── Debug ───────────────────────────────

        [ContextMenu("Log Audio State")]
        private void LogAudioState()
        {
            Debug.Log($"[AudioManager] Platform: {(IsMobilePlatform ? "Mobile" : "PC")}, " +
                      $"MaxTracks: {_maxTracks}, Active: {_activeTrackCount}\n" +
                      $"  Volumes: Baby={_babyEmotionVolume}, Weapon={_weaponHitVolume}, " +
                      $"Card={_cardVolume}, Ambient={_ambientVolume}, UI={_uiVolume}, BGM={_bgmVolume}\n" +
                      $"  Pools: WeaponHit={_pools.GetValueOrDefault(SFXPoolCategory.WeaponHit)?.AvailableCount ?? 0}, " +
                      $"CardRelease={_pools.GetValueOrDefault(SFXPoolCategory.CardRelease)?.AvailableCount ?? 0}, " +
                      $"EnemyHit={_pools.GetValueOrDefault(SFXPoolCategory.EnemyHit)?.AvailableCount ?? 0}, " +
                      $"UI={_pools.GetValueOrDefault(SFXPoolCategory.UI)?.AvailableCount ?? 0}");
        }

        #endregion

        #region ─── Inner Types ─────────────────────────

        /// <summary>
        /// 活跃音效记录
        /// </summary>
        private struct ActiveSFXRecord
        {
            public AudioSource source;
            public int priority;
            public SFXPoolCategory category;
            public float startTime;
            public float clipLength;
        }

        /// <summary>
        /// 引用EmotionState枚举 (from Baby namespace)
        /// 注意: 这里使用int映射来避免跨命名空间引用问题
        /// 实际使用时应通过EmotionStateMachine.EmotionState
        /// </summary>
        public enum EmotionState
        {
            CURIOUS,
            FEAR,
            ANGER,
            TIRED,
            AWAKENING
        }

        #endregion
    }

    // ──────────────────────────────────────────────────
    //  DictionaryAsset – AudioClip lookup helper
    // ──────────────────────────────────────────────────

    /// <summary>
    /// ScriptableObject用于存储key→AudioClip的映射
    /// 创建: Assets → Create → Angel Guardian → Audio Dictionary
    /// </summary>
    [CreateAssetMenu(fileName = "AudioDictionary", menuName = "Angel Guardian/Audio Dictionary")]
    [Serializable]
    public class DictionaryAsset : ScriptableObject
    {
        [SerializeField] private List<ClipEntry> _entries = new List<ClipEntry>();

        public AudioClip GetClip(string key)
        {
            var entry = _entries.Find(e => e.key == key);
            return entry?.clip;
        }

        public void SetClip(string key, AudioClip clip)
        {
            var entry = _entries.Find(e => e.key == key);
            if (entry != null)
            {
                entry.clip = clip;
            }
            else
            {
                _entries.Add(new ClipEntry { key = key, clip = clip });
            }
        }

        [Serializable]
        public class ClipEntry
        {
            public string key;
            public AudioClip clip;
        }
    }
}
