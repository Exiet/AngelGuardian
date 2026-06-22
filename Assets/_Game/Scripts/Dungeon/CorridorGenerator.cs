using System.Collections.Generic;
using UnityEngine;

namespace AngelGuardian.Dungeon
{
    /// <summary>
    /// 走廊生成器 —— 生成L形走廊连接两个房间
    /// 支持随机水平/垂直优先、可变宽度、障碍物清除
    /// </summary>
    public class CorridorGenerator : MonoBehaviour
    {
        #region Configuration

        [Header("Corridor Dimensions")]
        [SerializeField, Range(80, 200)]
        private int minWidth = 120;

        [SerializeField, Range(100, 240)]
        private int maxWidth = 180;

        [Header("Door Placement")]
        [SerializeField, Range(0.1f, 0.5f)]
        private float doorAtEndsChance = 0.3f;

        [Header("Path Smoothing")]
        [SerializeField, Range(0, 10)]
        private int pathSmoothingIterations = 2;

        #endregion

        #region Internal State

        private int currentCorridorWidth;
        private List<RectInt> corridorBounds;   // 所有走廊的边界(用于障碍物清除)
        private List<Vector2Int> obstacleClearCache;

        #endregion

        #region Public API

        /// <summary>
        /// 初始化走廊生成器参数
        /// </summary>
        public void Initialize(int minW, int maxW)
        {
            minWidth = minW;
            maxWidth = maxW;
            corridorBounds = new List<RectInt>();
            obstacleClearCache = new List<Vector2Int>();
        }

        /// <summary>
        /// 在两个房间之间生成L形走廊路径
        /// 随机选择：先水平后垂直，或先垂直后水平
        /// </summary>
        /// <param name="roomA">房间A</param>
        /// <param name="roomB">房间B</param>
        /// <returns>走廊路径上的所有像素坐标</returns>
        public List<Vector2Int> GenerateCorridorPath(Room roomA, Room roomB)
        {
            currentCorridorWidth = Random.Range(minWidth, maxWidth + 1);
            List<Vector2Int> path = new List<Vector2Int>();

            Vector2Int start = roomA.center;
            Vector2Int end = roomB.center;

            // 随机决定走廊形状：先水平后垂直(HV) 或 先垂直后水平(VH)
            bool horizontalFirst = Random.value > 0.5f;

            if (horizontalFirst)
            {
                GenerateHVCorridor(start, end, path);
            }
            else
            {
                GenerateVHCorridor(start, end, path);
            }

            // 路径平滑
            for (int i = 0; i < pathSmoothingIterations; i++)
            {
                path = SmoothPath(path);
            }

            // 清除走廊区域的障碍物
            ClearObstaclesInCorridor(path);

            return path;
        }

        /// <summary>
        /// 获取所有走廊占用的边界矩形
        /// </summary>
        public List<RectInt> GetCorridorBounds()
        {
            return corridorBounds;
        }

