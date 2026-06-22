using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using AngelGuardian.Core;
using AngelGuardian.Data;

namespace AngelGuardian.Cards
{
    /// <summary>
    /// Runtime instance of a card with current level and state.
    /// </summary>
    [Serializable]
    public class CardInstance
    {
        public string cardId;
        public int currentLevel = 1;
        public CardData cardData; // Reference to database entry

        /// <summary>Current effective value based on level.</summary>
        public float CurrentValue
        {
            get
            {
                if (cardData == null) return 0f;
                return cardData.baseValue + cardData.valuePerLevel * (currentLevel - 1);
            }
        }

        /// <summary>Is this card at max level?</summary>
        public bool IsMaxLevel
        {
            get
            {
                if (cardData == null) return true;
                return currentLevel >= cardData.maxLevel;
            }
        }

        /// <summary>Can this card still be upgraded?</summary>
        public bool CanUpgrade => !IsMaxLevel;

        public CardInstance() { }

        public CardInstance(CardData data, int level = 1)
        {
            cardId = data.cardId;
            cardData = data;
            currentLevel = level;
        }
    }

    /// <summary>
    /// Manages the player's card inventory during a run.
    /// Handles card addition, upgrading, fusion checking, and effect application
    /// across all 9 card categories (A through I).
    /// </summary>
    public class CardManager : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static CardManager _instance;
        public static CardManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<CardManager>();
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
        [SerializeField] private CardDatabase _cardDatabase;
        public CardDatabase CardDatabase
        {
            get
            {
                if (_cardDatabase == null)
                    _cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
                return _cardDatabase;
            }
            set => _cardDatabase = value;
        }

        [Header("Runtime State")]
        [SerializeField] private List<CardInstance> _cards = new List<CardInstance>();

        #endregion

        #region ─── Properties ───────────────────────────

        /// <summary>Maximum cards the player can hold.</summary>
        public int MaxCards
        {
            get
            {
                var config = GameManager.Instance?.Config;
                return config != null ? config.MaxCards : 10;
            }
        }

        /// <summary>Current number of cards held.</summary>
        public int CardCount => _cards.Count;

        /// <summary>Is the card inventory full?</summary>
        public bool IsFull => _cards.Count >= MaxCards;

        #endregion

        #region ─── Events ───────────────────────────────

        public event Action<CardInstance> OnCardAdded;
        public event Action<CardInstance, int> OnCardUpgraded; // (card, newLevel)
        public event Action<CardInstance> OnCardRemoved;
        public event Action<string, string, string> OnFusionExecuted; // (fusionId, cardA, cardB)

        #endregion

        #region ─── Unity Messages ───────────────────────

