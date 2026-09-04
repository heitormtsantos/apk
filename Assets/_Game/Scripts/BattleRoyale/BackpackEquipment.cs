using System;
using UnityEngine;

namespace BattleRoyale
{
    public enum BackpackEquipResult
    {
        Equipped,
        SameOrLowerLevel,
        InvalidItem,
        InventoryUnavailable
    }

    public sealed class BackpackEquipment : MonoBehaviour
    {
        private BackpackData current;
        private GameObject visualInstance;
        private Transform socket;

        public event Action<BackpackData, BackpackData> Changed;
        public BackpackData Current => current;
        public int CurrentLevel => current != null ? current.level : 0;
        public float AdditionalCapacity => current != null ? Mathf.Max(0f, current.additionalCapacity) : 0f;
        public Transform Socket => EnsureSocket();

        public BackpackEquipResult TryEquipCollectedItem(BackpackData item, out BackpackData replaced)
        {
            replaced = null;
            if (item == null || item.level is < 1 or > 3) return BackpackEquipResult.InvalidItem;
            var inventory = GetComponent<InventorySystem>();
            if (inventory == null || !inventory.CanUseInventory) return BackpackEquipResult.InventoryUnavailable;
            if (item.level <= CurrentLevel) return BackpackEquipResult.SameOrLowerLevel;

            replaced = current;
            current = item;
            RefreshVisual();
            inventory.NotifyEquipmentChanged();
            Changed?.Invoke(replaced, current);
            return BackpackEquipResult.Equipped;
        }

        public bool CanRemove()
        {
            var inventory = GetComponent<InventorySystem>();
            return current != null && current.canDrop && inventory != null
                && inventory.CurrentUsage <= inventory.BaseCapacity + 0.001f;
        }

        public BackpackData RemoveForDrop(bool enforceCapacity = true)
        {
            if (current == null || (enforceCapacity && !CanRemove())) return null;
            var removed = current;
            current = null;
            ReleaseVisual();
            GetComponent<InventorySystem>()?.NotifyEquipmentChanged();
            Changed?.Invoke(removed, null);
            return removed;
        }

        public BackpackData RemoveOnDeath() => RemoveForDrop(false);

        private Transform EnsureSocket()
        {
            if (socket != null) return socket;
            var anchor = FindBackAnchor(transform) ?? transform;
            var existing = anchor.Find("BackpackSocket");
            if (existing != null) socket = existing;
            else
            {
                var objectSocket = new GameObject("BackpackSocket");
                socket = objectSocket.transform;
                socket.SetParent(anchor, false);
            }
            socket.localPosition = anchor == transform ? new Vector3(0f, 1.02f, -0.11f) : new Vector3(0f, -0.035f, -0.09f);
            socket.localRotation = Quaternion.identity;
            socket.localScale = Vector3.one;
            return socket;
        }

        private static Transform FindBackAnchor(Transform root)
        {
            Transform spine = null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                var name = child.name.ToLowerInvariant();
                if (name.Contains("chest") || name.Contains("spine2") || name.Contains("spine_02")) return child;
                if (spine == null && name.Contains("spine")) spine = child;
            }
            return spine;
        }

        private void RefreshVisual()
        {
            ReleaseVisual();
            if (current == null || current.equippedVisualPrefab == null) return;
            visualInstance = Instantiate(current.equippedVisualPrefab, EnsureSocket(), false);
            visualInstance.name = $"Equipped {current.displayName}";
            visualInstance.transform.localPosition = new Vector3(0f, -0.035f, 0f);
            visualInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var collider in visualInstance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        private void ReleaseVisual()
        {
            if (visualInstance == null) return;
            if (Application.isPlaying) Destroy(visualInstance);
            else DestroyImmediate(visualInstance);
            visualInstance = null;
        }
    }
}
