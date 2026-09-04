using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class CharacterClothingManager : MonoBehaviour
    {
        [SerializeField] private Transform armatureRoot;
        [SerializeField] private Transform playerHips;
        [SerializeField] private Transform clothingContainer;
        [SerializeField] private CharacterBodyVisibility bodyVisibility;
        [SerializeField] private CharacterBoneMapper boneMapper;
        [SerializeField] private ClothingItemData[] defaultClothing = Array.Empty<ClothingItemData>();
        [SerializeField] private ClothingDatabase clothingDatabase;
        [SerializeField] private bool loadSavedClothes = true;
        [SerializeField] private bool requireOwnedItems = true;

        private readonly Dictionary<ClothingSlot, ClothingItemData> equippedItems = new();
        private readonly Dictionary<ClothingSlot, GameObject> equippedVisuals = new();
        private readonly List<ClothingItemData> equippedBuffer = new(6);
        private IClothingSaveSystem saveSystem;
        private bool initialized;

        public event Action<ClothingItemData> OnItemEquipped;
        public event Action<ClothingSlot> OnItemUnequipped;
        public event Action OnAppearanceChanged;

        public IReadOnlyDictionary<ClothingSlot, ClothingItemData> EquippedItems => equippedItems;
        public ClothingDatabase Database => clothingDatabase;
        public bool Initialized => initialized;

        private IEnumerator Start()
        {
            // CharacterVisualAnimator normalizes and grounds the base model before clothing contributes bounds.
            yield return null;
            Initialize();
            if (loadSavedClothes) LoadEquippedClothes();
            else EquipDefaultClothes();
        }

        public void ConfigureRuntime(Transform root, Transform container, ClothingDatabase database = null,
            CharacterBodyVisibility visibility = null, IClothingSaveSystem persistence = null)
        {
            armatureRoot = root;
            clothingContainer = container;
            if (database != null) clothingDatabase = database;
            if (visibility != null) bodyVisibility = visibility;
            if (persistence != null) saveSystem = persistence;
            initialized = false;
            Initialize();
        }

        public void Initialize()
        {
            if (initialized) return;
            if (armatureRoot == null) armatureRoot = FindAnimatorRoot();
            if (clothingContainer == null && armatureRoot != null)
            {
                var containerObject = new GameObject("Clothes");
                clothingContainer = containerObject.transform;
                clothingContainer.SetParent(armatureRoot, false);
            }
            if (boneMapper == null) boneMapper = GetComponent<CharacterBoneMapper>() ?? gameObject.AddComponent<CharacterBoneMapper>();
            boneMapper.Configure(armatureRoot, playerHips);
            playerHips = boneMapper.Hips;
            if (bodyVisibility == null) bodyVisibility = GetComponent<CharacterBodyVisibility>() ?? gameObject.AddComponent<CharacterBodyVisibility>();
            if (clothingDatabase == null) clothingDatabase = Resources.Load<ClothingDatabase>("Wardrobe/ClothingDatabase");
            saveSystem ??= new PlayerPrefsClothingSaveSystem();
            initialized = armatureRoot != null && clothingContainer != null && boneMapper.BoneCount > 0;
            if (!initialized) Debug.LogError($"CharacterClothingManager on '{name}' requires a valid armature and clothing container.", this);
        }

        public bool Equip(ClothingItemData item)
        {
            Initialize();
            if (!initialized || item == null) return false;
            if (requireOwnedItems && !IsOwned(item)) return false;
            if (!item.IsValid(out var reason))
            {
                Debug.LogWarning($"Cannot equip wardrobe item: {reason}", item);
                return false;
            }
            if (IsEquipped(item)) return true;

            var visual = Instantiate(item.ClothingPrefab, clothingContainer, false);
            visual.name = $"{item.Slot} - {item.DisplayName}";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;
            var animators = visual.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++) animators[i].enabled = false;
            var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"Wardrobe item '{item.Id}' contains no SkinnedMeshRenderer.", item);
                DestroyObject(visual);
                return false;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                if (!boneMapper.TryRemap(renderers[i], item.Id))
                {
                    DestroyObject(visual);
                    return false;
                }
                if (i == 0 && item.Mesh != null) renderers[i].sharedMesh = item.Mesh;
                if (i == 0 && item.Materials.Length > 0) renderers[i].sharedMaterials = item.Materials;
            }

            RemoveVisual(item.Slot);
            equippedVisuals[item.Slot] = visual;
            equippedItems[item.Slot] = item;
            saveSystem.Save(item.Slot, item.Id);
            RefreshCharacterAppearance();
            OnItemEquipped?.Invoke(item);
            return true;
        }

        public bool Unequip(ClothingSlot slot)
        {
            Initialize();
            var hadItem = equippedItems.Remove(slot);
            var hadVisual = RemoveVisual(slot);
            if (!hadItem && !hadVisual) return false;
            saveSystem.Delete(slot);
            RefreshCharacterAppearance();
            OnItemUnequipped?.Invoke(slot);
            return true;
        }

        public ClothingItemData GetEquippedItem(ClothingSlot slot) =>
            equippedItems.TryGetValue(slot, out var item) ? item : null;

        public bool IsEquipped(ClothingItemData item) => item != null
            && equippedItems.TryGetValue(item.Slot, out var equipped)
            && equipped != null && string.Equals(equipped.Id, item.Id, StringComparison.OrdinalIgnoreCase);

        public void RefreshCharacterAppearance()
        {
            RefreshHiddenBodyParts();
            OnAppearanceChanged?.Invoke();
        }

        public void RefreshHiddenBodyParts()
        {
            equippedBuffer.Clear();
            foreach (var entry in equippedItems) equippedBuffer.Add(entry.Value);
            bodyVisibility?.RefreshHiddenBodyParts(equippedBuffer);
        }

        public void EquipDefaultClothes()
        {
            Initialize();
            var slots = (ClothingSlot[])Enum.GetValues(typeof(ClothingSlot));
            for (var i = 0; i < slots.Length; i++)
            {
                var defaultItem = FindInspectorDefault(slots[i]);
                if (defaultItem == null && clothingDatabase != null) defaultItem = clothingDatabase.GetDefault(slots[i]);
                if (defaultItem != null) Equip(defaultItem);
            }
        }

        public void LoadEquippedClothes()
        {
            Initialize();
            if (!initialized) return;
            var slots = (ClothingSlot[])Enum.GetValues(typeof(ClothingSlot));
            for (var i = 0; i < slots.Length; i++)
            {
                ClothingItemData item = null;
                if (saveSystem.TryLoad(slots[i], out var itemId) && clothingDatabase != null)
                    item = clothingDatabase.GetById(itemId);
                item ??= FindInspectorDefault(slots[i]);
                if (item == null && clothingDatabase != null) item = clothingDatabase.GetDefault(slots[i]);
                if (item != null) Equip(item);
            }
        }

        public bool EquipBundle(OutfitBundleData bundle)
        {
            if (bundle == null) return false;
            var items = bundle.Items;
            for (var i = 0; i < items.Length; i++)
                if (items[i] == null || !items[i].IsValid(out _) || requireOwnedItems && !IsOwned(items[i])) return false;
            var success = true;
            for (var i = 0; i < items.Length; i++) success &= Equip(items[i]);
            return success;
        }

        public void SetSaveSystem(IClothingSaveSystem persistence) => saveSystem = persistence;

        public bool IsOwned(ClothingItemData item) => item != null && (item.Owned
            || PlayerProfileService.Current?.Rewards?.Owns(item.Id) == true);

        private Transform FindAnimatorRoot()
        {
            var animator = GetComponentInChildren<Animator>(true);
            return animator != null ? animator.transform : null;
        }

        private ClothingItemData FindInspectorDefault(ClothingSlot slot)
        {
            for (var i = 0; i < defaultClothing.Length; i++)
                if (defaultClothing[i] != null && defaultClothing[i].Slot == slot) return defaultClothing[i];
            return null;
        }

        private bool RemoveVisual(ClothingSlot slot)
        {
            if (!equippedVisuals.TryGetValue(slot, out var visual)) return false;
            equippedVisuals.Remove(slot);
            DestroyObject(visual);
            return true;
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
