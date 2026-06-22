using UnityEngine;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// 经验管理器 - 管理经验获取和等级提升
    /// </summary>
    public class ExperienceManager : MonoBehaviour
    {
        public static ExperienceManager Instance { get; private set; }

        [SerializeField] private int currentExperience = 0;
        [SerializeField] private int experienceToNextLevel = 100;

        public int CurrentExperience => currentExperience;
        public int ExperienceToNextLevel => experienceToNextLevel;

        public event System.Action<int, int> OnExperienceChanged;
        public event System.Action<int> OnLevelUp;

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

        public void AddExperience(int amount, Vector3 position)
        {
            currentExperience += amount;
            OnExperienceChanged?.Invoke(currentExperience, experienceToNextLevel);

            // 检查升级
            while (currentExperience >= experienceToNextLevel)
            {
                currentExperience -= experienceToNextLevel;
                experienceToNextLevel = Mathf.RoundToInt(experienceToNextLevel * 1.2f);
                OnLevelUp?.Invoke(experienceToNextLevel);
            }
        }
    }
}