        /// <summary>
        /// 检查一个点是否在走廊区域内
        /// </summary>
        public bool IsPointInCorridor(Vector2Int point)
        {
            foreach (var bounds in corridorBounds)
            {
                if (bounds.Contains(point))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 清除所有走廊数据
        /// </summary>
        public void Clear()
        {
            corridorBounds?.Clear();
            obstacleClearCache?.Clear();
        }

        #endregion

        #region Corridor Path Generation

        /// <summary>
        /// 先水平后垂直的L形走廊
        /// </summary>
        private void GenerateHVCorridor(Vector2Int start, Vector2Int end, List<Vector2Int> path)
        {
            Vector2Int corner = new Vector2Int(end.x, start.y);

            // 从起点水平移动到拐角
            AddHorizontalSegment(path, start.x, corner.x, start.y);
            // 从拐角垂直移动到终点
            AddVerticalSegment(path, start.y, end.y, corner.x);

            // 记录走廊边界
            RecordCorridorBoundsHV(start, corner, end);
        }

        /// <summary>
        /// 先垂直后水平的L形走廊
        /// </summary>
        private void GenerateVHCorridor(Vector2Int start, Vector2Int end, List<Vector2Int> path)
        {
            Vector2Int corner = new Vector2Int(start.x, end.y);

            // 从起点垂直移动到拐角
            AddVerticalSegment(path, start.y, end.y, start.x);
            // 从拐角水平移动到终点
            AddHorizontalSegment(path, start.x, end.x, corner.y);

            // 记录走廊边界
            RecordCorridorBoundsVH(start, corner, end);
        }

        /// <summary>
        /// 添加水平线段到路径
        /// </summary>
        private void AddHorizontalSegment(List<Vector2Int> path, int xStart, int xEnd, int y)
        {
            int step = xStart <= xEnd ? 1 : -1;
            int halfW = currentCorridorWidth / 2;

            for (int x = xStart; x != xEnd + step; x += step)
            {
                // 添加走廊宽度的所有像素
                for (int wy = -halfW; wy <= halfW; wy++)
                {
                    path.Add(new Vector2Int(x, y + wy));
                }
            }
        }

        /// <summary>
        /// 添加垂直线段到路径
        /// </summary>
        private void AddVerticalSegment(List<Vector2Int> path, int yStart, int yEnd, int x)
        {
            int step = yStart <= yEnd ? 1 : -1;
            int halfW = currentCorridorWidth / 2;

            for (int y = yStart; y != yEnd + step; y += step)
            {
                // 添加走廊宽度的所有像素
                for (int wx = -halfW; wx <= halfW; wx++)
                {
                    path.Add(new Vector2Int(x + wx, y));
                }
            }
        }

        #endregion

        #region Corridor Bounds Recording

        private void RecordCorridorBoundsHV(Vector2Int start, Vector2Int corner, Vector2Int end)
        {
            int halfW = currentCorridorWidth / 2;

            // 水平段
            int hxMin = Mathf.Min(start.x, corner.x) - halfW;
            int hxMax = Mathf.Max(start.x, corner.x) + halfW;
            int hyMin = start.y - halfW;
            int hyMax = start.y + halfW;
            corridorBounds.Add(new RectInt(hxMin, hyMin, hxMax - hxMin, hyMax - hyMin));

            // 垂直段
            int vyMin = Mathf.Min(corner.y, end.y) - halfW;
            int vyMax = Mathf.Max(corner.y, end.y) + halfW;
            int vxMin = corner.x - halfW;
            int vxMax = corner.x + halfW;
            corridorBounds.Add(new RectInt(vxMin, vyMin, vxMax - vxMin, vyMax - vyMin));
        }

        private void RecordCorridorBoundsVH(Vector2Int start, Vector2Int corner, Vector2Int end)
        {
            int halfW = currentCorridorWidth / 2;

            // 垂直段
            int vyMin = Mathf.Min(start.y, corner.y) - halfW;
            int vyMax = Mathf.Max(start.y, corner.y) + halfW;
            int vxMin = start.x - halfW;
            int vxMax = start.x + halfW;
            corridorBounds.Add(new RectInt(vxMin, vyMin, vxMax - vxMin, vyMax - vyMin));

            // 水平段
            int hxMin = Mathf.Min(corner.x, end.x) - halfW;
            int hxMax = Mathf.Max(corner.x, end.x) + halfW;
            int hyMin = corner.y - halfW;
            int hyMax = corner.y + halfW;
            corridorBounds.Add(new RectInt(hxMin, hyMin, hxMax - hxMin, hyMax - hyMin));
        }

        #endregion

        #region Path Smoothing

        /// <summary>
        /// 简单路径平滑 —— 去除重复点并保持顺序
        /// </summary>
        private List<Vector2Int> SmoothPath(List<Vector2Int> rawPath)
        {
            if (rawPath.Count <= 2) return rawPath;

            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            List<Vector2Int> smoothed = new List<Vector2Int>();

            foreach (var point in rawPath)
            {
                if (seen.Add(point))
                {
                    smoothed.Add(point);
                }
            }

            return smoothed;
        }

        #endregion

        #region Obstacle Clearing

        /// <summary>
        /// 清除走廊区域内的障碍物标记
        /// (具体清除逻辑由外部系统处理，这里标记区域)
        /// </summary>
        private void ClearObstaclesInCorridor(List<Vector2Int> path)
        {
            obstacleClearCache = new List<Vector2Int>(path);
        }

        /// <summary>
        /// 获取需要清除障碍物的位置列表
        /// </summary>
        public List<Vector2Int> GetObstacleClearPositions()
        {
            return obstacleClearCache;
        }

        #endregion

        #region Door Placement at Corridor Ends

        /// <summary>
        /// 检查是否在走廊两端放置门
        /// 30%概率在走廊两端放置门
        /// </summary>
        public bool ShouldPlaceDoorAtStart()
        {
            return Random.value < doorAtEndsChance;
        }

        public bool ShouldPlaceDoorAtEnd()
        {
            return Random.value < doorAtEndsChance;
        }

        /// <summary>
        /// 获取走廊起点的门位置
        /// </summary>
        public Vector2Int GetDoorPositionAtStart(Room roomA, Room roomB)
        {
            Vector2Int dir = roomB.center - roomA.center;
            Vector2Int doorPos;

            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                // 水平方向
                doorPos = new Vector2Int(
                    dir.x > 0 ? roomA.Right : roomA.Left,
                    roomA.center.y
                );
            }
            else
            {
                // 垂直方向
                doorPos = new Vector2Int(
                    roomA.center.x,
                    dir.y > 0 ? roomA.Top : roomA.Bottom
                );
            }

            return doorPos;
        }

        /// <summary>
        /// 获取走廊终点的门位置
        /// </summary>
        public Vector2Int GetDoorPositionAtEnd(Room roomA, Room roomB)
        {
            Vector2Int dir = roomA.center - roomB.center;
            Vector2Int doorPos;

            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                doorPos = new Vector2Int(
                    dir.x > 0 ? roomB.Right : roomB.Left,
                    roomB.center.y
                );
            }
            else
            {
                doorPos = new Vector2Int(
                    roomB.center.x,
                    dir.y > 0 ? roomB.Top : roomB.Bottom
                );
            }

            return doorPos;
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (corridorBounds == null) return;

            Gizmos.color = new Color(0.8f, 0.8f, 0.2f, 0.15f);
            foreach (var bounds in corridorBounds)
            {
                Vector3 center = new Vector3(bounds.center.x, bounds.center.y, 0);
                Vector3 size = new Vector3(bounds.width, bounds.height, 0);
                Gizmos.DrawCube(center, size);
            }
        }

        #endregion
    }
}
