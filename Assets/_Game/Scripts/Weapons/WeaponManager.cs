using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AngelGuardian.Core;
using AngelGuardian.Data;

namespace AngelGuardian.Weapons
{
    /// <summary>
    /// Manages the player's weapon inventory: holding, switching, adding, and replacing weapons.
    /// Max 6 weapons (configurable via GameConfig).
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static WeaponManager _instance;
        public static WeaponManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<WeaponManager>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        #endregion

        #region ─── Inspector ────────────────────────────

        [Header("References")]
        [SerializeField] private WeaponDatabase _weaponDatabase;
        public WeaponDatabase WeaponDatabase
        {
            get
            {
                if (_weaponDatabase == null)
                    _weaponDatabase = Resources.Load<WeaponDatabase>("WeaponDatabase");
                return _weaponDatabase;
            }
            set => _weaponDatabase = value;
        }

        [Header("Runtime State")]
        [SerializeField, ReadOnly] private int _currentWeaponIndex = 0;
        [SerializeField, ReadOnly] private List<WeaponBase> _weapons = new List<WeaponBase>();

        #endregion

        #region ─── Properties ───────────────────────────

        /// <summary>Maximum number of weapons the player can hold.</summary>
        public int MaxWeapons
        {
            get
            {
                var config = GameManager.Instance?.Config;
                return config != null ? config.MaxWeapons : 6;
            }
        }

        /// <summary>Current active weapon index.</summary>
        public int CurrentWeaponIndex => _currentWeaponIndex;

        /// <summary>Current active weapon.</summary>
        public WeaponBase CurrentWeapon
        {
            get
            {
                if (_weapons.Count == 0) return null;
                if (_currentWeaponIndex < 0 || _currentWeaponIndex >= _weapons.Count)
                    _currentWeaponIndex = 0;
                return _weapons[_currentWeaponIndex];
            }
        }

        /// <summary>Number of weapons currently held.</summary>
        public int WeaponCount => _weapons.Count;

        /// <summary>Is the weapon inventory full?</summary>
        public bool IsFull => _weapons.Count >= MaxWeapons;

        #endregion

        #region ─── Events ───────────────────────────────

        public System.Action<int, WeaponBase> OnWeaponSwitched;
        public System.Action<WeaponBase> OnWeaponAdded;
        public System.Action<int, WeaponBase, WeaponBase> OnWeaponReplaced; // (index, oldWeapon, newWeapon)
        public System.Action<int, WeaponBase> OnWeaponRemoved;
        public System.Action<WeaponBase> OnAOEBurst; // Triggered when old weapon creates AOE burst

        #endregion

        #region ─── Unity Messages ───────────────────────

