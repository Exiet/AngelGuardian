using System.Collections.Generic;
using UnityEngine;

namespace AngelGuardian.Dungeon
{
    /// <summary>
    /// 房间数据模型 —— BSP生成的地图房间单元
    /// </summary>
    [System.Serializable]
    public class Room
    {
        [Header("Position & Size")]
        public int x;
        public int y;
        public int w;
        public int h;

        [Header("Computed Properties")]
        public Vector2Int center;
        public float area;

        [Header("Flags")]
        public bool isConnected;
        public bool isPocketRoom;       // 口袋房间：只有1个出入口
        public bool isSafeRoom;         // 安全初始区：出生点附近无敌人生成
        public bool isCorridor;         // 标记为走廊类型(非房间)

        [Header("Connections")]
        public List<int> connectedRooms; // 连接的其他房间索引

        /// <summary>
        /// 房间矩形的四个边界
        /// </summary>
        public int Left   => x;
        public int Right  => x + w;
        public int Bottom => y;
        public int Top    => y + h;

        public Room()
        {
            connectedRooms = new List<int>();
        }

        public Room(int x, int y, int w, int h)
        {
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
            this.center = new Vector2Int(x + w / 2, y + h / 2);
            this.area = w * h;
            this.isConnected = false;
            this.isPocketRoom = false;
            this.isSafeRoom = false;
            this.isCorridor = false;
            this.connectedRooms = new List<int>();
        }

        /// <summary>
        /// 重新计算中心点和面积
        /// </summary>
        public void RecalculateCenter()
        {
            center = new Vector2Int(x + w / 2, y + h / 2);
            area = w * h;
        }

        /// <summary>
        /// 检查是否与另一个房间重叠
        /// </summary>
        public bool Overlaps(Room other, int padding = 0)
        {
            return !(Right + padding <= other.Left ||
                     Left - padding >= other.Right ||
                     Top + padding <= other.Bottom ||
                     Bottom - padding >= other.Top);
        }

        /// <summary>
        /// 检查一个点是否在房间内
        /// </summary>
        public bool Contains(Vector2Int point)
        {
            return point.x >= Left && point.x < Right &&
                   point.y >= Bottom && point.y < Top;
        }

        /// <summary>
        /// 获取两个房间中心之间的欧几里得距离
        /// </summary>
        public float DistanceTo(Room other)
        {
            return Vector2Int.Distance(center, other.center);
        }

        /// <summary>
        /// 获取两个房间中心之间的曼哈顿距离
        /// </summary>
        public int ManhattanDistanceTo(Room other)
        {
            return Mathf.Abs(center.x - other.center.x) +
                   Mathf.Abs(center.y - other.center.y);
        }

        /// <summary>
        /// 连接另一个房间
        /// </summary>
        public void ConnectTo(int roomIndex)
        {
            if (!connectedRooms.Contains(roomIndex))
            {
                connectedRooms.Add(roomIndex);
            }
            isConnected = connectedRooms.Count > 0;
            UpdatePocketRoomFlag();
        }

        /// <summary>
        /// 断开与另一个房间的连接
        /// </summary>
        public void DisconnectFrom(int roomIndex)
        {
            connectedRooms.Remove(roomIndex);
            isConnected = connectedRooms.Count > 0;
            UpdatePocketRoomFlag();
        }

        /// <summary>
        /// 更新口袋房间标记
        /// </summary>
        public void UpdatePocketRoomFlag()
        {
            isPocketRoom = connectedRooms.Count == 1;
        }

        /// <summary>
        /// 获取房间的矩形表示
        /// </summary>
        public RectInt GetRect()
        {
            return new RectInt(x, y, w, h);
        }

        public override string ToString()
        {
            return $"Room(x:{x}, y:{y}, w:{w}, h:{h}, center:{center}, " +
                   $"connected:{isConnected}, pocket:{isPocketRoom}, safe:{isSafeRoom})";
        }
    }
}
