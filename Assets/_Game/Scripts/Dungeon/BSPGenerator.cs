using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AngelGuardian.Dungeon
{
    /// <summary>
    /// BSP地形生成器 —— 游戏核心差异化系统
    /// 基于BSP分割 + Delaunay三角剖分 + Kruskal MST + 随机回填环路
    /// 生成自然有机的地下城地形，支持口袋房间、安全区和门放置
    /// </summary>
    public class BSPGenerator : MonoBehaviour
    {
        #region Configuration

        [Header("Map Settings")]
        [SerializeField, Range(512, 4096)]
        private int mapSize = 2048;

        [SerializeField, Range(4, 8)]
        private int maxSplitDepth = 6;

        [SerializeField, Range(8, 14)]
        private int targetRoomCount = 10;

        [SerializeField, Range(0.10f, 0.25f)]
        private float splitVariation = 0.15f;       // 分割偏移 ±15%

        [Header("Room Settings")]
        [SerializeField, Range(100, 400)]
        private int minRoomSize = 160;

        [SerializeField, Range(150, 600)]
        private int maxRoomSize = 480;

        [SerializeField, Range(2000, 10000)]
        private int minRoomArea = 4000;

        [Header("Corridor Settings")]
        [SerializeField, Range(80, 200)]
        private int corridorMinWidth = 120;

        [SerializeField, Range(100, 240)]
        private int corridorMaxWidth = 180;

        [Header("Door Settings")]
        [SerializeField, Range(0.2f, 0.6f)]
        private float doorPlacementChance = 0.3f;

        [Header("Safe Zone")]
        [SerializeField, Range(150, 500)]
        private float safeZoneRadius = 300f;

        [Header("MST & Loop")]
        [SerializeField, Range(0.2f, 0.6f)]
        private float loopBackfillRatio = 0.4f;      // 30-50% 非MST边回填

        [Header("Debug")]
        [SerializeField]
        private bool drawDebugGizmos = true;

        [SerializeField]
        private bool verboseLogging = false;

        #endregion

        #region Result Data

        [HideInInspector]
        public List<Room> rooms;

        [HideInInspector]
        public List<(int roomA, int roomB, List<Vector2Int> path)> corridors;

        [HideInInspector]
        public List<DoorData> doors;

        [HideInInspector]
        public int safeRoomIndex = -1;

        [HideInInspector]
        public bool generationComplete = false;

        #endregion

        #region BSP Node

        /// <summary>
        /// BSP树节点 —— 表示一次分割产生的区域
        /// </summary>
        [System.Serializable]
        private class BSPNode
        {
            public int x, y, w, h;
            public BSPNode left;
            public BSPNode right;
            public bool isLeaf => left == null && right == null;
            public int roomIndex = -1;          // 关联的房间索引(仅叶节点有效)
            public int depth;

            public BSPNode(int x, int y, int w, int h, int depth = 0)
            {
                this.x = x;
                this.y = y;
                this.w = w;
                this.h = h;
                this.depth = depth;
            }

            public Vector2Int Center => new Vector2Int(x + w / 2, y + h / 2);
            public int Area => w * h;

            /// <summary>
            /// 随机在节点区域内生成一个房间
            /// </summary>
            public Room GenerateRoom(int minSize, int maxSize, int padding = 10)
            {
                int roomW = Random.Range(minSize, Mathf.Min(maxSize, w - padding * 2));
                int roomH = Random.Range(minSize, Mathf.Min(maxSize, h - padding * 2));

                roomW = Mathf.Clamp(roomW, minSize, w - padding * 2);
                roomH = Mathf.Clamp(roomH, minSize, h - padding * 2);

                int roomX = x + padding + Random.Range(0, Mathf.Max(1, w - roomW - padding * 2));
                int roomY = y + padding + Random.Range(0, Mathf.Max(1, h - roomH - padding * 2));

                return new Room(roomX, roomY, roomW, roomH);
            }
        }

        #endregion

        #region Edge (for MST)

        private struct Edge : IComparable<Edge>
        {
            public int a;
            public int b;
            public float weight;

            public Edge(int a, int b, float weight)
            {
                this.a = a;
                this.b = b;
                this.weight = weight;
            }

            public int CompareTo(Edge other) => weight.CompareTo(other.weight);
        }

        #endregion

        #region Door Data

        [System.Serializable]
        public class DoorData
        {
            public Vector2Int position;
            public int roomA;
            public int roomB;
            public bool isHorizontal;       // 门朝向(水平/垂直)

            public DoorData(Vector2Int pos, int a, int b, bool horizontal)
            {
                position = pos;
                roomA = a;
                roomB = b;
                isHorizontal = horizontal;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 主入口：生成完整地下城地图
        /// </summary>
        public void GenerateMap(int overrideMapSize = -1, GameConfig overrideConfig = null)
        {
            if (overrideMapSize > 0)
                mapSize = overrideMapSize;

            if (overrideConfig != null)
                ApplyGameConfig(overrideConfig);

            ClearResults();

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] ===== 开始生成地图 ({mapSize}x{mapSize}) =====");

            // ========== 步骤1: BSP分割 ==========
            BSPNode root = new BSPNode(0, 0, mapSize, mapSize);
            SplitBSP(root, 0, maxSplitDepth);

            // 收集所有叶节点
            List<BSPNode> leafNodes = new List<BSPNode>();
            CollectLeafNodes(root, leafNodes);

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] BSP分割完成: {leafNodes.Count} 个叶节点");

            // ========== 步骤2: 在叶节点中生成房间 ==========
            rooms = new List<Room>();
            for (int i = 0; i < leafNodes.Count; i++)
            {
                Room room = leafNodes[i].GenerateRoom(minRoomSize, maxRoomSize);
                room.RecalculateCenter();
                rooms.Add(room);
                leafNodes[i].roomIndex = i;
            }

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] 初始房间生成: {rooms.Count} 个");

            // ========== 步骤3: 过滤面积过小的房间 ==========
            FilterSmallRooms();

            if (rooms.Count < 8)
            {
                Debug.LogWarning($"[BSPGenerator] 房间数量不足 ({rooms.Count})，重新生成...");
                GenerateMap(overrideMapSize, overrideConfig);
                return;
            }

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] 过滤后房间: {rooms.Count} 个 (目标: {targetRoomCount})");

            // ========== 步骤4: Delaunay三角剖分(简化版) ==========
            List<Edge> allEdges = SimplifiedDelaunayTriangulation();

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] Delaunay三角剖分完成: {allEdges.Count} 条边");

            // ========== 步骤5: Kruskal MST 最小生成树 ==========
            List<Edge> mstEdges = KruskalMST(allEdges);

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] MST生成: {mstEdges.Count} 条边");

            // ========== 步骤6: 随机回填非MST边形成环路 ==========
            List<Edge> loopEdges = SelectLoopEdges(allEdges, mstEdges);

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] 环路回填: {loopEdges.Count} 条边");

            // 合并MST + 环路 = 最终走廊边
            List<Edge> finalEdges = new List<Edge>(mstEdges);
            finalEdges.AddRange(loopEdges);

            // ========== 步骤7: 建立房间连接关系 ==========
            foreach (var edge in finalEdges)
            {
                rooms[edge.a].ConnectTo(edge.b);
                rooms[edge.b].ConnectTo(edge.a);
            }

            // ========== 步骤8: 生成L形走廊 ==========
            corridors = new List<(int, int, List<Vector2Int>)>();
            CorridorGenerator corridorGen = GetComponent<CorridorGenerator>();
            if (corridorGen == null)
                corridorGen = gameObject.AddComponent<CorridorGenerator>();

            corridorGen.Initialize(corridorMinWidth, corridorMaxWidth);

            foreach (var edge in finalEdges)
            {
                var path = corridorGen.GenerateCorridorPath(rooms[edge.a], rooms[edge.b]);
                corridors.Add((edge.a, edge.b, path));
            }

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] 走廊生成: {corridors.Count} 条");

            // ========== 步骤9: 门放置 ==========
            doors = new List<DoorData>();
            PlaceDoors(finalEdges);

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] 门放置: {doors.Count} 个");

            // ========== 步骤10: 安全初始区 ==========
            AssignSafeRoom();

            if (verboseLogging)
                Debug.Log($"[BSPGenerator] 安全区: 房间 #{safeRoomIndex}");

            // ========== 步骤11: 质量验证 ==========
            bool validationPassed = ValidateMap();
            if (!validationPassed)
            {
                Debug.LogWarning("[BSPGenerator] 质量验证失败，重新生成...");
                GenerateMap(overrideMapSize, overrideConfig);
                return;
            }

            // ========== 步骤12: 口袋房间检测 ==========
            DetectPocketRooms();

            generationComplete = true;

            Debug.Log($"[BSPGenerator] ===== 地图生成完成: {rooms.Count} 房间, " +
                      $"{corridors.Count} 走廊, {doors.Count} 门 =====");
        }

        #endregion

        #region BSP Splitting

        /// <summary>
        /// 递归BSP分割 —— 交替水平/垂直分割，带随机偏移
        /// </summary>
        private void SplitBSP(BSPNode node, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;
            if (node.w < minRoomSize * 2 + 40 && node.h < minRoomSize * 2 + 40) return;

            // 决定分割方向：房间更宽则垂直分割，更高则水平分割，否则随机
            bool splitVertical;
            if (node.w > node.h * 1.25f)
                splitVertical = true;
            else if (node.h > node.w * 1.25f)
                splitVertical = false;
            else
                splitVertical = Random.value > 0.5f;

            int maxSplit = (splitVertical ? node.w : node.h) - minRoomSize;
            if (maxSplit < minRoomSize) return;

            // 中点位置 + 随机偏移 ±15%
            float midPoint = splitVertical ? node.w / 2f : node.h / 2f;
            float variation = midPoint * splitVariation;
            float offset = Random.Range(-variation, variation);
            int splitPos = Mathf.RoundToInt(midPoint + offset);

            // 确保分割后两个区域都足够大
            splitPos = Mathf.Clamp(splitPos, minRoomSize, maxSplit);

            if (splitVertical)
            {
                // 垂直分割 → 左右子节点
                node.left = new BSPNode(node.x, node.y, splitPos, node.h, depth + 1);
                node.right = new BSPNode(node.x + splitPos, node.y, node.w - splitPos, node.h, depth + 1);
            }
            else
            {
                // 水平分割 → 上下子节点
                node.left = new BSPNode(node.x, node.y, splitPos, node.w, depth + 1);
                node.right = new BSPNode(node.x, node.y + splitPos, node.w, node.h - splitPos, depth + 1);
            }

            SplitBSP(node.left, depth + 1, maxDepth);
            SplitBSP(node.right, depth + 1, maxDepth);
        }

        /// <summary>
        /// 收集所有BSP树叶节点
        /// </summary>
        private void CollectLeafNodes(BSPNode node, List<BSPNode> leaves)
        {
            if (node == null) return;
            if (node.isLeaf)
                leaves.Add(node);
            else
            {
                CollectLeafNodes(node.left, leaves);
                CollectLeafNodes(node.right, leaves);
            }
        }

        #endregion

        #region Room Filtering

        /// <summary>
        /// 过滤面积过小的房间，确保至少8个
        /// </summary>
        private void FilterSmallRooms()
        {
            rooms = rooms.Where(r => r.area >= minRoomArea).ToList();

            // 如果过滤后不足8个，保留最大的8个
            if (rooms.Count < 8)
            {
                rooms = rooms.OrderByDescending(r => r.area).ToList();
            }

            // 如果超过目标数量，随机裁剪
            while (rooms.Count > targetRoomCount + 2)
            {
                int removeIndex = Random.Range(0, rooms.Count);
                rooms.RemoveAt(removeIndex);
            }

            // 重新分配索引
            for (int i = 0; i < rooms.Count; i++)
            {
                rooms[i].RecalculateCenter();
            }
        }

        #endregion

        #region Delaunay Triangulation (Simplified)

        /// <summary>
        /// 简化版Delaunay三角剖分 —— 对所有房间中心做边连接
        /// 使用Bowyer-Watson算法的近似实现
        /// </summary>
        private List<Edge> SimplifiedDelaunayTriangulation()
        {
            List<Edge> edges = new List<Edge>();
            int n = rooms.Count;
            if (n < 3) return edges;

            // 创建超级三角形包围所有点
            Vector2Int[] points = new Vector2Int[n];
            for (int i = 0; i < n; i++)
                points[i] = rooms[i].center;

            // 简化方法：对所有点对计算距离，选择最近的k条边
            // 使用基于距离阈值的邻接判定
            List<(int a, int b, float dist)> allPairs = new List<(int, int, float)>();

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    float dist = Vector2Int.Distance(points[i], points[j]);
                    allPairs.Add((i, j, dist));
                }
            }

            // 按距离排序
            allPairs.Sort((x, y) => x.dist.CompareTo(y.dist));

            // 对每个点，连接最近的3-5个邻居（模拟Delaunay）
            Dictionary<int, int> connectionsPerRoom = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
                connectionsPerRoom[i] = 0;

            int maxConnectionsPerRoom = Mathf.Min(5, n - 1);
            HashSet<(int, int)> addedEdges = new HashSet<(int, int)>();

            foreach (var pair in allPairs)
            {
                if (connectionsPerRoom[pair.a] >= maxConnectionsPerRoom &&
                    connectionsPerRoom[pair.b] >= maxConnectionsPerRoom)
                    continue;

                var key = pair.a < pair.b ? (pair.a, pair.b) : (pair.b, pair.a);
                if (addedEdges.Contains(key)) continue;

                // 排除过远的连接
                if (pair.dist > mapSize * 0.65f) continue;

                edges.Add(new Edge(pair.a, pair.b, pair.dist));
                addedEdges.Add(key);
                connectionsPerRoom[pair.a]++;
                connectionsPerRoom[pair.b]++;
            }

            // 确保每个房间至少连接1个其他房间
            for (int i = 0; i < n; i++)
            {
                if (connectionsPerRoom[i] == 0 && allPairs.Count > 0)
                {
                    // 找到最近的邻居
                    var nearest = allPairs.FirstOrDefault(p => p.a == i || p.b == i);
                    int other = nearest.a == i ? nearest.b : nearest.a;
                    var key = i < other ? (i, other) : (other, i);

                    if (!addedEdges.Contains(key))
                    {
                        edges.Add(new Edge(i, other, nearest.dist));
                        addedEdges.Add(key);
                        connectionsPerRoom[i]++;
                        connectionsPerRoom[other]++;
                    }
                }
            }

            return edges;
        }

        #endregion

        #region Kruskal MST

        /// <summary>
        /// Kruskal最小生成树 —— 保证所有房间连通
        /// </summary>
        private List<Edge> KruskalMST(List<Edge> allEdges)
        {
            List<Edge> mst = new List<Edge>();
            int n = rooms.Count;

            // 按权重排序
            List<Edge> sortedEdges = new List<Edge>(allEdges);
            sortedEdges.Sort();

            // Union-Find
            int[] parent = new int[n];
            for (int i = 0; i < n; i++)
                parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int x, int y)
            {
                int rx = Find(x), ry = Find(y);
                if (rx != ry) parent[rx] = ry;
            }

            foreach (var edge in sortedEdges)
            {
                if (Find(edge.a) != Find(edge.b))
                {
                    mst.Add(edge);
                    Union(edge.a, edge.b);

                    if (mst.Count == n - 1) break;
                }
            }

            return mst;
        }

        #endregion

        #region Loop Backfill

        /// <summary>
        /// 随机回填30-50%非MST边形成环路
        /// </summary>
        private List<Edge> SelectLoopEdges(List<Edge> allEdges, List<Edge> mstEdges)
        {
            HashSet<(int, int)> mstSet = new HashSet<(int, int)>();
            foreach (var e in mstEdges)
            {
                var key = e.a < e.b ? (e.a, e.b) : (e.b, e.a);
                mstSet.Add(key);
            }

            // 收集非MST边
            List<Edge> nonMstEdges = new List<Edge>();
            foreach (var e in allEdges)
            {
                var key = e.a < e.b ? (e.a, e.b) : (e.b, e.a);
                if (!mstSet.Contains(key))
                    nonMstEdges.Add(e);
            }

            // 按权重排序(优先选择较短的边)
            nonMstEdges.Sort();

            // 随机回填30-50%
            int loopCount = Mathf.RoundToInt(nonMstEdges.Count * loopBackfillRatio);
            loopCount = Mathf.Clamp(loopCount, 1, nonMstEdges.Count);

            List<Edge> selected = new List<Edge>();
            // 从非MST边中随机选择
            List<Edge> shuffled = new List<Edge>(nonMstEdges);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            for (int i = 0; i < loopCount && i < shuffled.Count; i++)
            {
                selected.Add(shuffled[i]);
            }

            return selected;
        }

        #endregion

        #region Door Placement

        /// <summary>
        /// 门放置：≥30%走廊连接处放置门
        /// </summary>
        private void PlaceDoors(List<Edge> finalEdges)
        {
            foreach (var edge in finalEdges)
            {
                if (Random.value < doorPlacementChance)
                {
                    Room roomA = rooms[edge.a];
                    Room roomB = rooms[edge.b];

                    // 计算两个房间之间的门位置
                    Vector2Int doorPos = CalculateDoorPosition(roomA, roomB);
                    bool isHorizontal = DetermineDoorOrientation(roomA, roomB);

                    doors.Add(new DoorData(doorPos, edge.a, edge.b, isHorizontal));
                }
            }

            // 确保至少30%的走廊连接处有门
            int minDoors = Mathf.CeilToInt(finalEdges.Count * doorPlacementChance);
            while (doors.Count < minDoors)
            {
                // 随机选择一条没有门的边
                var candidates = finalEdges
                    .Where(e => !doors.Any(d =>
                        (d.roomA == e.a && d.roomB == e.b) ||
                        (d.roomA == e.b && d.roomB == e.a)))
                    .ToList();

                if (candidates.Count == 0) break;

                var edge = candidates[Random.Range(0, candidates.Count)];
                Room roomA = rooms[edge.a];
                Room roomB = rooms[edge.b];

                Vector2Int doorPos = CalculateDoorPosition(roomA, roomB);
                bool isHorizontal = DetermineDoorOrientation(roomA, roomB);
                doors.Add(new DoorData(doorPos, edge.a, edge.b, isHorizontal));
            }
        }

        private Vector2Int CalculateDoorPosition(Room a, Room b)
        {
            // 在房间A到房间B的方向上，取A边缘的位置
            Vector2Int dir = b.center - a.center;

            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                // 水平方向为主
                int doorX = dir.x > 0 ? a.Right : a.Left;
                int doorY = Mathf.Clamp(a.center.y, b.Bottom + 20, b.Top - 20);
                return new Vector2Int(doorX, doorY);
            }
            else
            {
                // 垂直方向为主
                int doorY = dir.y > 0 ? a.Top : a.Bottom;
                int doorX = Mathf.Clamp(a.center.x, b.Left + 20, b.Right - 20);
                return new Vector2Int(doorX, doorY);
            }
        }

        private bool DetermineDoorOrientation(Room a, Room b)
        {
            Vector2Int dir = b.center - a.center;
            return Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
        }

        #endregion

        #region Safe Zone

        /// <summary>
        /// 安全初始区 —— 出生点300px内无敌人生成
        /// </summary>
        private void AssignSafeRoom()
        {
            // 选择离地图中心最近的房间作为安全区
            Vector2Int mapCenter = new Vector2Int(mapSize / 2, mapSize / 2);

            float closestDist = float.MaxValue;
            int closestIndex = -1;

            for (int i = 0; i < rooms.Count; i++)
            {
                float dist = Vector2Int.Distance(rooms[i].center, mapCenter);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestIndex = i;
                }
            }

            if (closestIndex >= 0)
            {
                safeRoomIndex = closestIndex;
                rooms[closestIndex].isSafeRoom = true;

                // 标记安全区范围内的其他房间
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (i == closestIndex) continue;
                    float dist = Vector2Int.Distance(rooms[i].center, rooms[closestIndex].center);
                    if (dist <= safeZoneRadius)
                    {
                        rooms[i].isSafeRoom = true;
                    }
                }
            }
        }

        #endregion

        #region Quality Validation

        /// <summary>
        /// 质量验证: BFS连通性检测、DFS环路检测、口袋房间检测
        /// </summary>
        private bool ValidateMap()
        {
            if (rooms.Count < 8)
            {
                Debug.LogWarning("[BSPGenerator] 验证失败: 房间不足8个");
                return false;
            }

            // BFS连通性检测 —— 所有房间必须连通
            bool[] visited = new bool[rooms.Count];
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(0);
            visited[0] = true;
            int visitedCount = 1;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbor in rooms[current].connectedRooms)
                {
                    if (!visited[neighbor])
                    {
                        visited[neighbor] = true;
                        visitedCount++;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (visitedCount != rooms.Count)
            {
                Debug.LogWarning($"[BSPGenerator] 验证失败: BFS连通性 — {visitedCount}/{rooms.Count} 连通");
                return false;
            }

            // DFS环路检测 —— 应该存在至少一个环路
            bool hasCycle = DetectCycle();
            if (!hasCycle && rooms.Count >= 4)
            {
                Debug.LogWarning("[BSPGenerator] 验证警告: 无环路检测到(地图可能是纯树结构)");
                // 不阻止生成，但记录警告
            }

            return true;
        }

        /// <summary>
        /// DFS环路检测
        /// </summary>
        private bool DetectCycle()
        {
            bool[] visited = new bool[rooms.Count];
            for (int i = 0; i < rooms.Count; i++)
            {
                if (!visited[i])
                {
                    if (DFSCycleCheck(i, -1, visited))
                        return true;
                }
            }
            return false;
        }

        private bool DFSCycleCheck(int node, int parent, bool[] visited)
        {
            visited[node] = true;

            foreach (int neighbor in rooms[node].connectedRooms)
            {
                if (!visited[neighbor])
                {
                    if (DFSCycleCheck(neighbor, node, visited))
                        return true;
                }
                else if (neighbor != parent)
                {
                    return true; // 找到环路
                }
            }
            return false;
        }

        /// <summary>
        /// 口袋房间检测 —— 只有1个连接
        /// </summary>
        private void DetectPocketRooms()
        {
            foreach (var room in rooms)
            {
                room.UpdatePocketRoomFlag();
            }

            int pocketCount = rooms.Count(r => r.isPocketRoom);
            if (verboseLogging)
                Debug.Log($"[BSPGenerator] 口袋房间检测: {pocketCount} 个口袋房间");
        }

        #endregion

        #region Utility

        private void ApplyGameConfig(GameConfig config)
        {
            // 从GameConfig读取参数(如果存在)
            if (config.MapSize > 0) mapSize = config.MapSize;
            if (config.maxSplitDepth > 0) maxSplitDepth = config.maxSplitDepth;
            if (config.targetRoomCount > 0) targetRoomCount = config.targetRoomCount;
            if (config.minRoomSize > 0) minRoomSize = config.minRoomSize;
            if (config.corridorWidth > 0)
            {
                corridorMinWidth = config.corridorWidth;
                corridorMaxWidth = Mathf.RoundToInt(config.corridorWidth * 1.5f);
            }
            if (config.doorFrequency > 0) doorPlacementChance = config.doorFrequency;
            if (config.safeZoneRadius > 0) safeZoneRadius = config.safeZoneRadius;
        }

        private void ClearResults()
        {
            rooms = null;
            corridors = null;
            doors = null;
            safeRoomIndex = -1;
            generationComplete = false;
        }

        public Vector2Int GetSafeRoomCenter()
        {
            if (safeRoomIndex >= 0 && safeRoomIndex < rooms.Count)
                return rooms[safeRoomIndex].center;
            return new Vector2Int(mapSize / 2, mapSize / 2);
        }

        public Room GetSafeRoom()
        {
            if (safeRoomIndex >= 0 && safeRoomIndex < rooms.Count)
                return rooms[safeRoomIndex];
            return null;
        }

        #endregion

        #region Debug Visualization

        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos || rooms == null) return;

            // 绘制房间
            foreach (var room in rooms)
            {
                if (room.isSafeRoom)
                    Gizmos.color = new Color(0, 1, 0, 0.3f);
                else if (room.isPocketRoom)
                    Gizmos.color = new Color(1, 0.6f, 0, 0.3f);
                else
                    Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.2f);

                Vector3 center = new Vector3(room.center.x, room.center.y, 0);
                Vector3 size = new Vector3(room.w, room.h, 0);
                Gizmos.DrawCube(center, size);

                // 房间边框
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(center, size);
            }

            // 绘制走廊
            if (corridors != null)
            {
                Gizmos.color = new Color(0.8f, 0.8f, 0.2f, 0.5f);
                foreach (var (_, _, path) in corridors)
                {
                    if (path == null) continue;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Vector3 a = new Vector3(path[i].x, path[i].y, 0);
                        Vector3 b = new Vector3(path[i + 1].x, path[i + 1].y, 0);
                        Gizmos.DrawLine(a, b);
                    }
                }
            }

            // 绘制门
            if (doors != null)
            {
                Gizmos.color = Color.red;
                foreach (var door in doors)
                {
                    Vector3 pos = new Vector3(door.position.x, door.position.y, 0);
                    Gizmos.DrawSphere(pos, 8f);
                }
            }

            // 绘制安全区半径
            if (safeRoomIndex >= 0 && safeRoomIndex < rooms.Count)
            {
                Gizmos.color = new Color(0, 1, 0, 0.15f);
                Vector3 safeCenter = new Vector3(rooms[safeRoomIndex].center.x, rooms[safeRoomIndex].center.y, 0);
                Gizmos.DrawWireSphere(safeCenter, safeZoneRadius);
            }
        }

        #endregion
    }

    /// <summary>
    /// 游戏配置 —— 用于从外部传入BSP生成参数
    /// </summary>
    [System.Serializable]
    public class GameConfig
    {
        public int mapSize = 2048;
        public int maxSplitDepth = 6;
        public int targetRoomCount = 10;
        public int minRoomSize = 160;
        public int corridorWidth = 150;
        public float doorFrequency = 0.3f;
        public float safeZoneRadius = 300f;
    }
}
