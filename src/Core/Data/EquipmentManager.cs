using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Entities.Player;
namespace AshesofaDyingWorld.Core.Managers
{
    public partial class EquipmentManager : Node
    {
        //Tham chiếu ngược lên PlayerStats để báo cáo thay đổi chỉ số
        [Export] private PlayerStats _playerStats;

        //Lưu trữ các món đồ đang mặc
        private Dictionary<EquipmentSlot, EquipmentItemData> _equippedItems = new();

        //Sự kiện UI để lắng nghe ( khi mặc đồ thì UI đổi hình)
        [Signal] public delegate void EquipmentChangedEventHandler(int slot, EquipmentItemData item);
        
        // Signal khi vũ khí thay đổi (để Player cập nhật sprite)
        [Signal] public delegate void WeaponVisualChangedEventHandler(PackedScene weaponScene);

        /// <summary>
        /// Lấy item đang trang bị ở slot
        /// </summary>
        public EquipmentItemData GetEquippedItem(EquipmentSlot slot)
        {
            return _equippedItems.ContainsKey(slot) ? _equippedItems[slot] : null;
        }

        /// <summary>
        /// Kiểm tra có vũ khí đang trang bị không
        /// </summary>
        public bool HasWeaponEquipped => _equippedItems.ContainsKey(EquipmentSlot.MainHand);

        //hàm mặc đồ
        public void EquipItem(EquipmentItemData newItem)
        {
            if(newItem == null)
                return;

            // Kiểm tra điều kiện (Level, Class...)
            if (_playerStats.CurrentLevel < newItem.MinLevel)
            {
                GD.Print("Level not high enough!");
                return;
            }        
            
            // Nếu ô đó đang có đồ, tháo ra trước (Swap)
            if (_equippedItems.ContainsKey(newItem.SlotType))
            {
                UnequipItem(newItem.SlotType); 
                // Logic: Trả đồ cũ về Inventory ở đây (bạn tự implement)
            }

            // Mặc đồ mới
            _equippedItems[newItem.SlotType] = newItem;
            GD.Print($"Equipped: {newItem.ItemName} into {newItem.SlotType}");
            
            // Báo cho PlayerStats tính lại chỉ số
            _playerStats.RecalculateStats();
            
            // Bắn signal cho UI
            EmitSignal(SignalName.EquipmentChanged, (int)newItem.SlotType, newItem);

            // Nếu là vũ khí, bắn signal để Player cập nhật sprite
            if (newItem.SlotType == EquipmentSlot.MainHand && newItem.WeaponScene != null)
            {
                EmitSignal(SignalName.WeaponVisualChanged, newItem.WeaponScene);
            }
        }

        //hàm tháo đồ
        public void UnequipItem(EquipmentSlot slot){
            if (_equippedItems.ContainsKey(slot))
            {
                var removedItem = _equippedItems[slot];
                _equippedItems.Remove(slot);
                
                GD.Print($"Unequipped: {removedItem.ItemName}");
                _playerStats.RecalculateStats();
                
                // Bắn signal cho UI
                EmitSignal(SignalName.EquipmentChanged, (int)slot, default(Variant));

                // Nếu là vũ khí, xóa visual
                if (slot == EquipmentSlot.MainHand)
                {
                    EmitSignal(SignalName.WeaponVisualChanged, default(Variant));
                }
            }
            
        }
        // Helper để PlayerStats lấy tổng chỉ số cộng thêm
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

        // Helper lấy tổng giáp/dmg cơ bản
        public float GetTotalBaseValue(EquipmentSlot slot)
        {
            if (_equippedItems.ContainsKey(slot))
                return _equippedItems[slot].BaseValue;
            return 0;
        }
    }
}