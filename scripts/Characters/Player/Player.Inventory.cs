using Godot;
using AshesofaDyingWorld.Core.Data;

public partial class Player
{
    public bool EquipFromInventory(string itemId)
    {
        if (_inventory == null || Equipment == null)
        {
            return false;
        }

        EquipmentItemData item = _inventory.GetItem(itemId);
        if (item == null || !Equipment.CanEquipItem(item))
        {
            return false;
        }

        EquipmentItemData previous = Equipment.GetEquippedItem(item.SlotType);
        if (!_inventory.RemoveItem(item))
        {
            return false;
        }

        if (previous != null)
        {
            EquipmentItemData removed = Equipment.UnequipItem(item.SlotType);
            if (removed != null && !_inventory.AddItem(removed))
            {
                Equipment.EquipItem(removed);
                _inventory.AddItem(item);
                return false;
            }
        }

        if (Equipment.EquipItem(item))
        {
            return true;
        }

        if (previous != null)
        {
            _inventory.RemoveItem(previous);
            Equipment.EquipItem(previous);
        }
        _inventory.AddItem(item);
        return false;
    }

    public bool UnequipToInventory(EquipmentSlot slotType)
    {
        if (_inventory == null || Equipment == null)
        {
            return false;
        }

        EquipmentItemData equipped = Equipment.GetEquippedItem(slotType);
        if (equipped == null || !_inventory.CanAddItem(equipped))
        {
            return false;
        }

        EquipmentItemData removed = Equipment.UnequipItem(slotType);
        if (removed == null)
        {
            return false;
        }

        if (_inventory.AddItem(removed))
        {
            return true;
        }

        Equipment.EquipItem(removed);
        return false;
    }

    public void AutoEquipStarterWeapon()
    {
        CallDeferred(nameof(DoAutoEquipStarterWeapon));
    }

    private void DoAutoEquipStarterWeapon()
    {
        EquipFromInventory("weapon_wood_sword");
    }
}
