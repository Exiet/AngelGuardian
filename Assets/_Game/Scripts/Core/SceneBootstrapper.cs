using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// 场景启动器（备用方案） —— 如果 AutoBootstrap 没触发，手动挂载此脚本到任意GameObject。
    /// 建议直接使用 AutoBootstrap，无需手动操作。
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("快速启动配置")]
        [SerializeField] private bool _autoBootstrap = true;
        [SerializeField] private bool _autoStartGame = true;
        [SerializeField] private GameConfig _gameConfig;
        [SerializeField] private Camera _mainCamera;

        private void Awake()
        {
            if (!_autoBootstrap) return;

            Debug.Log("[SceneBootstrapper] 手动模式搭建场景...");
            AutoBootstrapHelper.Bootstrap(_gameConfig, _mainCamera);

            if (_autoStartGame)
                StartCoroutine(AutoStartCoroutine());
        }

        private System.Collections.IEnumerator AutoStartCoroutine()
        {
            yield return null;
            yield return null;
            GameManager.Instance?.StartGame();
        }
    }

    /// <summary>
    /// 静态启动逻辑 —— 被 AutoBootstrap 和 SceneBootstrapper 共享
    /// </summary>
    public static class AutoBootstrapHelper
    {
        private static bool _initialized = false;

        public static void Bootstrap(GameConfig gameConfig = null, Camera existingCamera = null)
        {
            if (_initialized) return;
            _initialized = true;

            Debug.Log("═══════════════════════════════════════");
            Debug.Log("  Angel Guardian - 天使护婴");
            Debug.Log("  Roguelike 地宫冒险");
            Debug.Log("═══════════════════════════════════════");

            // 1. GameManager
            var gm = GameManager.Instance;
            if (gameConfig != null) gm.Config = gameConfig;
            Debug.Log("  [1/9] GameManager ✓");

            // 2. EventBus
            CreateIfMissing<EventBus>("[EventBus]");

            // 3. Camera
            EnsureCamera(existingCamera);

            // 4. Angel
            CreatePlayer();

            // 5. Baby
            CreateBaby();

            // 6. Dungeon
            var dungeonGo = CreateIfMissing<Dungeon.BSPGenerator>("[DungeonGenerator]").gameObject;
            dungeonGo.AddComponent<Dungeon.CorridorGenerator>();

            // 7. EnemySpawner
            CreateIfMissing<Enemies.EnemySpawner>("[EnemySpawner]");

            // 8. HUD
            CreateHUD();

            // 9. Audio
            CreateIfMissing<Audio.AudioManager>("[AudioManager]");

            // 10. Debug Visualizer
            CreateIfMissing<DebugVisualizer>("[DebugVisualizer]");

            Debug.Log("  [10/10] 全部系统就绪 ✓");
            Debug.Log("═══════════════════════════════════════");
        }

        #region ─── Helpers ───────────────────────────────

        private static T CreateIfMissing<T>(string name) where T : Component
        {
            var existing = Object.FindObjectOfType<T>();
            if (existing != null) return existing;
            var go = new GameObject(name);
            return go.AddComponent<T>();
        }

        private static void EnsureCamera(Camera existing)
        {
            if (existing != null) return;
            var cam = Camera.main;
            if (cam != null)
            {
                // 确保相机设置正确
                cam.orthographic = true;
                cam.orthographicSize = 1800f; // 足够看到 3000x3000 地图
                cam.transform.position = new Vector3(0, 0, -10);
                cam.backgroundColor = new Color(0.06f, 0.05f, 0.10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                return;
            }

            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();

            cam.orthographic = true;
            cam.orthographicSize = 1800f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private static void CreatePlayer()
        {
            if (Object.FindObjectOfType<Player.AngelController>() != null) return;

            var go = new GameObject("Angel");
            go.tag = "Player";
            go.transform.position = Vector3.zero;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.drag = 8f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            go.AddComponent<Player.AngelAttributes>();
            go.AddComponent<Player.AngelController>();
            go.AddComponent<Player.AngelCombat>();
            go.AddComponent<Player.BabyInteraction>();
        }

        private static void CreateBaby()
        {
            if (Object.FindObjectOfType<Baby.BabyController>() != null) return;

            var go = new GameObject("Baby");
            go.tag = "Baby";
            go.transform.position = new Vector3(40, 0, 0);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.drag = 6f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            go.AddComponent<Baby.BabyAttributes>();
            go.AddComponent<Baby.BabyController>();
            go.AddComponent<Baby.BabyAI>();
            go.AddComponent<Baby.EmotionStateMachine>();
        }

        private static void CreateHUD()
        {
            if (Object.FindObjectOfType<UI.HUDController>() != null) return;

            var canvasGo = new GameObject("HUD_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            canvasGo.AddComponent<UI.HUDController>();
        }

        #endregion
    }
}
