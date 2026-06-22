using UnityEngine;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// 金币管理器 - 管理金币获取
    /// </summary>
    public class GoldManager : MonoBehaviour
    {
        public static GoldManager Instance { get; private set; }

        [SerializeField] private int currentGold = 0;

        public int CurrentGold => currentGold;

        public event System.Action<int, int> OnGoldChanged;

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
            }
        }

        public void AddGold(int amount, Vector3 position)
        {
            int previousGold = currentGold;
            currentGold += amount;
            OnGoldChanged?.Invoke(currentGold, amount);

            Debug.Log($"[GoldManager] +{amount} 金币 (总计: {currentGold}), 位置: {position}");
        }

        public bool SpendGold(int amount)
        {
            if (currentGold >= amount)
            {
                currentGold -= amount;
                OnGoldChanged?.Invoke(currentGold, -amount);
                return true;
            }
            return false;
        }
    }
}
