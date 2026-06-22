using System;
using System.Collections.Generic;
using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// Generic object pool that supports both UnityEngine.GameObject and plain C# objects.
    /// Features auto-expansion, pre-initialization, and optional parent transform for scene organization.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must be a class.</typeparam>
    public class ObjectPool<T> where T : class
    {
        #region ─── Fields ─────────────────────────────

        private readonly Stack<T> _available = new Stack<T>();
        private readonly List<T> _allObjects = new List<T>();

        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGetCallback;
        private readonly Action<T> _onReleaseCallback;
        private readonly Action<T> _onDestroyCallback;

        private readonly int _initialCapacity;
        private readonly int _maxSize;
        private readonly bool _autoExpand;
        private readonly bool _collectionCheck;

        private int _activeCount = 0;

        #endregion

        #region ─── Properties ──────────────────────────

        /// <summary>Number of objects currently available in the pool.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Number of objects currently checked out (active).</summary>
        public int ActiveCount => _activeCount;

        /// <summary>Total number of objects managed by this pool.</summary>
        public int TotalCount => _allObjects.Count;

        /// <summary>Maximum pool size. 0 = unlimited.</summary>
        public int MaxSize => _maxSize;

        #endregion

        #region ─── Constructors ────────────────────────

        /// <summary>
        /// Creates a new object pool.
        /// </summary>
        /// <param name="createFunc">Factory function to create a new instance.</param>
        /// <param name="onGetCallback">Called when an object is taken from the pool.</param>
        /// <param name="onReleaseCallback">Called when an object is returned to the pool.</param>
        /// <param name="onDestroyCallback">Called when an object is permanently destroyed.</param>
        /// <param name="initialCapacity">Number of objects to pre-instantiate.</param>
        /// <param name="maxSize">Hard cap on pool size. 0 = unlimited.</param>
        /// <param name="autoExpand">If true, pool grows when empty. If false, returns null.</param>
        /// <param name="collectionCheck">If true, throws if releasing an already-released object.</param>
        public ObjectPool(
            Func<T> createFunc,
            Action<T> onGetCallback = null,
            Action<T> onReleaseCallback = null,
            Action<T> onDestroyCallback = null,
            int initialCapacity = 10,
            int maxSize = 0,
            bool autoExpand = true,
            bool collectionCheck = true)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _onGetCallback = onGetCallback;
            _onReleaseCallback = onReleaseCallback;
            _onDestroyCallback = onDestroyCallback;
            _initialCapacity = Mathf.Max(0, initialCapacity);
            _maxSize = Mathf.Max(0, maxSize);
            _autoExpand = autoExpand;
            _collectionCheck = collectionCheck;

            // Pre-instantiate
            for (int i = 0; i < _initialCapacity; i++)
            {
                T obj = _createFunc();
                _allObjects.Add(obj);
                _available.Push(obj);
            }
        }

        #endregion

        #region ─── Public API ──────────────────────────

        /// <summary>
        /// Retrieves an object from the pool. If the pool is empty:
        /// - autoExpand=true: creates a new object.
        /// - autoExpand=false: returns null.
        /// </summary>
        public T Get()
        {
            T obj;

            if (_available.Count > 0)
            {
                obj = _available.Pop();
            }
            else if (_autoExpand)
            {
                // Check max size
                if (_maxSize > 0 && _allObjects.Count >= _maxSize)
                {
                    Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Pool reached max size ({_maxSize}). Cannot expand.");
                    return null;
                }

                obj = _createFunc();
                _allObjects.Add(obj);
            }
            else
            {
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Pool exhausted and auto-expand is disabled.");
                return null;
            }

            _activeCount++;
            _onGetCallback?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// Returns an object to the pool. If the pool is full (maxSize reached),
        /// the object is destroyed instead.
        /// </summary>
        public void Release(T obj)
        {
            if (obj == null)
            {
                Debug.LogError($"[ObjectPool<{typeof(T).Name}>] Attempted to release a null object.");
                return;
            }

            if (_collectionCheck && _available.Contains(obj))
            {
                throw new InvalidOperationException(
                    $"[ObjectPool<{typeof(T).Name}>] Object is already in the pool (double release detected).");
            }

            _onReleaseCallback?.Invoke(obj);
            _activeCount = Mathf.Max(0, _activeCount - 1);

            // If pool is capped and we're over capacity, destroy the object
            if (_maxSize > 0 && _available.Count >= _maxSize)
            {
                _onDestroyCallback?.Invoke(obj);
                _allObjects.Remove(obj);
            }
            else
            {
                _available.Push(obj);
            }
        }

        /// <summary>
        /// Pre-initializes the pool up to the specified count (subject to maxSize).
        /// </summary>
        public void PreWarm(int count)
        {
            int toCreate = Mathf.Min(count, _maxSize > 0 ? _maxSize - _allObjects.Count : count);

            for (int i = 0; i < toCreate; i++)
            {
                if (_maxSize > 0 && _allObjects.Count >= _maxSize)
                    break;

                T obj = _createFunc();
                _allObjects.Add(obj);
                _available.Push(obj);
            }
        }

        /// <summary>
        /// Clears the pool. All objects will be destroyed via the destroy callback.
        /// </summary>
        public void Clear()
        {
            foreach (T obj in _allObjects)
            {
                _onDestroyCallback?.Invoke(obj);
            }

            _available.Clear();
            _allObjects.Clear();
            _activeCount = 0;
        }

        /// <summary>
        /// Returns all currently-active objects to the pool.
        /// Note: This method requires that active objects are tracked externally.
        /// It simply ensures internal counters are consistent.
        /// </summary>
        public void ReleaseAllActive(List<T> activeObjects)
        {
            if (activeObjects == null) return;

            foreach (T obj in activeObjects)
            {
                if (obj != null)
                    Release(obj);
            }

            activeObjects.Clear();
        }

        #endregion
    }

    // ──────────────────────────────────────────────────
    //  GameObject-specific Pool (convenience wrapper)
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Convenience wrapper around ObjectPool&lt;GameObject&gt; with GameObject-specific
    /// activation/deactivation and parent transform support.
    /// </summary>
    public class GameObjectPool
    {
        private readonly ObjectPool<GameObject> _pool;
        private readonly Transform _parent;

        public int AvailableCount => _pool.AvailableCount;
        public int ActiveCount => _pool.ActiveCount;
        public int TotalCount => _pool.TotalCount;

        /// <summary>
        /// Creates a GameObject pool.
        /// </summary>
        /// <param name="prefab">The prefab to instantiate.</param>
        /// <param name="parent">Optional parent transform for scene organization.</param>
        /// <param name="initialCapacity">Number of objects to pre-instantiate.</param>
        /// <param name="maxSize">Hard cap. 0 = unlimited.</param>
        /// <param name="autoExpand">If true, pool grows when empty.</param>
        public GameObjectPool(
            GameObject prefab,
            Transform parent = null,
            int initialCapacity = 10,
            int maxSize = 0,
            bool autoExpand = true)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            _parent = parent;

            _pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    GameObject go = UnityEngine.Object.Instantiate(prefab, parent);
                    go.name = prefab.name; // Remove "(Clone)" suffix for cleanliness
                    go.SetActive(false);
                    return go;
                },
                onGetCallback: (go) =>
                {
                    go.SetActive(true);
                },
                onReleaseCallback: (go) =>
                {
                    go.SetActive(false);
                    if (parent != null)
                        go.transform.SetParent(parent);
                },
                onDestroyCallback: (go) =>
                {
                    UnityEngine.Object.Destroy(go);
                },
                initialCapacity: initialCapacity,
                maxSize: maxSize,
                autoExpand: autoExpand,
                collectionCheck: true
            );
        }

        /// <summary>
        /// Retrieves an inactive GameObject from the pool (or creates a new one).
        /// </summary>
        public GameObject Get() => _pool.Get();

        /// <summary>
        /// Returns a GameObject to the pool (deactivates it).
        /// </summary>
        public void Release(GameObject go) => _pool.Release(go);

        /// <summary>
        /// Pre-warms the pool with additional instances.
        /// </summary>
        public void PreWarm(int count) => _pool.PreWarm(count);

        /// <summary>
        /// Destroys all pooled objects and resets the pool.
        /// </summary>
        public void Clear() => _pool.Clear();

        /// <summary>
        /// Releases all active objects back to the pool.
        /// </summary>
        public void ReleaseAllActive(List<GameObject> activeObjects)
            => _pool.ReleaseAllActive(activeObjects);
    }

    // ──────────────────────────────────────────────────
    //  Component-specific Pool
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Convenience wrapper around ObjectPool&lt;T&gt; for Unity Component types.
    /// Handles GameObject instantiation and component retrieval.
    /// </summary>
    /// <typeparam name="T">The Component type to pool.</typeparam>
    public class ComponentPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;
        private readonly Transform _parent;

        public int AvailableCount => _pool.AvailableCount;
        public int ActiveCount => _pool.ActiveCount;
        public int TotalCount => _pool.TotalCount;

        /// <summary>
        /// Creates a Component pool.
        /// </summary>
        /// <param name="prefab">A GameObject with the T component attached.</param>
        /// <param name="parent">Optional parent transform.</param>
        /// <param name="initialCapacity">Pre-instantiated count.</param>
        /// <param name="maxSize">Hard cap. 0 = unlimited.</param>
        /// <param name="autoExpand">Auto-expand when empty.</param>
        public ComponentPool(
            T prefab,
            Transform parent = null,
            int initialCapacity = 10,
            int maxSize = 0,
            bool autoExpand = true)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            _parent = parent;

            _pool = new ObjectPool<T>(
                createFunc: () =>
                {
                    GameObject go = UnityEngine.Object.Instantiate(prefab.gameObject, parent);
                    go.name = prefab.name;
                    go.SetActive(false);
                    return go.GetComponent<T>();
                },
                onGetCallback: (comp) =>
                {
                    comp.gameObject.SetActive(true);
                },
                onReleaseCallback: (comp) =>
                {
                    comp.gameObject.SetActive(false);
                    if (parent != null)
                        comp.transform.SetParent(parent);
                },
                onDestroyCallback: (comp) =>
                {
                    UnityEngine.Object.Destroy(comp.gameObject);
                },
                initialCapacity: initialCapacity,
                maxSize: maxSize,
                autoExpand: autoExpand,
                collectionCheck: true
            );
        }

        /// <summary>Retrieves a component from the pool.</summary>
        public T Get() => _pool.Get();

        /// <summary>Returns a component's GameObject to the pool.</summary>
        public void Release(T component) => _pool.Release(component);

        /// <summary>Pre-warms the pool.</summary>
        public void PreWarm(int count) => _pool.PreWarm(count);

        /// <summary>Destroys all pooled instances.</summary>
        public void Clear() => _pool.Clear();

        /// <summary>Releases all active objects.</summary>
        public void ReleaseAllActive(List<T> activeObjects)
            => _pool.ReleaseAllActive(activeObjects);
    }
}
