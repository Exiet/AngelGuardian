using System;
using System.Collections.Generic;
using UnityEngine;

namespace AngelGuardian.Audio
{
    // ──────────────────────────────────────────────────
    //  SFXPool – AudioSource Object Pool
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 音效对象池 —— 泛型AudioSource池
    /// 
    /// 功能:
    /// - Get()/Release() 获取和归还AudioSource
    /// - 自动音量设置
    /// - 3D/2D音效支持
    /// - 池容量上限和自动扩容
    /// - 与AudioManager配合使用
    /// </summary>
    public class SFXPool
    {
        #region ─── Fields ─────────────────────────────

        private readonly Stack<AudioSource> _available = new Stack<AudioSource>();
        private readonly List<AudioSource> _allObjects = new List<AudioSource>();
        private readonly HashSet<AudioSource> _activeSet = new HashSet<AudioSource>();

        private readonly Transform _parent;
        private readonly int _initialCapacity;
        private readonly int _maxSize;
        private readonly bool _autoExpand;

        private int _activeCount = 0;

        #endregion

        #region ─── Properties ──────────────────────────

        /// <summary>池中可用AudioSource数量</summary>
        public int AvailableCount => _available.Count;

        /// <summary>当前活跃（正在播放）数量</summary>
        public int ActiveCount => _activeCount;

        /// <summary>池管理的AudioSource总数</summary>
        public int TotalCount => _allObjects.Count;

        /// <summary>池最大容量 (0=无限)</summary>
        public int MaxSize => _maxSize;

        #endregion

        #region ─── Constructors ────────────────────────

        /// <summary>
        /// 创建音效对象池
        /// </summary>
        /// <param name="initialCapacity">初始预创建数量</param>
        /// <param name="is3D">是否默认使用3D音效</param>
        /// <param name="parent">父Transform (null则自动创建)</param>
        /// <param name="maxSize">最大容量 (0=无限)</param>
        /// <param name="autoExpand">是否自动扩容</param>
        public SFXPool(int initialCapacity, bool is3D = false, Transform parent = null, int maxSize = 0, bool autoExpand = true)
        {
            _initialCapacity = Mathf.Max(1, initialCapacity);
            _maxSize = Mathf.Max(0, maxSize);
            _autoExpand = autoExpand;

            // 创建父对象（如果未提供）
            if (parent != null)
            {
                _parent = parent;
            }
            else
            {
                GameObject poolParent = new GameObject("SFXPool_" + _initialCapacity);
                poolParent.transform.SetParent(AudioManager.Instance?.transform);
                _parent = poolParent.transform;
            }

            // 预创建AudioSource
            for (int i = 0; i < _initialCapacity; i++)
            {
                AudioSource source = CreateNewSource(is3D);
                _allObjects.Add(source);
                _available.Push(source);
            }
        }

        #endregion

        #region ─── Public API ──────────────────────────

        /// <summary>
        /// 从池中获取一个AudioSource
        /// 池空时根据autoExpand决定是否创建新实例
        /// </summary>
        /// <param name="volume">设置音量</param>
        /// <param name="is3D">是否3D音效</param>
        /// <param name="position">3D音效位置</param>
        /// <returns>可用的AudioSource，池耗尽且不扩容时返回null</returns>
        public AudioSource Get(float volume = 1f, bool is3D = false, Vector3? position = null)
        {
            AudioSource source;

            if (_available.Count > 0)
            {
                source = _available.Pop();
            }
            else if (_autoExpand)
            {
                // 检查最大容量
                if (_maxSize > 0 && _allObjects.Count >= _maxSize)
                {
                    Debug.LogWarning($"[SFXPool] Pool reached max size ({_maxSize}). Returning null.");
                    return null;
                }

                source = CreateNewSource(is3D);
                _allObjects.Add(source);
            }
            else
            {
                Debug.LogWarning($"[SFXPool] Pool exhausted and auto-expand is disabled.");
                return null;
            }

            // 配置AudioSource
            ConfigureSource(source, volume, is3D, position);

            _activeCount++;
            _activeSet.Add(source);
            return source;
        }

        /// <summary>
        /// 归还AudioSource到池中
        /// </summary>
        /// <param name="source">要归还的AudioSource</param>
        public void Release(AudioSource source)
        {
            if (source == null)
            {
                Debug.LogError("[SFXPool] Attempted to release a null AudioSource.");
                return;
            }

            if (!_activeSet.Contains(source))
            {
                Debug.LogWarning("[SFXPool] Attempted to release an AudioSource not tracked by this pool.");
                return;
            }

            // 重置AudioSource状态
            source.Stop();
            source.clip = null;
            source.volume = 0f;
            source.spatialBlend = 0f;
            source.loop = false;
            source.transform.SetParent(_parent);
            source.gameObject.SetActive(false);

            _activeSet.Remove(source);
            _activeCount = Mathf.Max(0, _activeCount - 1);

            // 如果池已达到最大容量，销毁而非归还
            if (_maxSize > 0 && _available.Count >= _maxSize)
            {
                DestroySource(source);
                _allObjects.Remove(source);
            }
            else
            {
                _available.Push(source);
            }
        }

        /// <summary>
        /// 预热池（额外创建指定数量）
        /// </summary>
        /// <param name="count">额外创建数量</param>
        /// <param name="is3D">是否3D音效</param>
        public void PreWarm(int count, bool is3D = false)
        {
            int toCreate = Mathf.Min(count, _maxSize > 0 ? _maxSize - _allObjects.Count : count);

            for (int i = 0; i < toCreate; i++)
            {
                if (_maxSize > 0 && _allObjects.Count >= _maxSize)
                    break;

                AudioSource source = CreateNewSource(is3D);
                _allObjects.Add(source);
                _available.Push(source);
            }
        }

