using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class MatchShopController : MonoBehaviour
    {
        private readonly List<MatchShopItemData> categoryItems = new();
        private MatchManager match;
        private BRParticipant player;
        private MatchCurrencyWallet wallet;
        private MatchEconomyConfig economy;
        private SafeZoneController safeZone;
        private AudioSource audioSource;
        private float nextPurchaseTime;
        private MatchShopItemData pendingConfirmation;
        private float confirmationExpiresAt;

        public event Action<MatchShopTerminal> OnShopOpened;
        public event Action OnShopClosed;
        public event Action<MatchShopItemData> OnPurchaseSucceeded;
        public event Action<MatchShopItemData, PurchaseResult> OnPurchaseFailed;

        public bool IsShopOpen => ActiveTerminal != null;
        public MatchShopTerminal ActiveTerminal { get; private set; }
        public MatchShopCategory ActiveCategory { get; private set; } = MatchShopCategory.Weapons;
        public IReadOnlyList<MatchShopItemData> CategoryItems => categoryItems;
        public PurchaseResult LastResult { get; private set; } = PurchaseResult.Success;
        public string LastFeedback { get; private set; }
        public float FeedbackVisibleUntil { get; private set; }

        public void Configure(MatchManager manager, BRParticipant localPlayer, MatchEconomyConfig settings,
            SafeZoneController zone)
        {
            CloseShop();
            match = manager;
            player = localPlayer;
            wallet = player != null ? player.GetComponent<MatchCurrencyWallet>() : null;
            economy = settings;
            safeZone = zone;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            economy?.Normalize();
        }

        public void Tick()
        {
            if (!IsShopOpen) return;
            if (match == null || match.State != MatchState.Active || player == null || !player.IsCombatCapable
                || !ActiveTerminal.Available
                || (ActiveTerminal.closeWhenPlayerMovesAway
                    && Vector3.Distance(player.transform.position, ActiveTerminal.InteractionPosition)
                    > ActiveTerminal.closeDistance)) CloseShop();
        }

        public BRInputFrame ApplyInputRestrictions(BRInputFrame input)
        {
            if (!IsShopOpen || economy == null) return input;
            return new BRInputFrame(
                economy.disableMovementWhileShopping ? Vector2.zero : input.Move,
                economy.disableCameraWhileShopping ? Vector2.zero : input.Look,
                !economy.disableMovementWhileShopping && input.Jump,
                !economy.disableMovementWhileShopping && input.Crouch,
                !economy.disableMovementWhileShopping && input.Sprint,
                !economy.disableAimWhileShopping && input.Aim,
                !economy.disableFireWhileShopping && input.Fire,
                !economy.disableFireWhileShopping && input.FirePressed,
                input.Reload,
                input.Interact,
                input.Heal,
                input.Swap,
                input.WeaponSlot,
                input.ShoulderSwap,
                input.Drop,
                input.InteractHeld,
                economy.disableCameraWhileShopping ? Vector2.zero : input.FireDragDelta,
                input.InputDevice);
        }

        public bool OpenShop(MatchShopTerminal terminal)
        {
            var result = ValidateTerminal(terminal, true);
            if (result != PurchaseResult.Success)
            {
                SetFeedback(null, result);
                return false;
            }
            ActiveTerminal = terminal;
            ShowCategory(FirstAvailableCategory(terminal.shopDatabase));
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            PlaySound(economy != null ? economy.shopOpenSound : null);
            OnShopOpened?.Invoke(terminal);
            return true;
        }

        public void CloseShop()
        {
            if (ActiveTerminal == null) return;
            ActiveTerminal = null;
            pendingConfirmation = null;
            categoryItems.Clear();
            if (match != null && match.State == MatchState.Active && !match.Paused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            PlaySound(economy != null ? economy.shopCloseSound : null);
            OnShopClosed?.Invoke();
        }

        public void ShowCategory(MatchShopCategory category)
        {
            ActiveCategory = category;
            pendingConfirmation = null;
            categoryItems.Clear();
            if (ActiveTerminal?.shopDatabase == null) return;
            categoryItems.AddRange(ActiveTerminal.shopDatabase.GetByCategory(category));
        }

        public PurchaseResult RequestPurchase(MatchShopItemData item)
            => RequestPurchase(item, Time.time);

        public PurchaseResult RequestPurchase(MatchShopItemData item, float requestTime)
        {
            var result = ValidatePurchase(item, requestTime);
            if (result != PurchaseResult.Success)
            {
                SetFeedback(item, result);
                return result;
            }
            if (item.requireConfirmation && !IsAwaitingConfirmation(item, requestTime))
            {
                pendingConfirmation = item;
                confirmationExpiresAt = requestTime + 3f;
                SetFeedback(item, PurchaseResult.ConfirmationRequired);
                return PurchaseResult.ConfirmationRequired;
            }
            pendingConfirmation = null;
            nextPurchaseTime = requestTime + (economy != null ? economy.purchaseRequestCooldown : 0.12f);
            var price = GetFinalPrice(item);
            if (!wallet.TrySpend(price))
            {
                SetFeedback(item, PurchaseResult.InsufficientCurrency);
                return PurchaseResult.InsufficientCurrency;
            }
            if (!ExecuteDelivery(item))
            {
                wallet.AddCurrency(price);
                SetFeedback(item, PurchaseResult.Failed);
                return PurchaseResult.Failed;
            }
            ActiveTerminal.CommitPurchase(item, player);
            LastResult = PurchaseResult.Success;
            LastFeedback = $"{item.displayName}  +{item.quantityGranted}";
            FeedbackVisibleUntil = Time.unscaledTime + 1.8f;
            PlaySound(economy != null ? economy.purchaseSuccessSound : null);
            OnPurchaseSucceeded?.Invoke(item);
            return PurchaseResult.Success;
        }

        public PurchaseResult ValidatePurchase(MatchShopItemData item, float requestTime)
        {
            var terminalResult = ValidateTerminal(ActiveTerminal, true);
            if (terminalResult != PurchaseResult.Success) return terminalResult;
            if (item == null || string.IsNullOrWhiteSpace(item.id)) return PurchaseResult.InvalidItem;
            if (!ActiveTerminal.shopDatabase.Contains(item)) return PurchaseResult.InvalidItem;
            if (!item.available) return PurchaseResult.ItemUnavailable;
            if (item.quantityGranted <= 0 || item.price < 0) return PurchaseResult.InvalidQuantity;
            if (requestTime < nextPurchaseTime) return PurchaseResult.Cooldown;
            if (!ActiveTerminal.HasStock(item, player)) return PurchaseResult.OutOfStock;
            var price = GetFinalPrice(item);
            if (wallet == null || !wallet.CanAfford(price)) return PurchaseResult.InsufficientCurrency;
            return CanReceive(item) ? PurchaseResult.Success : PurchaseResult.InventoryFull;
        }

        public int GetFinalPrice(MatchShopItemData item) => item != null ? Mathf.Max(0, item.price) : 0;

        public bool IsAwaitingConfirmation(MatchShopItemData item)
            => IsAwaitingConfirmation(item, Time.time);

        private bool IsAwaitingConfirmation(MatchShopItemData item, float requestTime)
            => item != null && pendingConfirmation == item && requestTime <= confirmationExpiresAt;

        private PurchaseResult ValidateTerminal(MatchShopTerminal terminal, bool requireRange)
        {
            if (match == null || match.State != MatchState.Active) return PurchaseResult.MatchUnavailable;
            if (player == null || !player.IsCombatCapable) return PurchaseResult.PlayerUnavailable;
            if (terminal == null || !terminal.Available) return PurchaseResult.ShopUnavailable;
            if (requireRange && !terminal.IsPlayerInRange(player)) return PurchaseResult.TooFarAway;
            if (!terminal.usableOutsideSafeZone && safeZone != null
                && !safeZone.IsInsideSafeZone(player.transform.position)) return PurchaseResult.OutsideSafeZone;
            return PurchaseResult.Success;
        }

        private bool CanReceive(MatchShopItemData item)
        {
            var inventory = player?.Inventory;
            var health = player?.Health;
            if (inventory == null || health == null) return false;
            var quantity = item.quantityGranted;
            return item.productType switch
            {
                MatchShopProductType.Weapon => ResolveWeapon(item) != null
                    && !OwnsWeapon(ResolveWeapon(item))
                    && (inventory.Weapons.Count < 2
                        || item.weaponPurchaseBehavior == WeaponPurchaseBehavior.ReplaceSelectedWeapon),
                MatchShopProductType.Ammo => inventory.CanAddAmmo(item.ammoKind, quantity, item.requireFullPackageSpace),
                MatchShopProductType.MedKit => inventory.CanAddMedKits(quantity, item.requireFullPackageSpace),
                MatchShopProductType.Vest => item.equipmentLevel > health.VestLevel,
                MatchShopProductType.Helmet => item.equipmentLevel > health.HelmetLevel,
                MatchShopProductType.GlooWall => inventory.CanAddGlooWalls(quantity, item.requireFullPackageSpace),
                MatchShopProductType.Grenade => inventory.CanAddGrenades(quantity, item.requireFullPackageSpace),
                MatchShopProductType.Stabilizer => inventory.StabilizerLevel < 3,
                _ => false
            };
        }

        private bool ExecuteDelivery(MatchShopItemData item)
        {
            var inventory = player.Inventory;
            var quantity = item.quantityGranted;
            switch (item.productType)
            {
                case MatchShopProductType.Weapon:
                    var weapon = ResolveWeapon(item);
                    if (weapon == null || !player.Weapon.EquipFromDeathLoot(weapon, weapon.magazineSize)) return false;
                    if (item.bonusAmmo > 0) inventory.AddAmmoQuantitative(weapon.ammoKind, item.bonusAmmo);
                    return true;
                case MatchShopProductType.Ammo:
                    return inventory.AddAmmoQuantitative(item.ammoKind, quantity).AcceptedAmount > 0;
                case MatchShopProductType.MedKit:
                    return inventory.AddMedKitsQuantitative(quantity).AcceptedAmount > 0;
                case MatchShopProductType.Vest:
                    player.Health.EquipVest(item.equipmentLevel);
                    return player.Health.VestLevel >= item.equipmentLevel;
                case MatchShopProductType.Helmet:
                    player.Health.EquipHelmet(item.equipmentLevel);
                    return player.Health.HelmetLevel >= item.equipmentLevel;
                case MatchShopProductType.GlooWall:
                    return inventory.AddGlooWallsQuantitative(quantity).AcceptedAmount > 0;
                case MatchShopProductType.Grenade:
                    return inventory.AddGrenadesQuantitative(quantity).AcceptedAmount > 0;
                case MatchShopProductType.Stabilizer:
                    return inventory.TryAddStabilizer(quantity);
                default:
                    return false;
            }
        }

        private WeaponDefinition ResolveWeapon(MatchShopItemData item)
        {
            if (item.weapon != null) return item.weapon;
            if (match?.WeaponCatalog == null || string.IsNullOrWhiteSpace(item.weaponId)) return null;
            foreach (var candidate in match.WeaponCatalog)
                if (candidate != null && string.Equals(candidate.displayName, item.weaponId,
                        StringComparison.OrdinalIgnoreCase)) return candidate;
            return null;
        }

        private bool OwnsWeapon(WeaponDefinition weapon)
        {
            if (weapon == null || player?.Inventory?.Weapons == null) return false;
            foreach (var owned in player.Inventory.Weapons)
                if (owned == weapon) return true;
            return false;
        }

        private void SetFeedback(MatchShopItemData item, PurchaseResult result)
        {
            LastResult = result;
            LastFeedback = ResultMessage(result);
            FeedbackVisibleUntil = Time.unscaledTime + 1.5f;
            PlaySound(economy != null ? economy.purchaseFailSound : null);
            OnPurchaseFailed?.Invoke(item, result);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }

        public static string ResultMessage(PurchaseResult result) => result switch
        {
            PurchaseResult.Success => "COMPRA REALIZADA",
            PurchaseResult.TooFarAway => "VOCE ESTA LONGE DEMAIS",
            PurchaseResult.OutOfStock => "ITEM ESGOTADO",
            PurchaseResult.InsufficientCurrency => "CREDITOS INSUFICIENTES",
            PurchaseResult.InventoryFull => "INVENTARIO CHEIO",
            PurchaseResult.ConfirmationRequired => "TOQUE NOVAMENTE PARA CONFIRMAR",
            PurchaseResult.OutsideSafeZone => "LOJA BLOQUEADA FORA DA SAFE",
            PurchaseResult.Cooldown => "AGUARDE",
            PurchaseResult.PlayerUnavailable => "JOGADOR INDISPONIVEL",
            PurchaseResult.MatchUnavailable => "LOJA FORA DA PARTIDA",
            _ => "COMPRA NAO AUTORIZADA"
        };

        private static MatchShopCategory FirstAvailableCategory(MatchShopDatabase database)
        {
            if (database?.items == null) return MatchShopCategory.Weapons;
            foreach (var item in database.items)
                if (item != null && item.available) return item.category;
            return MatchShopCategory.Weapons;
        }
    }
}
