using Godot;
using AshesofaDyingWorld.Core.Data;

public partial class Player
{
	private void OnWeaponVisualChanged(PackedScene weaponScene)
	{
		if (_weaponSprite == null) return;

		// Xóa hitbox cũ (nếu có)
		if (_hitbox != null)
		{
			_hitbox.QueueFree();
			_hitbox = null;
		}

		if (weaponScene == null)
		{
			_weaponSprite.SpriteFrames = null;
			_weaponSprite.Visible = false;
			SetHitboxActive(false);
			return;
		}

		Node weaponInstance = weaponScene.Instantiate();
		if (weaponInstance is AnimatedSprite2D spriteSource)
		{
			_weaponSprite.SpriteFrames = spriteSource.SpriteFrames;
			_weaponSprite.Visible = false;

			// Tìm Hitbox trong weapon scene (nếu có) và gắn sang WeaponSprite
			Area2D newHitbox = weaponInstance.GetNodeOrNull<Area2D>("Hitbox");
			if (newHitbox != null && _weaponSprite != null)
			{
				newHitbox.GetParent()?.RemoveChild(newHitbox);
				_weaponSprite.AddChild(newHitbox);
				_hitbox = newHitbox;
				SetHitboxActive(false);
			}

			weaponInstance.QueueFree();
		}
	}

	public bool IsAttackHitboxActive()
	{
		return _isAttacking && _hitbox != null && _hitbox.Monitoring && _hitbox.Monitorable;
	}

	private void SetHitboxActive(bool active)
	{
		if (_hitbox == null) return;

		_hitbox.Monitoring = active;
		_hitbox.Monitorable = active;

		var shape = _hitbox.GetNodeOrNull<CollisionShape2D>("HitboxShape");
		if (shape != null)
		{
			shape.Disabled = !active;
		}
	}

	public bool EquipFromInventory(string itemId)
	{
		if (_inventory == null || _equipMgr == null) return false;

		var item = _inventory.GetItem(itemId);
		if (item == null)
		{
			return false;
		}

		if (!_equipMgr.CanEquipItem(item))
		{
			return false;
		}

		var previouslyEquipped = _equipMgr.GetEquippedItem(item.SlotType); // lưu đồ đang được mặc
		if (!_inventory.RemoveItem(item)) 
		{
			return false;
		}

		if (previouslyEquipped != null)
		{
			var removedItem = _equipMgr.UnequipItem(item.SlotType); // bỏ đồ đang mặc ra khỏi slot
			if (removedItem != null && !_inventory.AddItem(removedItem))
			{
				GD.PrintErr($"[Player] Failed to return {removedItem.ItemName} to inventory.");
				_equipMgr.EquipItem(removedItem);
				_inventory.AddItem(item);
				return false;
			}
		}

		if (!_equipMgr.EquipItem(item))
		{
			GD.PrintErr($"[Player] Failed to equip {item.ItemName} from inventory.");

			if (previouslyEquipped != null) // nếu equip thất bại
			{
				_inventory.RemoveItem(previouslyEquipped); // xoá đồ đang mặc
				_equipMgr.EquipItem(previouslyEquipped); // mặc lại đồ cũ
			}

			_inventory.AddItem(item); // trả item về inventory
			return false;
		}

		return true;
	}

	public bool UnequipToInventory(EquipmentSlot slotType)
	{
		if (_inventory == null || _equipMgr == null) return false;

		var equippedItem = _equipMgr.GetEquippedItem(slotType);
		if (equippedItem == null)
		{
			return false;
		}

		if (!_inventory.CanAddItem(equippedItem))
		{
			return false;
		}

		var removedItem = _equipMgr.UnequipItem(slotType);
		if (removedItem == null)
		{
			return false;
		}

		if (!_inventory.AddItem(removedItem))
		{
			GD.PrintErr($"[Player] Failed to add {removedItem.ItemName} back to inventory.");
			_equipMgr.EquipItem(removedItem);
			return false;
		}

		return true;
	}

	public void AutoEquipStarterWeapon()
	{
		CallDeferred(nameof(DoAutoEquip));
	}

	private void DoAutoEquip()
	{
		EquipFromInventory("weapon_wood_sword");
	}
}