        /// <summary>
        /// 清理池（销毁所有AudioSource）
        /// </summary>
        public void Clear()
        {
            foreach (AudioSource source in _allObjects)
            {
                DestroySource(source);
            }

            _available.Clear();
            _allObjects.Clear();
            _activeSet.Clear();
            _activeCount = 0;
        }

        /// <summary>
        /// 归还所有活跃AudioSource
        /// </summary>
        public void ReleaseAllActive()
        {
            foreach (AudioSource source in _activeSet)
            {
                if (source != null)
                {
                    source.Stop();
                    source.clip = null;
                    source.volume = 0f;
                    source.gameObject.SetActive(false);
                    _available.Push(source);
                }
            }

            _activeSet.Clear();
            _activeCount = 0;
        }

        #endregion

        #region ─── Internal – Source Creation ──────────

        /// <summary>
        /// 创建新的AudioSource
        /// </summary>
        private AudioSource CreateNewSource(bool is3D)
        {
            GameObject go = new GameObject("SFXSource");
            go.transform.SetParent(_parent);
            go.SetActive(false); // 默认不可见

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0f;

            // 3D音效配置
            if (is3D)
            {
                source.spatialBlend = 1.0f;
                source.minDistance = 1f;
                source.maxDistance = 50f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
            }
            else
            {
                source.spatialBlend = 0f; // 2D
            }

            return source;
        }

        /// <summary>
        /// 销毁AudioSource及其GameObject
        /// </summary>
        private void DestroySource(AudioSource source)
        {
            if (source != null && source.gameObject != null)
            {
                UnityEngine.Object.Destroy(source.gameObject);
            }
        }

        #endregion

        #region ─── Internal – Source Configuration ─────

        /// <summary>
        /// 配置AudioSource属性
        /// </summary>
        private void ConfigureSource(AudioSource source, float volume, bool is3D, Vector3? position)
        {
            source.gameObject.SetActive(true);

            // 音量
            source.volume = Mathf.Clamp01(volume);

            // 3D/2D设置
            if (is3D)
            {
                source.spatialBlend = 1.0f;
                source.minDistance = 1f;
                source.maxDistance = 50f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;

                if (position.HasValue)
                {
                    source.transform.position = position.Value;
                }
            }
            else
            {
                source.spatialBlend = 0f;
            }

            // 如果有AudioMixer分组，从AudioManager获取并分配
            if (AudioManager.Instance != null)
            {
                // Mixer分组由AudioManager在播放时分配，这里不重复设置
            }
        }

        #endregion

        #region ─── Debug ───────────────────────────────

        /// <summary>
        /// 获取池状态摘要字符串
        /// </summary>
        public string GetStatusString()
        {
            return $"[SFXPool] Available: {_available.Count} | Active: {_activeCount} | Total: {_allObjects.Count} | Max: {_maxSize}";
        }

        #endregion
    }

    // ──────────────────────────────────────────────────
    //  SFXPoolManager – Pool Collection Manager
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 音效池管理器 —— 集中管理多个SFXPool实例
    /// 作为AudioManager的内部辅助类
    /// 
    /// 池规格:
    /// - WeaponHit:    10个AudioSource
    /// - CardRelease:  5个AudioSource
    /// - EnemyHit:     8个AudioSource
    /// - UI:           3个AudioSource
    /// </summary>
    public static class SFXPoolManager
    {
        /// <summary>
        /// 创建标准池集合（按规格）
        /// </summary>
        /// <param name="parent">池集合父Transform</param>
        /// <returns>分类→池的字典</returns>
        public static Dictionary<SFXPoolCategory, SFXPool> CreateStandardPools(Transform parent)
        {
            var pools = new Dictionary<SFXPoolCategory, SFXPool>
            {
                [SFXPoolCategory.WeaponHit]   = new SFXPool(10, true, parent, 0, true),   // 武器命中(10), 3D, 无限扩展
                [SFXPoolCategory.CardRelease] = new SFXPool(5, false, parent, 0, true),    // 卡牌释放(5), 2D
                [SFXPoolCategory.EnemyHit]    = new SFXPool(8, true, parent, 0, true),     // 敌人受伤(8), 3D
                [SFXPoolCategory.UI]          = new SFXPool(3, false, parent, 0, true)      // UI音效(3), 2D
            };

            Debug.Log("[SFXPoolManager] Standard pools created: " +
                      $"WeaponHit=10, CardRelease=5, EnemyHit=8, UI=3");

            return pools;
        }

        /// <summary>
        /// 获取池默认容量（按规格）
        /// </summary>
        public static int GetPoolDefaultSize(SFXPoolCategory category)
        {
            return category switch
            {
                SFXPoolCategory.WeaponHit => 10,
                SFXPoolCategory.CardRelease => 5,
                SFXPoolCategory.EnemyHit => 8,
                SFXPoolCategory.UI => 3,
                _ => 5
            };
        }

        /// <summary>
        /// 获取池是否默认使用3D音效
        /// </summary>
        public static bool IsPoolDefault3D(SFXPoolCategory category)
        {
            return category switch
            {
                SFXPoolCategory.WeaponHit => true,
                SFXPoolCategory.CardRelease => false,
                SFXPoolCategory.EnemyHit => true,
                SFXPoolCategory.UI => false,
                _ => false
            };
        }
    }
}
