using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Save;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class EquipmentManager : Node
    {
        [Export] private PlayerStats _playerStats;

        private readonly Dictionary<EquipmentSlot, EquipmentItemData> _equippedItems = new();

        [Signal] public delegate void EquipmentChangedEventHandler(int slot, EquipmentItemData item);
        [Signal] public delegate void WeaponVisualChangedEventHandler(PackedScene weaponScene);

        public EquipmentItemData GetEquippedItem(EquipmentSlot slot)
        {
            return _equippedItems.ContainsKey(slot) ? _equippedItems[slot] : null;
        }

        public bool HasWeaponEquipped => _equippedItems.ContainsKey(EquipmentSlot.MainHand);

        public bool CanEquipItem(EquipmentItemData newItem)
        {
            if (newItem == null)
            {
                return false;
            }

            if (_playerStats.CurrentLevel < newItem.MinLevel)
            {
                GD.Print("Level not high enough!");
                return false;
            }

            return true;
        }

        public bool EquipItem(EquipmentItemData newItem)
        {
            if (!CanEquipItem(newItem))
            {
                return false;
            }

            if (_equippedItems.ContainsKey(newItem.SlotType))
            {
                UnequipItem(newItem.SlotType);
            }

            _equippedItems[newItem.SlotType] = newItem;
            GD.Print($"Equipped: {newItem.ItemName} into {newItem.SlotType}");

            _playerStats.RecalculateStats();
            EmitSignal(SignalName.EquipmentChanged, (int)newItem.SlotType, newItem);

            if (newItem.SlotType == EquipmentSlot.MainHand && newItem.WeaponScene != null)
            {
                EmitSignal(SignalName.WeaponVisualChanged, newItem.WeaponScene);
            }

            return true;
        }

        public EquipmentItemData UnequipItem(EquipmentSlot slot)
        {
            if (!_equippedItems.ContainsKey(slot))
            {
                return null;
            }

            var removedItem = _equippedItems[slot];
            _equippedItems.Remove(slot);

            GD.Print($"Unequipped: {removedItem.ItemName}");
            _playerStats.RecalculateStats();
            EmitSignal(SignalName.EquipmentChanged, (int)slot, default(Variant));

            if (slot == EquipmentSlot.MainHand)
            {
                EmitSignal(SignalName.WeaponVisualChanged, default(Variant));
            }

            return removedItem;
        }

        public int GetTotalAttributeBonus(AttributeType type)
        {
            int total = 0;
            foreach (var item in _equippedItems.Values)
            {
                if (item.AttributeBonuses.ContainsKey(type))
                {
                    total += item.AttributeBonuses[type];
                }
            }

            return total;
        }

        public float GetTotalBaseValue(EquipmentSlot slot)
        {
            if (_equippedItems.ContainsKey(slot))
            {
                return _equippedItems[slot].BaseValue;
            }

            return 0;
        }

        public List<EquippedItemSaveData> CaptureEquippedItems()
        {
            var result = new List<EquippedItemSaveData>();
            foreach (var pair in _equippedItems)
            {
                result.Add(new EquippedItemSaveData
                {
                    Slot = (int)pair.Key,
                    ResourcePath = pair.Value?.ResourcePath ?? string.Empty,
                    ItemId = pair.Value?.ID ?? string.Empty
                });
            }

            return result;
        }

        public void ClearAllEquipment()
        {
            var slots = new List<EquipmentSlot>(_equippedItems.Keys);
            foreach (var slot in slots)
            {
                UnequipItem(slot);
            }
        }

        public void RestoreEquipment(IEnumerable<EquippedItemSaveData> equippedItems)
        {
            ClearAllEquipment();

            if (equippedItems == null)
            {
                return;
            }

            foreach (var savedItem in equippedItems)
            {
                if (savedItem == null || string.IsNullOrEmpty(savedItem.ResourcePath))
                {
                    continue;
                }

                EquipmentItemData item = GD.Load<EquipmentItemData>(savedItem.ResourcePath);
                if (item == null)
                {
                    GD.PrintErr($"[Equipment] Failed to load equipped item: {savedItem.ResourcePath}");
                    continue;
                }

                EquipItem(item);
            }
        }
    }
}
