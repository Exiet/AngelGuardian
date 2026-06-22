using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// 敌人生成器 - 管理波次、对象池、难度曲线和生成位置
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        #region 单例

        public static EnemySpawner Instance { get; private set; }

        #endregion

        #region 序列化字段

        [Header("=== 波次数据库 ===")]
        [SerializeField] private WaveDatabase waveDatabase;

        [Header("=== 生成设置 ===")]
        [SerializeField] private Transform spawnAreaCenter;
        [SerializeField] private float spawnAreaWidth = 30f;
        [SerializeField] private float spawnAreaHeight = 20f;
        [SerializeField] private float safeZoneRadius = 300f;
        [SerializeField] private LayerMask groundLayer;

        [Header("=== 对象池 ===")]
        [SerializeField] private int defaultPoolSize = 10;
        [SerializeField] private int maxPoolSizePerType = 30;

        [Header("=== 调试 ===")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool autoStartWaves = false;

        #endregion

        #region 私有字段

        private int currentWaveIndex = -1;
        private int enemiesAlive = 0;
        private int enemiesOnScreen = 0;
        private int maxEnemiesOnScreen = 30;
        private bool isSpawning = false;
        private bool isWaveActive = false;
        private Coroutine waveCoroutine;

        // 对象池
        private Dictionary<EnemyType, Queue<EnemyBase>> enemyPool = new Dictionary<EnemyType, Queue<EnemyBase>>();
        private Dictionary<EnemyType, GameObject> enemyPrefabCache = new Dictionary<EnemyType, GameObject>();

        // 当前波次的难度倍率
        private float currentHPMultiplier = 1f;
        private float currentAttackMultiplier = 1f;
        private float currentSpeedMultiplier = 1f;

        // 活跃敌人追踪
        private List<EnemyBase> activeEnemies = new List<EnemyBase>();

        #endregion

        #region 事件

        /// <summary>波次开始事件</summary>
        public event Action<int, string> OnWaveStarted;

        /// <summary>波次完成事件</summary>
        public event Action<int> OnWaveCompleted;

        /// <summary>所有波次完成事件</summary>
        public event Action OnAllWavesCompleted;

        /// <summary>Boss生成事件</summary>
        public event Action<EnemyBase> OnBossSpawned;

        /// <summary>敌人生成事件</summary>
        public event Action<EnemyBase> OnEnemySpawned;

        #endregion

        #region 属性

        public int CurrentWaveIndex => currentWaveIndex;
        public int EnemiesAlive => enemiesAlive;
        public int EnemiesOnScreen => enemiesOnScreen;
        public bool IsWaveActive => isWaveActive;
        public int TotalWaves => waveDatabase != null ? waveDatabase.TotalWaves : 0;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (spawnAreaCenter == null)
            {
                spawnAreaCenter = transform;
            }
        }

        private void Start()
        {
            InitializeObjectPool();

            if (autoStartWaves)
            {
                StartWaves();
            }
        }

        private void Update()
        {
            // 清理已死亡敌人的引用
            CleanupDeadEnemies();
        }

        #endregion

        #region 波次管理

        /// <summary>
        /// 开始波次系统
        /// </summary>
        public void StartWaves()
        {
            if (waveDatabase == null || waveDatabase.waves.Count == 0)
            {
                Debug.LogWarning("[EnemySpawner] 没有配置波次数据!");
                return;
            }

            if (!isSpawning)
            {
                StartCoroutine(WaveLoopCoroutine());
            }
        }

        /// <summary>
        /// 波次循环协程
        /// </summary>
        private IEnumerator WaveLoopCoroutine()
        {
            isSpawning = true;

            for (int i = 0; i < waveDatabase.TotalWaves; i++)
            {
                currentWaveIndex = i;
                WaveConfig wave = waveDatabase.GetWave(i);

                if (wave == null) continue;

                // 波次前等待
                yield return new WaitForSeconds(wave.preWaveDelay);

                // 应用难度倍率
                ApplyDifficultyModifiers(wave);

                // 设置同屏上限
                maxEnemiesOnScreen = wave.maxEnemiesOnScreen;

                // 安全区域
                safeZoneRadius = wave.safeZoneRadius;

                // 开始波次
                isWaveActive = true;
                OnWaveStarted?.Invoke(i, wave.waveName);
                Debug.Log($"[EnemySpawner] 波次 {i + 1}: {wave.waveName} 开始!");

                // 生成该波次的所有敌人
                yield return StartCoroutine(SpawnWaveEnemies(wave));

                // 等待所有敌人被消灭
                yield return StartCoroutine(WaitForWaveClear());

                // 波次完成
                isWaveActive = false;
                OnWaveCompleted?.Invoke(i);
                Debug.Log($"[EnemySpawner] 波次 {i + 1}: {wave.waveName} 完成!");
            }

            isSpawning = false;
            OnAllWavesCompleted?.Invoke();
            Debug.Log("[EnemySpawner] 所有波次完成!");
        }

        /// <summary>
        /// 生成波次敌人
        /// </summary>
        private IEnumerator SpawnWaveEnemies(WaveConfig wave)
        {
            foreach (var entry in wave.enemyEntries)
            {
                // 等待生成延迟
                if (entry.spawnDelay > 0f)
                {
                    yield return new WaitForSeconds(entry.spawnDelay);
                }

                // 生成该类型的敌人
                for (int i = 0; i < entry.count; i++)
                {
                    // 等待同屏数量释放
                    yield return StartCoroutine(WaitForScreenSlot());

                    // 等待生成间隔
                    if (i > 0 || wave.spawnInterval > 0f)
                    {
                        yield return new WaitForSeconds(wave.spawnInterval);
                    }

                    // 获取生成位置
                    Vector2 spawnPos = GetSpawnPosition(entry.spawnPositionType, entry.isBoss);

                    // 从对象池获取或实例化敌人
                    EnemyBase enemy = SpawnEnemy(entry.enemyType, spawnPos, entry.isBoss);

                    if (enemy != null)
                    {
                        // 应用难度倍率
                        enemy.DifficultyHPModifier = currentHPMultiplier;
                        enemy.DifficultyAttackModifier = currentAttackMultiplier;
                        enemy.DifficultySpeedModifier = currentSpeedMultiplier;

                        // Boss特殊处理
                        if (entry.isBoss)
                        {
                            OnBossSpawned?.Invoke(enemy);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 等待所有敌人被消灭
        /// </summary>
        private IEnumerator WaitForWaveClear()
        {
            while (enemiesAlive > 0 || enemiesOnScreen > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            // 额外等待确保清理完毕
            yield return new WaitForSeconds(1f);
        }

        /// <summary>
        /// 等待同屏槽位释放
        /// </summary>
        private IEnumerator WaitForScreenSlot()
        {
            while (enemiesOnScreen >= maxEnemiesOnScreen)
            {
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>
        /// 跳过当前波次（调试用）
        /// </summary>
        public void SkipCurrentWave()
        {
            if (!isWaveActive) return;

            // 消灭所有活跃敌人
            foreach (var enemy in activeEnemies.ToArray())
            {
                if (enemy != null && enemy.CurrentState != EnemyState.Dead)
                {
                    enemy.TakeDamage(float.MaxValue, DamageType.True);
                }
            }
        }

        #endregion

        #region 生成位置计算

        /// <summary>
        /// 根据生成类型获取生成位置
        /// </summary>
        private Vector2 GetSpawnPosition(SpawnPositionType type, bool isBoss)
        {
            Vector2 center = spawnAreaCenter != null ? (Vector2)spawnAreaCenter.position : Vector2.zero;

            switch (type)
            {
                case SpawnPositionType.AllDirections:
                    return GetAllDirectionsSpawn(center);

                case SpawnPositionType.RallyPoint:
                    return GetRallyPointSpawn(center);

                case SpawnPositionType.SkyDrop:
                    return GetSkyDropSpawn(center);

                case SpawnPositionType.BackLine:
                    return GetBackLineSpawn(center);

                case SpawnPositionType.FrontLine:
                    return GetFrontLineSpawn(center);

                case SpawnPositionType.LeftFlank:
                    return GetLeftFlankSpawn(center);

                case SpawnPositionType.RightFlank:
                    return GetRightFlankSpawn(center);

                case SpawnPositionType.BossPosition:
                    return GetBossSpawn(center);

                default:
                    return GetAllDirectionsSpawn(center);
            }
        }

        /// <summary>
        /// 四面八方生成 - 在区域边缘随机位置
        /// </summary>
        private Vector2 GetAllDirectionsSpawn(Vector2 center)
        {
            // 随机选择一条边
            int edge = Random.Range(0, 4);
            float halfW = spawnAreaWidth * 0.5f;
            float halfH = spawnAreaHeight * 0.5f;

            return edge switch
            {
                0 => center + new Vector2(Random.Range(-halfW, halfW), halfH),        // 上边
                1 => center + new Vector2(Random.Range(-halfW, halfW), -halfH),       // 下边
                2 => center + new Vector2(-halfW, Random.Range(-halfH, halfH)),       // 左边
                _ => center + new Vector2(halfW, Random.Range(-halfH, halfH)),        // 右边
            };
        }

        /// <summary>
        /// 集结生成 - 在指定集结区域生成
        /// </summary>
        private Vector2 GetRallyPointSpawn(Vector2 center)
        {
            float rallyX = center.x + Random.Range(-spawnAreaWidth * 0.3f, spawnAreaWidth * 0.3f);
            float rallyY = center.y - spawnAreaHeight * 0.3f;
            return new Vector2(rallyX, rallyY);
        }

        /// <summary>
        /// 天空生成 - 从上方掉落
        /// </summary>
        private Vector2 GetSkyDropSpawn(Vector2 center)
        {
            float dropX = center.x + Random.Range(-spawnAreaWidth * 0.5f, spawnAreaWidth * 0.5f);
            float dropY = center.y + spawnAreaHeight * 0.6f;
            return new Vector2(dropX, dropY);
        }

        /// <summary>
        /// 后排生成
        /// </summary>
        private Vector2 GetBackLineSpawn(Vector2 center)
        {
            float backX = center.x + Random.Range(-spawnAreaWidth * 0.3f, spawnAreaWidth * 0.3f);
            float backY = center.y + Random.Range(spawnAreaHeight * 0.2f, spawnAreaHeight * 0.5f);
            return new Vector2(backX, backY);
        }

        /// <summary>
        /// 前排生成
        /// </summary>
        private Vector2 GetFrontLineSpawn(Vector2 center)
        {
            float frontX = center.x + Random.Range(-spawnAreaWidth * 0.5f, spawnAreaWidth * 0.5f);
            float frontY = center.y - spawnAreaHeight * 0.3f;
            return new Vector2(frontX, frontY);
        }

        /// <summary>
        /// 左侧生成
        /// </summary>
        private Vector2 GetLeftFlankSpawn(Vector2 center)
        {
            float leftX = center.x - spawnAreaWidth * 0.4f;
            float leftY = center.y + Random.Range(-spawnAreaHeight * 0.4f, spawnAreaHeight * 0.4f);
            return new Vector2(leftX, leftY);
        }

        /// <summary>
        /// 右侧生成
        /// </summary>
        private Vector2 GetRightFlankSpawn(Vector2 center)
        {
            float rightX = center.x + spawnAreaWidth * 0.4f;
            float rightY = center.y + Random.Range(-spawnAreaHeight * 0.4f, spawnAreaHeight * 0.4f);
            return new Vector2(rightX, rightY);
        }

        /// <summary>
        /// Boss生成位置 - 屏幕中央偏上
        /// </summary>
        private Vector2 GetBossSpawn(Vector2 center)
        {
            return center + new Vector2(0f, spawnAreaHeight * 0.25f);
        }

        #endregion

        #region 安全区域检查

        /// <summary>
        /// 检查位置是否在安全区域内
        /// </summary>
        public bool IsInSafeZone(Vector2 position)
        {
            // 获取天使或婴儿位置（如果有的话）
            Vector2 safeCenter = GetSafeZoneCenter();
            return Vector2.Distance(position, safeCenter) < safeZoneRadius;
        }

        /// <summary>
        /// 获取安全区域中心
        /// </summary>
        private Vector2 GetSafeZoneCenter()
        {
            // 尝试查找天使
            var angel = GameObject.FindGameObjectWithTag("Angel");
            if (angel != null) return angel.transform.position;

            // 尝试查找婴儿
            var baby = GameObject.FindGameObjectWithTag("Baby");
            if (baby != null) return baby.transform.position;

            // 默认使用生成区域中心
            return spawnAreaCenter != null ? (Vector2)spawnAreaCenter.position : Vector2.zero;
        }

        /// <summary>
        /// 获取安全区域外的有效生成位置
        /// </summary>
        private Vector2 GetSafeSpawnPosition(SpawnPositionType type, bool isBoss)
        {
            Vector2 position;
            int maxAttempts = 30;
            int attempts = 0;

            do
            {
                position = GetSpawnPosition(type, isBoss);
                attempts++;
            }
            while (IsInSafeZone(position) && attempts < maxAttempts);

            return position;
        }

        #endregion

        #region 对象池管理

        /// <summary>
        /// 初始化对象池
        /// </summary>
        private void InitializeObjectPool()
        {
            if (waveDatabase == null) return;

            foreach (var mapping in waveDatabase.enemyPrefabs)
            {
                if (mapping.prefab == null) continue;

                enemyPrefabCache[mapping.enemyType] = mapping.prefab;
                enemyPool[mapping.enemyType] = new Queue<EnemyBase>();

                // 预实例化对象
                for (int i = 0; i < defaultPoolSize; i++)
                {
                    CreatePooledEnemy(mapping.enemyType, mapping.prefab);
                }
            }

            Debug.Log($"[EnemySpawner] 对象池初始化完成: {enemyPool.Count} 种敌人类型");
        }

        /// <summary>
        /// 创建对象池中的敌人
        /// </summary>
        private EnemyBase CreatePooledEnemy(EnemyType type, GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, Vector2.zero, Quaternion.identity, transform);
            obj.SetActive(false);

            EnemyBase enemy = obj.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.OnDespawn();
            }

            enemyPool[type].Enqueue(enemy);
            return enemy;
        }

        /// <summary>
        /// 从对象池获取或实例化敌人
        /// </summary>
        public EnemyBase SpawnEnemy(EnemyType type, Vector2 position, bool isBoss = false)
        {
            EnemyBase enemy = null;

            // 尝试从对象池获取
            if (enemyPool.ContainsKey(type) && enemyPool[type].Count > 0)
            {
                enemy = enemyPool[type].Dequeue();
                enemy.transform.position = position;
                enemy.OnSpawn();
            }
            else if (enemyPrefabCache.ContainsKey(type))
            {
                // 池中没有，但还没超过上限则创建新的
                int currentPoolCount = enemyPool.ContainsKey(type) ? enemyPool[type].Count : 0;
                if (currentPoolCount < maxPoolSizePerType)
                {
                    GameObject obj = Instantiate(enemyPrefabCache[type], position, Quaternion.identity, transform);
                    enemy = obj.GetComponent<EnemyBase>();
                    if (enemy != null)
                    {
                        enemy.OnSpawn();
                    }
                }
                else
                {
                    Debug.LogWarning($"[EnemySpawner] 敌人类型 {type} 的对象池已达上限 ({maxPoolSizePerType})，无法生成更多!");
                    return null;
                }
            }
            else
            {
                Debug.LogError($"[EnemySpawner] 未找到敌人类型 {type} 的预制体!");
                return null;
            }

            if (enemy != null)
            {
                // 追踪
                activeEnemies.Add(enemy);
                enemiesAlive++;
                enemiesOnScreen++;

                // 监听死亡事件
                enemy.OnEnemyDamaged += HandleEnemyDamaged;

                OnEnemySpawned?.Invoke(enemy);

                // 注册到全局击杀事件
                AngelGuardian.Enemies.EnemyBase.OnEnemyKilled += HandleEnemyKilled;
            }

            return enemy;
        }

        /// <summary>
        /// 回收敌人到对象池
        /// </summary>
        public void DespawnEnemy(EnemyBase enemy)
        {
            if (enemy == null) return;

            activeEnemies.Remove(enemy);
            enemiesOnScreen--;

            EnemyType? type = GetEnemyType(enemy);
            if (type.HasValue && enemyPool.ContainsKey(type.Value))
            {
                enemy.OnDespawn();
                enemyPool[type.Value].Enqueue(enemy);
            }
            else
            {
                Destroy(enemy.gameObject);
            }
        }

        /// <summary>
        /// 获取敌人类型
        /// </summary>
        private EnemyType? GetEnemyType(EnemyBase enemy)
        {
            foreach (var kvp in enemyPrefabCache)
            {
                var prefabEnemy = kvp.Value.GetComponent<EnemyBase>();
                if (prefabEnemy != null && prefabEnemy.EnemyId == enemy.EnemyId)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理敌人被击杀
        /// </summary>
        private void HandleEnemyKilled(string enemyId, string enemyName, Vector3 killPosition)
        {
            enemiesAlive--;
            enemiesOnScreen--;
        }

        /// <summary>
        /// 处理敌人受伤
        /// </summary>
        private void HandleEnemyDamaged(string enemyId, float damage, float currentHP, float maxHP)
        {
            // 可以用于UI更新或其他逻辑
        }

        /// <summary>
        /// 清理已死亡/回收的敌人引用
        /// </summary>
        private void CleanupDeadEnemies()
        {
            activeEnemies.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy || e.CurrentState == EnemyState.Dead);

            // 重新计数
            int aliveCount = 0;
            int screenCount = 0;
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null && enemy.CurrentState != EnemyState.Dead)
                {
                    aliveCount++;
                    if (enemy.gameObject.activeInHierarchy)
                        screenCount++;
                }
            }
            enemiesAlive = aliveCount;
            enemiesOnScreen = screenCount;
        }

        #endregion

        #region 难度系统

        /// <summary>
        /// 应用波次难度倍率
        /// </summary>
        private void ApplyDifficultyModifiers(WaveConfig wave)
        {
            currentHPMultiplier = wave.difficultyHPMultiplier;
            currentAttackMultiplier = wave.difficultyAttackMultiplier;
            currentSpeedMultiplier = wave.difficultySpeedMultiplier;
        }

        /// <summary>
        /// 设置全局难度倍率（可用于外部难度调整）
        /// </summary>
        public void SetGlobalDifficulty(float hpMultiplier, float attackMultiplier, float speedMultiplier)
        {
            currentHPMultiplier = hpMultiplier;
            currentAttackMultiplier = attackMultiplier;
            currentSpeedMultiplier = speedMultiplier;

            // 应用到所有活跃敌人
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null && enemy.CurrentState != EnemyState.Dead)
                {
                    enemy.DifficultyHPModifier = currentHPMultiplier;
                    enemy.DifficultyAttackModifier = currentAttackMultiplier;
                    enemy.DifficultySpeedModifier = currentSpeedMultiplier;
                }
            }
        }

        #endregion

        #region 编辑器辅助

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            Vector2 center = spawnAreaCenter != null ? (Vector2)spawnAreaCenter.position : (Vector2)transform.position;

            // 绘制生成区域
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireCube(center, new Vector2(spawnAreaWidth, spawnAreaHeight));

            // 绘制安全区域
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(center, safeZoneRadius);

            // 绘制生成位置示例
            Gizmos.color = Color.red;
            var spawnTypes = System.Enum.GetValues(typeof(SpawnPositionType));
            foreach (SpawnPositionType type in spawnTypes)
            {
                Vector2 pos = GetSpawnPosition(type, false);
                Gizmos.DrawSphere(pos, 0.5f);
            }

            // 标签
            UnityEditor.Handles.Label(center + Vector2.up * (spawnAreaHeight * 0.5f + 1f),
                $"Wave: {currentWaveIndex + 1}/{TotalWaves}\nAlive: {enemiesAlive}\nOnScreen: {enemiesOnScreen}");
        }
#endif

        #endregion
    }
}
