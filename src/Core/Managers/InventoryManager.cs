using Godot;
using System.Collections.Generic;
using System.Linq;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Core.Managers
{
    /// <summary>
    /// Quan ly tui do (Inventory) cua player.
    /// AutoLoad hoac add vao Player scene.
    /// </summary>
    public partial class InventoryManager : Node
    {
        private const string StarterWeaponPath = "res://assets/resources/data/weapons/sword/WoodSword.tres";

        [Signal] public delegate void InventoryChangedEventHandler();

        private readonly List<EquipmentItemData> _items = new();

        [Export] public int MaxSlots { get; set; } = 20;

        public IReadOnlyList<EquipmentItemData> Items => _items;

        public override void _Ready()
        {
            if (_items.Count > 0)
            {
                return;
            }

            var woodSword = GD.Load<EquipmentItemData>(StarterWeaponPath);
            if (woodSword != null)
            {
                AddItem(woodSword);
                GD.Print("[Inventory] Starter weapon added.");
            }
            else
            {
                GD.PrintErr("[Inventory] Failed to load starter weapon.");
            }
        }

        public void AddItem(EquipmentItemData item)
        {
            AddLoadedItem(item, true);
        }

        public void ClearItems(bool emitSignal = true)
        {
            _items.Clear();
            if (emitSignal)
            {
                EmitSignal(SignalName.InventoryChanged);
            }
        }

        public List<string> GetItemResourcePaths()
        {
            return _items
                .Where(item => item != null && !string.IsNullOrEmpty(item.ResourcePath))
                .Select(item => item.ResourcePath)
                .ToList();
        }

        public void RestoreItems(IEnumerable<string> resourcePaths)
        {
            ClearItems(false);

            if (resourcePaths != null)
            {
                foreach (string resourcePath in resourcePaths)
                {
                    if (string.IsNullOrEmpty(resourcePath))
                    {
                        continue;
                    }

                    EquipmentItemData item = GD.Load<EquipmentItemData>(resourcePath);
                    if (item == null)
                    {
                        GD.PrintErr($"[Inventory] Failed to load item from save: {resourcePath}");
                        continue;
                    }

                    AddLoadedItem(item, false);
                }
            }

            EmitSignal(SignalName.InventoryChanged);
        }

        public bool RemoveItem(EquipmentItemData item)
        {
            if (_items.Remove(item))
            {
                GD.Print($"[Inventory] -{item.ItemName}");
                EmitSignal(SignalName.InventoryChanged);
                return true;
            }

            return false;
        }

        public bool HasItem(string itemId)
        {
            return _items.Exists(i => i.ID == itemId);
        }

        public EquipmentItemData GetItem(string itemId)
        {
            return _items.Find(i => i.ID == itemId);
        }

        private void AddLoadedItem(EquipmentItemData item, bool emitSignal)
        {
            if (item == null)
            {
                return;
            }

            if (_items.Count >= MaxSlots)
            {
                GD.Print("[Inventory] Inventory is full.");
                return;
            }

            _items.Add(item);
            GD.Print($"[Inventory] +{item.ItemName}");
            if (emitSignal)
            {
                EmitSignal(SignalName.InventoryChanged);
            }
        }
    }
}
