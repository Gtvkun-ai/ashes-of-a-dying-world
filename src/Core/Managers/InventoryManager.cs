using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Core.Managers
{
    /// <summary>
    /// Quản lý túi đồ (Inventory) của player.
    /// AutoLoad hoặc add vào Player scene.
    /// </summary>
    public partial class InventoryManager : Node
    {
        [Signal] public delegate void InventoryChangedEventHandler();

        private List<EquipmentItemData> _items = new();

        [Export] public int MaxSlots { get; set; } = 20;

        public IReadOnlyList<EquipmentItemData> Items => _items;

        public override void _Ready()
        {
            // Thêm WoodSword mặc định vào túi đồ khi bắt đầu game
            var woodSword = GD.Load<EquipmentItemData>("res://assets/resources/data/weapons/sword/WoodSword.tres");
            if (woodSword != null)
            {
                AddItem(woodSword);
                GD.Print("[Inventory] WoodSword đã được thêm vào túi đồ.");
            }
            else
            {
                GD.PrintErr("[Inventory] Không tải được WoodSword.tres!");
            }
        }

        public void AddItem(EquipmentItemData item)
        {
            if (item == null) return;

            if (_items.Count >= MaxSlots)
            {
                GD.Print("[Inventory] Kho do da day, khong the them item moi.");
                return;
            }

            _items.Add(item);
            GD.Print($"[Inventory] +{item.ItemName}");
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
    }
}
