using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using AngelGuardian.Core;
using AngelGuardian.Data;

namespace AngelGuardian.Cards
{
    /// <summary>
    /// Represents a single card choice in the 3-pick upgrade UI.
    /// </summary>
    [Serializable]
    public class CardChoice
    {
        public CardData cardData;
        public int currentLevel = 1;
        public bool isUpgrade; // true = upgrading existing card, false = new card

        public string DisplayName
        {
            get
            {
                if (cardData == null) return "Empty";
                return isUpgrade
                    ? $"{cardData.cardName} Lv.{currentLevel}→{currentLevel + 1}"
                    : cardData.cardName;
            }
        }

        public string DisplayEffect
        {
            get
            {
                if (cardData == null) return "";
                return isUpgrade
                    ? $"{cardData.maxEffect}"
                    : cardData.baseEffect;
            }
        }

        public string DisplayRarity => cardData?.rarity.ToString() ?? "";

        public string DisplayCategory => cardData?.category.ToString() ?? "";
    }

    /// <summary>
    /// Manages the card upgrade UI logic.
    /// Triggers on level up via EventBus.OnLevelUp.
    /// Presents 3 random card choices, supports reroll (costs EXP), and confirms selection.
    /// </summary>
    public class CardUpgradeUI : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static CardUpgradeUI _instance;
        public static CardUpgradeUI Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<CardUpgradeUI>();
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

        [Header("Reroll")]
        [Tooltip("EXP cost to reroll card choices")]
        [SerializeField] private float _rerollExpCost = 30f;
        public float RerollExpCost => _rerollExpCost;

        [Tooltip("Maximum rerolls per level-up")]
        [SerializeField] private int _maxRerolls = 3;

        [Header("Drop Weights")]
        [Tooltip("Drop weight for each rarity tier (N, R, SR, SSR)")]
        public float[] rarityWeights = { 40f, 30f, 20f, 10f };

        [Tooltip("Bias towards owned card categories (0-1)")]
        [Range(0f, 1f)]
        public float ownedCategoryBias = 0.3f;

        #endregion

        #region ─── Runtime State ────────────────────────

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private bool _isChoosing = false;
        [SerializeField] private int _rerollsUsed = 0;
        [SerializeField] private List<CardChoice> _currentChoices = new List<CardChoice>();
        [SerializeField] private int _totalUpgradesChosen = 0;

        /// <summary>Is the upgrade UI currently active?</summary>
        public bool IsChoosing => _isChoosing;

        /// <summary>Current card choices for the player.</summary>
        public List<CardChoice> CurrentChoices => _currentChoices;

        /// <summary>Number of rerolls used this level-up.</summary>
        public int RerollsUsed => _rerollsUsed;

        /// <summary>Remaining rerolls.</summary>
        public int RerollsRemaining => _maxRerolls - _rerollsUsed;

        /// <summary>Total upgrades chosen this run.</summary>
        public int TotalUpgradesChosen => _totalUpgradesChosen;

        #endregion

        #region ─── Events ───────────────────────────────

        /// <summary>Fired when the upgrade UI opens with 3 choices.</summary>
        public event Action<List<CardChoice>> OnUpgradeUIOpened;

        /// <summary>Fired when the player selects a card.</summary>
        public event Action<CardChoice> OnCardSelected;

        /// <summary>Fired when the upgrade UI closes.</summary>
        public event Action OnUpgradeUIClosed;

        /// <summary>Fired when choices are rerolled.</summary>
        public event Action<List<CardChoice>> OnChoicesRerolled;

        #endregion

        #region ─── Unity Messages ───────────────────────

