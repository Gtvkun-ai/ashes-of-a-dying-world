using Godot;

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
GD.Print("[Player] Weapon visual cleared.");
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
GD.Print("[Player] Weapon visual (and hitbox) loaded.");
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

public void EquipFromInventory(string itemId)
{
if (_inventory == null || _equipMgr == null) return;

var item = _inventory.GetItem(itemId);
if (item != null)
{
_equipMgr.EquipItem(item);
GD.Print($"[Player] Equipped {item.ItemName} from inventory.");
}
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
