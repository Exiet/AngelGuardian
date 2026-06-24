using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// 调试可视化 —— 给所有关键GameObject添加临时可视化
    /// 用于无美术资源时的快速验证
    /// </summary>
    public class DebugVisualizer : MonoBehaviour
    {
        [Header("Debug Colors")]
        [SerializeField] private Color _angelColor = Color.cyan;
        [SerializeField] private Color _babyColor = Color.yellow;
        [SerializeField] private Color _enemyColor = Color.red;
        [SerializeField] private Color _roomColor = new Color(0.2f, 0.5f, 1f, 0.3f);
        [SerializeField] private Color _corridorColor = new Color(0.8f, 0.8f, 0.2f, 0.5f);

        private void Awake()
        {
            // 延迟一帧执行，等 AutoBootstrap 创建完所有对象
            Invoke(nameof(VisualizeAll), 0.5f);
        }

        private void VisualizeAll()
        {
            Debug.Log("[DebugVisualizer] 开始可视化...");

            // 1. 给 Angel 添加 Sprite
            var angel = GameObject.FindGameObjectWithTag("Angel");
            if (angel != null)
            {
                AddCircleSprite(angel, _angelColor, 20f, "Angel");
                Debug.Log("  ✓ Angel 可视化");
            }

            // 2. 给 Baby 添加 Sprite
            var baby = GameObject.FindGameObjectWithTag("Baby");
            if (baby != null)
            {
                AddCircleSprite(baby, _babyColor, 15f, "Baby");
                Debug.Log("  ✓ Baby 可视化");
            }

            // 3. 生成地图可视化
            var bspGen = FindObjectOfType<Dungeon.BSPGenerator>();
            if (bspGen != null && bspGen.generationComplete)
            {
                VisualizeDungeon(bspGen);
                Debug.Log("  ✓ 地图可视化");
            }
            else
            {
                Debug.Log("  ! 地图尚未生成，延迟可视化...");
                Invoke(nameof(TryVisualizeDungeon), 2f);
            }

            Debug.Log("[DebugVisualizer] 可视化完成");
        }

        private void TryVisualizeDungeon()
        {
            var bspGen = FindObjectOfType<Dungeon.BSPGenerator>();
            if (bspGen != null && bspGen.generationComplete)
            {
                VisualizeDungeon(bspGen);
            }
        }

        private void AddCircleSprite(GameObject go, Color color, float size, string label)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            
            sr.sprite = CreateCircleSprite(color);
            sr.sortingOrder = 100;
            sr.drawMode = SpriteDrawMode.Sliced;
            
            go.transform.localScale = new Vector3(size, size, 1);
        }

        private Sprite CreateCircleSprite(Color color)
        {
            int size = 64;
            var tex = new Texture2D(size, size);
            var pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        pixels[y * size + x] = color;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return sprite;
        }

        private void VisualizeDungeon(Dungeon.BSPGenerator bspGen)
        {
            // 创建地图父对象
            var mapGo = new GameObject("[DebugMap]");

            // 绘制房间
            if (bspGen.rooms != null)
            {
                for (int i = 0; i < bspGen.rooms.Count; i++)
                {
                    var room = bspGen.rooms[i];
                    var roomGo = new GameObject($"Room_{i}");
                    roomGo.transform.SetParent(mapGo.transform);
                    roomGo.transform.position = new Vector3(room.center.x, room.center.y, 0);

                    var sr = roomGo.AddComponent<SpriteRenderer>();
                    sr.sprite = CreateRectSprite(_roomColor, room.w, room.h);
                    sr.sortingLayerName = "Ground";
                    sr.sortingOrder = -10;
                }
            }

            // 绘制走廊
            if (bspGen.corridors != null)
            {
                int corridorIndex = 0;
                foreach (var (_, _, path) in bspGen.corridors)
                {
                    if (path == null || path.Count < 2) continue;

                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        var start = path[i];
                        var end = path[i + 1];

                        var corridorGo = new GameObject($"Corridor_{corridorIndex++}");
                        corridorGo.transform.SetParent(mapGo.transform);

                        Vector2 mid = (Vector2)(start + end) * 0.5f;
                        corridorGo.transform.position = new Vector3(mid.x, mid.y, 0);

                        float width = Mathf.Abs(end.x - start.x) + 20;
                        float height = Mathf.Abs(end.y - start.y) + 20;

                        var sr = corridorGo.AddComponent<SpriteRenderer>();
                        sr.sprite = CreateRectSprite(_corridorColor, (int)width, (int)height);
                        sr.sortingLayerName = "Ground";
                        sr.sortingOrder = -5;
                    }
                }
            }
        }

        private Sprite CreateRectSprite(Color color, int w, int h)
        {
            // 限制最大尺寸
            w = Mathf.Min(w, 512);
            h = Mathf.Min(h, 512);

            var tex = new Texture2D(w, h);
            var pixels = new Color[w * h];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            tex.SetPixels(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            return sprite;
        }
    }
}