        private void Start()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnCardPickedUp.AddListener(OnCardPickedUpExternal);
            }
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnCardPickedUp.RemoveListener(OnCardPickedUpExternal);
            }
        }

        #endregion

        #region ─── Public API – Card Management ─────────

        /// <summary>
        /// Adds a card to the inventory. If already owned, upgrades it.
        /// If inventory is full, the card is discarded.
        /// </summary>
        /// <returns>True if successfully added/upgraded.</returns>
        public bool AddCard(CardData cardData)
        {
            if (cardData == null)
            {
                Debug.LogError("[CardManager] Cannot add null card.");
                return false;
            }

            // Check if already owned — upgrade instead
            CardInstance existing = GetCard(cardData.cardId);
            if (existing != null)
            {
                return UpgradeCard(cardData.cardId);
            }

            // Check capacity
            if (_cards.Count >= MaxCards)
            {
                Debug.LogWarning($"[CardManager] Card inventory full ({_cards.Count}/{MaxCards}). Cannot add {cardData.cardName}.");
                return false;
            }

            // Add new card
            CardInstance instance = new CardInstance(cardData, 1);
            _cards.Add(instance);

            Debug.Log($"[CardManager] Added card: {cardData.cardName} [{cardData.cardId}] " +
                      $"({cardData.category}) — {cardData.baseEffect}");

            OnCardAdded?.Invoke(instance);

            // Fire global event
            EventBus.Instance?.FireCardPickedUp(cardData.cardId);

            // Check for fusion opportunities
            CheckAllFusions();

            return true;
        }

        /// <summary>
        /// Upgrades an existing card by one level.
        /// </summary>
        /// <returns>True if upgrade was successful.</returns>
        public bool UpgradeCard(string cardId)
        {
            CardInstance card = GetCard(cardId);
            if (card == null)
            {
                Debug.LogWarning($"[CardManager] Cannot upgrade: card {cardId} not owned.");
                return false;
            }

            if (!card.CanUpgrade)
            {
                Debug.LogWarning($"[CardManager] {card.cardData.cardName} is already at max level ({card.currentLevel}/{card.cardData.maxLevel}).");
                return false;
            }

            card.currentLevel++;
            Debug.Log($"[CardManager] Upgraded: {card.cardData.cardName} → Level {card.currentLevel}/{card.cardData.maxLevel} " +
                      $"(Value: {card.CurrentValue:F1})");

            OnCardUpgraded?.Invoke(card, card.currentLevel);
            return true;
        }

        /// <summary>
        /// Removes a card from the inventory.
        /// </summary>
        public void RemoveCard(string cardId)
        {
            CardInstance card = GetCard(cardId);
            if (card == null) return;

            _cards.Remove(card);
            Debug.Log($"[CardManager] Removed card: {card.cardData.cardName}");

            OnCardRemoved?.Invoke(card);
        }

        /// <summary>
        /// Gets the current effective value for a card.
        /// </summary>
        public float GetCardEffectValue(string cardId)
        {
            CardInstance card = GetCard(cardId);
            return card?.CurrentValue ?? 0f;
        }

        /// <summary>
        /// Gets a card instance by ID.
        /// </summary>
        public CardInstance GetCard(string cardId)
        {
            return _cards.FirstOrDefault(c => c.cardId == cardId);
        }

        /// <summary>
        /// Checks if a card is owned.
        /// </summary>
        public bool HasCard(string cardId)
        {
            return _cards.Any(c => c.cardId == cardId);
        }

        /// <summary>
        /// Returns all owned cards.
        /// </summary>
        public List<CardInstance> GetAllCards()
        {
            return new List<CardInstance>(_cards);
        }

        /// <summary>
        /// Returns cards filtered by category.
        /// </summary>
        public List<CardInstance> GetCardsByCategory(CardCategory category)
        {
            return _cards.Where(c => c.cardData?.category == category).ToList();
        }

        #endregion

        #region ─── Fusion Checks ─────────────────────────

        /// <summary>
        /// Checks if two owned cards can be fused.
        /// </summary>
        public bool CheckFusionAvailable(string cardIdA, string cardIdB)
        {
            if (!HasCard(cardIdA) || !HasCard(cardIdB))
                return false;

            // Check if both are at max level (fusion requires max level cards)
            CardInstance cardA = GetCard(cardIdA);
            CardInstance cardB = GetCard(cardIdB);

            if (!cardA.IsMaxLevel || !cardB.IsMaxLevel)
                return false;

            // Check fusion path on either card
            string expectedFusion = $"{cardIdA}+{cardIdB}";
            string expectedFusionAlt = $"{cardIdB}+{cardIdA}";

            if (cardA.cardData?.fusionPath?.Contains(cardIdB) == true ||
                cardB.cardData?.fusionPath?.Contains(cardIdA) == true)
                return true;

            // Also check the FusionSystem for registered fusions
            FusionSystem fusionSystem = FusionSystem.Instance;
            if (fusionSystem != null)
            {
                return fusionSystem.CheckFusionAvailable(cardIdA, cardIdB);
            }

            return false;
        }

        /// <summary>
        /// Executes a fusion between two cards.
        /// </summary>
        /// <returns>The resulting fusion card, or null if failed.</returns>
        public CardInstance ExecuteFusion(string cardIdA, string cardIdB)
        {
            if (!CheckFusionAvailable(cardIdA, cardIdB))
            {
                Debug.LogWarning($"[CardManager] Fusion not available: {cardIdA}+{cardIdB}");
                return null;
            }

            // Try FusionSystem first
            FusionSystem fusionSystem = FusionSystem.Instance;
            if (fusionSystem != null)
            {
                CardData fusionResult = fusionSystem.ExecuteFusion(cardIdA, cardIdB);
                if (fusionResult != null)
                {
                    // Remove source cards
                    RemoveCard(cardIdA);
                    RemoveCard(cardIdB);

                    // Add fusion result
                    AddCard(fusionResult);

                    Debug.Log($"[CardManager] ✨ FUSION: {cardIdA}+{cardIdB} → {fusionResult.cardId} ({fusionResult.cardName})");
                    OnFusionExecuted?.Invoke(fusionResult.cardId, cardIdA, cardIdB);
                    return GetCard(fusionResult.cardId);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks all possible fusions among owned cards.
        /// </summary>
        public void CheckAllFusions()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                for (int j = i + 1; j < _cards.Count; j++)
                {
                    if (CheckFusionAvailable(_cards[i].cardId, _cards[j].cardId))
                    {
                        Debug.Log($"[CardManager] Fusion available: {_cards[i].cardId}+{_cards[j].cardId} " +
                                  $"({_cards[i].cardData?.cardName}+{_cards[j].cardData?.cardName})");
                    }
                }
            }
        }

        #endregion

        #region ─── Category Effect Processing ────────────

        /// <summary>
        /// Applies all card effects of a given category to the game state.
        /// Called each tick or on state changes.
        /// </summary>
        public CategoryEffects CalculateCategoryEffects()
        {
            CategoryEffects effects = new CategoryEffects();

            foreach (var card in _cards)
            {
                if (card.cardData == null) continue;

                switch (card.cardData.category)
                {
                    case CardCategory.A_Attack:
                        effects.angelDamageBonus += card.CurrentValue * 0.01f; // % conversion
                        effects.angelShieldBonus += card.CurrentValue * 0.01f;
                        break;

                    case CardCategory.B_BabyControl:
                        effects.babySlowPercent += card.CurrentValue * 0.01f;
                        effects.babyControlDuration += card.CurrentValue * 0.1f;
                        break;

                    case CardCategory.C_Aura:
                        effects.auraRadius += card.CurrentValue * 0.05f;
                        effects.auraEffectStrength += card.CurrentValue * 0.01f;
                        break;

                    case CardCategory.D_Terrain:
                        effects.terrainDuration += card.CurrentValue * 0.1f;
                        effects.terrainDamageBonus += card.CurrentValue * 0.01f;
                        break;

                    case CardCategory.E_Passive:
                        effects.critChanceBonus += card.CurrentValue * 0.01f;
                        effects.critDamageBonus += card.CurrentValue * 0.02f;
                        effects.maxHPBonus += card.CurrentValue * 0.01f;
                        break;

                    case CardCategory.F_Growth:
                        effects.expMultiplier += card.CurrentValue * 0.01f;
                        effects.growthSpeedBonus += card.CurrentValue * 0.01f;
                        break;

                    case CardCategory.G_Emotion:
                        effects.emotionEffectStrength += card.CurrentValue * 0.01f;
                        break;

                    case CardCategory.H_Combo:
                        effects.comboChargeRate += card.CurrentValue * 0.01f;
                        effects.comboEffectBonus += card.CurrentValue * 0.01f;
                        break;

                    case CardCategory.I_TerrainActivation:
                        effects.activationDuration += card.CurrentValue * 0.1f;
                        effects.activationDamageBonus += card.CurrentValue * 0.01f;
                        break;
                }
            }

            return effects;
        }

        #endregion

        #region ─── Event Handlers ───────────────────────

        private void OnCardPickedUpExternal(string cardId)
        {
            Debug.Log($"[CardManager] External card pickup: {cardId}");
        }

        #endregion

        #region ─── Reset ────────────────────────────────

        /// <summary>
        /// Clears all cards for a new run.
        /// </summary>
        public void ResetAll()
        {
            _cards.Clear();
        }

        #endregion

        #region ─── Debug ────────────────────────────────

        [ContextMenu("Log Inventory")]
        private void LogInventory()
        {
            Debug.Log($"[CardManager] === Card Inventory ({_cards.Count}/{MaxCards}) ===");
            foreach (var card in _cards)
            {
                Debug.Log($"  {card.cardId} {card.cardData?.cardName}: " +
                          $"Lv.{card.currentLevel}/{card.cardData?.maxLevel} " +
                          $"Val:{card.CurrentValue:F1} | {card.cardData?.category}");
            }

            var effects = CalculateCategoryEffects();
            Debug.Log($"[CardManager] Cumulative Effects: {effects}");
        }

        #endregion
    }

    /// <summary>
    /// Accumulated effect values from all card categories.
    /// </summary>
    [Serializable]
    public class CategoryEffects
    {
        // A类 - 攻击/防御
        public float angelDamageBonus = 0f;
        public float angelShieldBonus = 0f;

        // B类 - 婴灵控制
        public float babySlowPercent = 0f;
        public float babyControlDuration = 0f;

        // C类 - 光环
        public float auraRadius = 0f;
        public float auraEffectStrength = 0f;

        // D类 - 地形
        public float terrainDuration = 0f;
        public float terrainDamageBonus = 0f;

        // E类 - 被动
        public float critChanceBonus = 0f;
        public float critDamageBonus = 0f;
        public float maxHPBonus = 0f;

        // F类 - 成长
        public float expMultiplier = 1f;
        public float growthSpeedBonus = 0f;

        // G类 - 情感
        public float emotionEffectStrength = 0f;

        // H类 - 连携
        public float comboChargeRate = 0f;
        public float comboEffectBonus = 0f;

        // I类 - 地形活化
        public float activationDuration = 0f;
        public float activationDamageBonus = 0f;

        public override string ToString()
        {
            return $"Dmg:{angelDamageBonus:P0} Shield:{angelShieldBonus:P0} " +
                   $"Crit:{critChanceBonus:P0}/{critDamageBonus:P0} " +
                   $"EXP:{expMultiplier:F2}x Combo:{comboChargeRate:P0}";
        }
    }
}
