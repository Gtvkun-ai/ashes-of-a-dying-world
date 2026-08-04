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
        private const string StarterWeaponPath = "res://data/weapons/sword/wood_sword.tres";

        [Signal] public delegate void InventoryChangedEventHandler();

        private readonly List<EquipmentItemData> _items = new();

        // Đồng bộ với InventoryPanel: grid mặc định 8 cột x 5 hàng = 40 ô.
        // Nếu scene ghi đè giá trị này trong Inspector thì giá trị của scene vẫn được ưu tiên.
        [Export] public int MaxSlots { get; set; } = 40;

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
            }
            else
            {
                GD.PrintErr("[Inventory] Failed to load starter weapon.");
            }
        }

        public bool AddItem(EquipmentItemData item)
        {
            return AddLoadedItem(item, true);
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
                EmitSignal(SignalName.InventoryChanged);
                return true;
            }

            return false;
        }

        public bool CanAddItem(EquipmentItemData item)
        {
            return item != null && _items.Count < MaxSlots;
        }

        public bool HasItem(string itemId)
        {
            return _items.Exists(i => i.ID == itemId);
        }

        public EquipmentItemData GetItem(string itemId)
        {
            return _items.Find(i => i.ID == itemId);
        }

        private bool AddLoadedItem(EquipmentItemData item, bool emitSignal)
        {
            if (item == null)
            {
                return false;
            }

            if (_items.Count >= MaxSlots)
            {
                return false;
            }

            _items.Add(item);
            if (emitSignal)
            {
                EmitSignal(SignalName.InventoryChanged);
            }

            return true;
        }
    }
}
