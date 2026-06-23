using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// 场景启动器 —— 挂到空场景的任意GameObject上，自动搭建所有必要系统。
    /// 用户只需：创建空场景 → 创建空GameObject → 挂此脚本 → 点击Play
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("快速启动配置")]
        [Tooltip("勾选后，Play时自动创建所有系统GameObject")]
        [SerializeField] private bool _autoBootstrap = true;

        [Tooltip("是否在启动时自动开始游戏")]
        [SerializeField] private bool _autoStartGame = true;

        [Header("预置引用 (可选)")]
        [SerializeField] private GameConfig _gameConfig;
        [SerializeField] private Camera _mainCamera;

        private void Awake()
        {
            if (!_autoBootstrap) return;

            Debug.Log("[SceneBootstrapper] 开始自动搭建场景...");

            // 1. 确保有 Main Camera
            EnsureMainCamera();

            // 2. 创建 GameManager (单例，自动DontDestroyOnLoad)
            EnsureGameManager();

            // 3. 创建 EventBus
            EnsureEventBus();

            // 4. 创建玩家 (Angel)
            EnsurePlayer();

            // 5. 创建婴儿 (Baby)
            EnsureBaby();

            // 6. 创建地牢生成器
            EnsureDungeonGenerator();

            // 7. 创建敌人生成器
            EnsureEnemySpawner();

            // 8. 创建 HUD Canvas
            EnsureHUD();

            // 9. 创建 AudioManager
            EnsureAudioManager();

            Debug.Log("[SceneBootstrapper] 场景搭建完成！");

            if (_autoStartGame)
            {
                // 延迟一帧启动，确保所有Awake执行完毕
                StartCoroutine(AutoStartCoroutine());
            }
        }

        private System.Collections.IEnumerator AutoStartCoroutine()
        {
            yield return null;
            GameManager.Instance?.StartGame();
            Debug.Log("[SceneBootstrapper] 游戏自动开始！");
        }

        #region ─── 系统创建 ─────────────────────────────

        private void EnsureMainCamera()
        {
            if (_mainCamera != null) return;

            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                var camGo = new GameObject("Main Camera");
                _mainCamera = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }

            // 设置为2D正交相机（适合俯视角游戏）
            _mainCamera.orthographic = true;
            _mainCamera.orthographicSize = 800f;
            _mainCamera.transform.position = new Vector3(0, 0, -10);
            _mainCamera.backgroundColor = new Color(0.08f, 0.06f, 0.12f); // 深色地牢背景

            Debug.Log("[SceneBootstrapper] Main Camera 就绪");
        }

        private void EnsureGameManager()
        {
            // GameManager是自动单例，直接访问即可创建
            var gm = GameManager.Instance;
            if (_gameConfig != null)
                gm.Config = _gameConfig;

            Debug.Log("[SceneBootstrapper] GameManager 就绪");
        }

        private void EnsureEventBus()
        {
            var existing = FindObjectOfType<EventBus>();
            if (existing != null) return;

            var go = new GameObject("[EventBus]");
            go.AddComponent<EventBus>();
            Debug.Log("[SceneBootstrapper] EventBus 就绪");
        }

        private void EnsurePlayer()
        {
            var existing = FindObjectOfType<AngelController>();
            if (existing != null) return;

            var go = new GameObject("Angel");
            go.tag = "Player";
            go.transform.position = Vector3.zero;

            // 挂载核心组件（RequireComponent会自动添加依赖）
            go.AddComponent<AngelAttributes>();
            go.AddComponent<AngelController>();
            go.AddComponent<AngelCombat>();
            go.AddComponent<BabyInteraction>();

            // 添加Rigidbody2D（AngelController需要）
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.drag = 8f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            Debug.Log("[SceneBootstrapper] Angel (玩家) 就绪");
        }

        private void EnsureBaby()
        {
            var existing = FindObjectOfType<BabyController>();
            if (existing != null) return;

            var go = new GameObject("Baby");
            go.tag = "Baby";
            go.transform.position = new Vector3(40, 0, 0); // 在玩家旁边

            // 挂载核心组件
            go.AddComponent<BabyAttributes>();
            go.AddComponent<BabyController>();
            go.AddComponent<BabyAI>();
            go.AddComponent<EmotionStateMachine>();

            // Rigidbody2D
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.drag = 6f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            Debug.Log("[SceneBootstrapper] Baby (婴儿) 就绪");
        }

        private void EnsureDungeonGenerator()
        {
            var existing = FindObjectOfType<Dungeon.BSPGenerator>();
            if (existing != null) return;

            var go = new GameObject("[DungeonGenerator]");
            go.AddComponent<Dungeon.BSPGenerator>();
            go.AddComponent<Dungeon.CorridorGenerator>();
            go.AddComponent<Dungeon.DestructibleSpawner>();

            Debug.Log("[SceneBootstrapper] DungeonGenerator 就绪");
        }

        private void EnsureEnemySpawner()
        {
            var existing = FindObjectOfType<Enemies.EnemySpawner>();
            if (existing != null) return;

            var go = new GameObject("[EnemySpawner]");
            go.AddComponent<Enemies.EnemySpawner>();

            Debug.Log("[SceneBootstrapper] EnemySpawner 就绪");
        }

        private void EnsureHUD()
        {
            var existing = FindObjectOfType<UI.HUDController>();
            if (existing != null) return;

            // 创建Canvas
            var canvasGo = new GameObject("HUD_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // HUD Controller
            canvasGo.AddComponent<UI.HUDController>();

            Debug.Log("[SceneBootstrapper] HUD Canvas 就绪");
        }

        private void EnsureAudioManager()
        {
            var existing = FindObjectOfType<Audio.AudioManager>();
            if (existing != null) return;

            var go = new GameObject("[AudioManager]");
            go.AddComponent<Audio.AudioManager>();

            Debug.Log("[SceneBootstrapper] AudioManager 就绪");
        }

        #endregion
    }
}