        private void Start()
        {
            // Subscribe to weapon pickup event
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnWeaponPickedUp.AddListener(OnWeaponPickedUpExternal);
            }
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnWeaponPickedUp.RemoveListener(OnWeaponPickedUpExternal);
            }
        }

        private void Update()
        {
            // Handle weapon switching input (1-6 keys)
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Playing)
            {
                for (int i = 0; i < MaxWeapons; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        SwitchWeapon(i);
                    }
                }

                // Mouse wheel / Q/E for next/previous
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0.1f)
                    SwitchWeapon((_currentWeaponIndex + 1) % _weapons.Count);
                else if (scroll < -0.1f)
                    SwitchWeapon((_currentWeaponIndex - 1 + _weapons.Count) % _weapons.Count);

                if (Input.GetKeyDown(KeyCode.Q))
                    SwitchWeapon((_currentWeaponIndex - 1 + _weapons.Count) % _weapons.Count);
                if (Input.GetKeyDown(KeyCode.E))
                    SwitchWeapon((_currentWeaponIndex + 1) % _weapons.Count);
            }
        }

        #endregion

        #region ─── Public API – Weapon Management ───────

        /// <summary>
        /// Switches to the weapon at the specified index.
        /// </summary>
        /// <returns>True if the switch was successful.</returns>
        public bool SwitchWeapon(int index)
        {
            if (_weapons.Count == 0)
                return false;

            if (index < 0 || index >= _weapons.Count)
                return false;

            if (index == _currentWeaponIndex)
                return false;

            int oldIndex = _currentWeaponIndex;
            _currentWeaponIndex = index;

            Debug.Log($"[WeaponManager] Switched weapon: {_weapons[oldIndex]?.weaponName} → {_weapons[index]?.weaponName}");

            OnWeaponSwitched?.Invoke(index, _weapons[index]);
            return true;
        }

        /// <summary>
        /// Adds a weapon to the inventory. If full, replaces the current weapon.
        /// </summary>
        /// <returns>True if added, false if replaced or failed.</returns>
        public bool AddWeapon(WeaponBase weapon)
        {
            if (weapon == null)
            {
                Debug.LogError("[WeaponManager] Cannot add null weapon.");
                return false;
            }

            // Check if already owned
            if (_weapons.Any(w => w.weaponId == weapon.weaponId))
            {
                Debug.LogWarning($"[WeaponManager] Already own weapon: {weapon.weaponName}");
                return false;
            }

            // If not full, add directly
            if (_weapons.Count < MaxWeapons)
            {
                // Create runtime instance so ScriptableObject isn't mutated
                WeaponBase runtimeWeapon = weapon.CreateRuntimeInstance();
                _weapons.Add(runtimeWeapon);

                Debug.Log($"[WeaponManager] Added weapon: {weapon.weaponName} (slot {_weapons.Count - 1})");

                OnWeaponAdded?.Invoke(runtimeWeapon);

                // Fire global event
                EventBus.Instance?.FireWeaponPickedUp(weapon.weaponId);

                return true;
            }

            // Full — replace current weapon
            Debug.Log($"[WeaponManager] Inventory full. Replacing {CurrentWeapon.weaponName} with {weapon.weaponName}");
            ReplaceWeapon(_currentWeaponIndex, weapon);
            return false;
        }

        /// <summary>
        /// Replaces a weapon at the given index. Old weapon triggers an AOE burst.
        /// </summary>
        public void ReplaceWeapon(int index, WeaponBase newWeapon)
        {
            if (newWeapon == null)
            {
                Debug.LogError("[WeaponManager] Cannot replace with null weapon.");
                return;
            }

            if (index < 0 || index >= _weapons.Count)
            {
                Debug.LogError($"[WeaponManager] Invalid weapon index: {index}");
                return;
            }

            WeaponBase oldWeapon = _weapons[index];
            WeaponBase runtimeWeapon = newWeapon.CreateRuntimeInstance();

            _weapons[index] = runtimeWeapon;

            // Trigger AOE burst from the old weapon
            TriggerAOEBurst(oldWeapon);

            Debug.Log($"[WeaponManager] Replaced {oldWeapon.weaponName} with {newWeapon.weaponName} at slot {index}");

            OnWeaponReplaced?.Invoke(index, oldWeapon, runtimeWeapon);

            // Fire global event
            EventBus.Instance?.FireWeaponPickedUp(newWeapon.weaponId);
        }

        /// <summary>
        /// Removes a weapon at the given index.
        /// </summary>
        public void RemoveWeapon(int index)
        {
            if (index < 0 || index >= _weapons.Count)
                return;

            WeaponBase removed = _weapons[index];
            _weapons.RemoveAt(index);

            // Adjust current index
            if (_currentWeaponIndex >= _weapons.Count)
                _currentWeaponIndex = Mathf.Max(0, _weapons.Count - 1);

            Debug.Log($"[WeaponManager] Removed weapon: {removed.weaponName}");

            OnWeaponRemoved?.Invoke(index, removed);
        }

        /// <summary>
        /// Returns all currently held weapons.
        /// </summary>
        public List<WeaponBase> GetAllWeapons()
        {
            return new List<WeaponBase>(_weapons);
        }

        /// <summary>
        /// Gets a weapon by ID if owned.
        /// </summary>
        public WeaponBase GetWeaponById(string weaponId)
        {
            return _weapons.FirstOrDefault(w => w.weaponId == weaponId);
        }

        /// <summary>
        /// Checks if a specific weapon is owned.
        /// </summary>
        public bool HasWeapon(string weaponId)
        {
            return _weapons.Any(w => w.weaponId == weaponId);
        }

        /// <summary>
        /// Gets weapon data from the database by ID.
        /// </summary>
        public WeaponData GetWeaponData(string weaponId)
        {
            return WeaponDatabase?.GetWeapon(weaponId);
        }

        #endregion

        #region ─── Combat Integration ───────────────────

        /// <summary>
        /// Performs an attack with the current weapon.
        /// Called by AngelCombat or input system.
        /// </summary>
        public void Attack(Vector3 from, Vector3 direction)
        {
            if (CurrentWeapon == null) return;

            int projectileCount = CurrentWeapon.Attack(from, direction);

            // Record the attack for combo system
            ComboSystem.Instance?.AddComboCharge(CurrentWeapon.ComboWeight);

            Debug.Log($"[WeaponManager] Attack: {CurrentWeapon.weaponName} spawned {projectileCount} projectile(s)");
        }

        /// <summary>
        /// Performs an AOE burst (when old weapon is replaced).
        /// </summary>
        private void TriggerAOEBurst(WeaponBase oldWeapon)
        {
            if (oldWeapon == null) return;

            float burstDamage = oldWeapon.EffectiveDamage * 2f;
            float burstRadius = oldWeapon.attackRange * 1.5f;

            Vector3 burstOrigin = transform.position;

            Debug.Log($"[WeaponManager] AOE Burst from {oldWeapon.weaponName}: {burstDamage} dmg in {burstRadius} radius");

            // Find all enemies in range and apply damage
            Collider[] hits = Physics.OverlapSphere(burstOrigin, burstRadius);
            foreach (var hit in hits)
            {
                // TODO: Apply damage via enemy damage interface
                Debug.Log($"[WeaponManager] AOE Burst hit: {hit.name}");
            }

            OnAOEBurst?.Invoke(oldWeapon);
        }

        #endregion

        #region ─── Event Handlers ───────────────────────

        private void OnWeaponPickedUpExternal(string weaponId)
        {
            // Already handled by AddWeapon, but can be used for UI notifications
            Debug.Log($"[WeaponManager] External pickup event: {weaponId}");
        }

        #endregion

        #region ─── Reset ────────────────────────────────

        /// <summary>
        /// Clears all weapons for a new run.
        /// </summary>
        public void ResetAll()
        {
            foreach (var w in _weapons)
            {
                w.ResetRuntimeState();
            }
            _weapons.Clear();
            _currentWeaponIndex = 0;
        }

        #endregion

        #region ─── Debug ────────────────────────────────

        [ContextMenu("Log Inventory")]
        private void LogInventory()
        {
            Debug.Log($"[WeaponManager] === Weapon Inventory ({_weapons.Count}/{MaxWeapons}) ===");
            for (int i = 0; i < _weapons.Count; i++)
            {
                string marker = (i == _currentWeaponIndex) ? " [ACTIVE]" : "";
                var w = _weapons[i];
                Debug.Log($"  [{i}] {w.weaponName} ({w.rarity}) | Dmg:{w.EffectiveDamage:F0} | Kills:{w.TotalKills} | Growth:{w.Growth}{marker}");
            }
        }

        #endregion
    }
}