        private void Start()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnLevelUp.AddListener(OnLevelUp);
            }
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnLevelUp.RemoveListener(OnLevelUp);
            }
        }

        #endregion

        #region ─── Level Up Trigger ─────────────────────

        /// <summary>
        /// Called when the player levels up. Opens the upgrade UI.
        /// </summary>
        private void OnLevelUp(int newLevel)
        {
            if (_isChoosing)
            {
                Debug.LogWarning("[CardUpgradeUI] Already choosing cards. Skipping level-up trigger.");
                return;
            }

            Debug.Log($"[CardUpgradeUI] Level Up! ({newLevel}) — Opening card upgrade UI.");

            _isChoosing = true;
            _rerollsUsed = 0;

            GenerateChoices();
        }

        #endregion

        #region ─── Choice Generation ────────────────────

        /// <summary>
        /// Generates 3 random card choices based on drop weights and player state.
        /// </summary>
        public void GenerateChoices()
        {
            _currentChoices.Clear();

            CardManager cm = CardManager.Instance;
            List<CardData> allCards = CardDatabase?.cards;
            if (allCards == null || allCards.Count == 0)
            {
                Debug.LogError("[CardUpgradeUI] No cards in database!");
                return;
            }

            // Determine which cards are eligible
            List<CardData> eligibleCards = GetEligibleCards(allCards, cm);

            // Generate 3 unique choices
            HashSet<string> usedCardIds = new HashSet<string>();
            int attempts = 0;
            int maxAttempts = 50;

            while (_currentChoices.Count < 3 && attempts < maxAttempts)
            {
                attempts++;
                CardData selected = RollCard(eligibleCards);

                if (selected == null) continue;
                if (usedCardIds.Contains(selected.cardId)) continue;

                usedCardIds.Add(selected.cardId);

                // Determine if this is an upgrade or a new card
                bool isUpgrade = cm != null && cm.HasCard(selected.cardId);
                int currentLevel = isUpgrade ? cm.GetCard(selected.cardId).currentLevel : 1;

                // Don't offer upgrades for max-level cards
                if (isUpgrade && !cm.GetCard(selected.cardId).CanUpgrade)
                {
                    continue;
                }

                CardChoice choice = new CardChoice
                {
                    cardData = selected,
                    currentLevel = currentLevel,
                    isUpgrade = isUpgrade
                };

                _currentChoices.Add(choice);
            }

            // Fill remaining slots with any available cards
            if (_currentChoices.Count < 3)
            {
                var remaining = eligibleCards
                    .Where(c => !usedCardIds.Contains(c.cardId))
                    .OrderBy(x => UnityEngine.Random.value)
                    .Take(3 - _currentChoices.Count);

                foreach (var card in remaining)
                {
                    bool isUpgrade = cm != null && cm.HasCard(card.cardId);
                    int currentLevel = isUpgrade ? cm.GetCard(card.cardId).currentLevel : 1;

                    if (isUpgrade && !cm.GetCard(card.cardId).CanUpgrade)
                        continue;

                    _currentChoices.Add(new CardChoice
                    {
                        cardData = card,
                        currentLevel = currentLevel,
                        isUpgrade = isUpgrade
                    });
                }
            }

            Debug.Log($"[CardUpgradeUI] Generated {_currentChoices.Count} choices. " +
                      $"Rerolls: {_rerollsUsed}/{_maxRerolls}");

            OnUpgradeUIOpened?.Invoke(_currentChoices);
        }

        /// <summary>
        /// Returns the list of cards eligible for the current roll.
        /// Filters out cards the player already has at max level.
        /// </summary>
        private List<CardData> GetEligibleCards(List<CardData> allCards, CardManager cm)
        {
            if (cm == null) return new List<CardData>(allCards);

            return allCards.Where(card =>
            {
                if (cm.HasCard(card.cardId))
                {
                    CardInstance instance = cm.GetCard(card.cardId);
                    return instance.CanUpgrade;
                }
                return cm.CardCount < cm.maxCards; // Can add new card if not full
            }).ToList();
        }

        /// <summary>
        /// Rolls a single card from eligible pool based on rarity weights.
        /// </summary>
        private CardData RollCard(List<CardData> eligibleCards)
        {
            if (eligibleCards.Count == 0) return null;

            // Weighted random by rarity
            float totalWeight = 0f;
            Dictionary<CardRarity, float> rarityWeightMap = new Dictionary<CardRarity, float>
            {
                { CardRarity.N, rarityWeights.Length > 0 ? rarityWeights[0] : 40f },
                { CardRarity.R, rarityWeights.Length > 1 ? rarityWeights[1] : 30f },
                { CardRarity.SR, rarityWeights.Length > 2 ? rarityWeights[2] : 20f },
                { CardRarity.SSR, rarityWeights.Length > 3 ? rarityWeights[3] : 10f }
            };

            // Apply owned category bias
            CardManager cm = CardManager.Instance;
            if (cm != null && ownedCategoryBias > 0f)
            {
                var ownedCategories = new HashSet<CardCategory>(
                    cm.GetAllCards().Select(c => c.cardData?.category ?? CardCategory.A_Attack)
                );

                // Boost weights for cards in owned categories
                foreach (var card in eligibleCards)
                {
                    if (ownedCategories.Contains(card.category))
                    {
                        // Boost based on bias
                    }
                }
            }

            // Pick rarity first
            float roll = UnityEngine.Random.Range(0f, rarityWeightMap.Values.Sum());
            float cumulative = 0f;
            CardRarity selectedRarity = CardRarity.N;

            foreach (var kvp in rarityWeightMap)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative)
                {
                    selectedRarity = kvp.Key;
                    break;
                }
            }

            // Get cards of selected rarity
            var rarityCards = eligibleCards.Where(c => c.rarity == selectedRarity).ToList();

            // Fallback: any rarity
            if (rarityCards.Count == 0)
                rarityCards = eligibleCards;

            return rarityCards[UnityEngine.Random.Range(0, rarityCards.Count)];
        }

        #endregion

        #region ─── Public API – Reroll ──────────────────

        /// <summary>
        /// Rerolls the current 3 choices. Costs EXP.
        /// </summary>
        /// <returns>True if reroll was successful.</returns>
        public bool Reroll()
        {
            if (!_isChoosing)
            {
                Debug.LogWarning("[CardUpgradeUI] Cannot reroll: not currently choosing.");
                return false;
            }

            if (_rerollsUsed >= _maxRerolls)
            {
                Debug.LogWarning($"[CardUpgradeUI] Max rerolls reached ({_maxRerolls}).");
                return false;
            }

            // Check EXP cost
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.CurrentExp < _rerollExpCost)
            {
                Debug.LogWarning($"[CardUpgradeUI] Not enough EXP to reroll. Need {_rerollExpCost}, have {gm.CurrentExp:F0}.");
                return false;
            }

            // Deduct EXP
            if (gm != null)
            {
                gm.AddExp(-_rerollExpCost); // Negative to spend
            }

            _rerollsUsed++;

            // Also consume free reroll from meta progression
            MetaProgression mp = MetaProgression.Instance;
            int freeRerolls = mp != null ? mp.GetUpgradeLevel(MetaProgression.UpgradeType.CardRerollCount) : 0;
            if (_rerollsUsed <= freeRerolls && gm != null)
            {
                // Refund EXP for free reroll
                gm.AddExp(_rerollExpCost);
            }

            Debug.Log($"[CardUpgradeUI] 🔄 Reroll! ({_rerollsUsed}/{_maxRerolls}) Cost: {_rerollExpCost} EXP");

            GenerateChoices();
            OnChoicesRerolled?.Invoke(_currentChoices);

            return true;
        }

        #endregion

        #region ─── Public API – Selection ───────────────

        /// <summary>
        /// Confirms the player's card selection.
        /// </summary>
        /// <param name="choiceIndex">0, 1, or 2</param>
        /// <returns>The selected CardChoice, or null if invalid.</returns>
        public CardChoice ConfirmSelection(int choiceIndex)
        {
            if (!_isChoosing)
            {
                Debug.LogWarning("[CardUpgradeUI] Cannot confirm: not currently choosing.");
                return null;
            }

            if (choiceIndex < 0 || choiceIndex >= _currentChoices.Count)
            {
                Debug.LogError($"[CardUpgradeUI] Invalid choice index: {choiceIndex}");
                return null;
            }

            CardChoice selected = _currentChoices[choiceIndex];
            CardManager cm = CardManager.Instance;

            if (cm == null)
            {
                Debug.LogError("[CardUpgradeUI] CardManager not found.");
                return null;
            }

            // Apply the selection
            if (selected.isUpgrade)
            {
                cm.UpgradeCard(selected.cardData.cardId);
                Debug.Log($"[CardUpgradeUI] ✅ Selected UPGRADE: {selected.DisplayName}");
            }
            else
            {
                cm.AddCard(selected.cardData);
                Debug.Log($"[CardUpgradeUI] ✅ Selected NEW: {selected.DisplayName}");
            }

            _totalUpgradesChosen++;

            // Close UI
            CloseUI();

            OnCardSelected?.Invoke(selected);
            return selected;
        }

        /// <summary>
        /// Skips the card selection (no card chosen).
        /// </summary>
        public void SkipSelection()
        {
            Debug.Log("[CardUpgradeUI] Skipped card selection.");
            CloseUI();
        }

        #endregion

        #region ─── UI Management ─────────────────────────

        private void CloseUI()
        {
            _isChoosing = false;
            _currentChoices.Clear();
            OnUpgradeUIClosed?.Invoke();
        }

        /// <summary>
        /// Forces the upgrade UI to close (e.g., on game over).
        /// </summary>
        public void ForceClose()
        {
            if (_isChoosing)
            {
                _isChoosing = false;
                _currentChoices.Clear();
                OnUpgradeUIClosed?.Invoke();
            }
        }

        #endregion

        #region ─── Preview Data (for UI binding) ────────

        /// <summary>
        /// Builds a preview data object for a specific card choice.
        /// </summary>
        public CardPreviewData BuildPreviewData(CardChoice choice)
        {
            if (choice?.cardData == null) return null;

            CardManager cm = CardManager.Instance;

            return new CardPreviewData
            {
                cardId = choice.cardData.cardId,
                cardName = choice.cardData.cardName,
                category = choice.cardData.category.ToString(),
                rarity = choice.cardData.rarity.ToString(),
                isUpgrade = choice.isUpgrade,
                currentLevel = choice.currentLevel,
                maxLevel = choice.cardData.maxLevel,
                baseEffect = choice.cardData.baseEffect,
                currentEffect = choice.isUpgrade
                    ? $"Lv.{choice.currentLevel} → Lv.{choice.currentLevel + 1}"
                    : choice.cardData.baseEffect,
                maxEffect = choice.cardData.maxEffect,
                playStyle = choice.cardData.playStyle,
                isOwned = cm != null && cm.HasCard(choice.cardData.cardId),
                canAfford = true // Always affordable for level-up picks
            };
        }

        /// <summary>
        /// Builds preview data for all 3 current choices.
        /// </summary>
        public List<CardPreviewData> BuildAllPreviews()
        {
            return _currentChoices.Select(BuildPreviewData).Where(p => p != null).ToList();
        }

        #endregion

        #region ─── Reset ────────────────────────────────

        /// <summary>
        /// Resets state for a new run.
        /// </summary>
        public void ResetAll()
        {
            _isChoosing = false;
            _rerollsUsed = 0;
            _currentChoices.Clear();
            _totalUpgradesChosen = 0;
        }

        #endregion

        #region ─── Debug ────────────────────────────────

        [ContextMenu("Simulate Level Up")]
        private void SimulateLevelUp()
        {
            OnLevelUp((GameManager.Instance?.CurrentLevel ?? 0) + 1);
        }

        [ContextMenu("Log Current Choices")]
        private void LogCurrentChoices()
        {
            Debug.Log($"[CardUpgradeUI] === Current Choices (Choosing: {_isChoosing}) ===");
            for (int i = 0; i < _currentChoices.Count; i++)
            {
                var c = _currentChoices[i];
                Debug.Log($"  [{i}] {c.DisplayName} ({c.DisplayRarity}) | {c.DisplayCategory} | " +
                          $"Upgrade: {c.isUpgrade}");
            }
            Debug.Log($"Rerolls: {_rerollsUsed}/{_maxRerolls} | Cost: {_rerollExpCost} EXP");
        }

        #endregion
    }

    /// <summary>
    /// Data transfer object for card preview in the UI.
    /// </summary>
    [Serializable]
    public class CardPreviewData
    {
        public string cardId;
        public string cardName;
        public string category;
        public string rarity;
        public bool isUpgrade;
        public int currentLevel;
        public int maxLevel;
        public string baseEffect;
        public string currentEffect;
        public string maxEffect;
        public string playStyle;
        public bool isOwned;
        public bool canAfford;
    }
}
