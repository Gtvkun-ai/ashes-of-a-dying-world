# Ashes Shadow Core V2

## Vì sao refactor
V1.4 -> V1.6 đã chứng minh được hướng visual, nhưng runtime bắt đầu có mùi patch chồng patch:

- mỗi caster có một ShaderMaterial riêng;
- EnvironmentShadowBus scan scene rồi update từng caster;
- `KeyLightDirection` bị dùng lẫn cho cả light ray và shadow direction;
- hàng loạt số tuning nằm trực tiếp trong từng `.tscn`.

V2 tách đúng trách nhiệm.

## Luồng runtime

```text
WorldClock
    -> CelestialSolver
        -> EnvironmentState.ShadowDirection2D
        -> EnvironmentState.ShadowLength01

EnvironmentBinder2D
    -> ShadowRenderer2D
        -> ONE shared ShaderMaterial

ShadowCaster2D
    -> texture/source thật
    -> ShadowCasterProfile
    -> instance uniforms (setup-only)
```

## Quy tắc quan trọng

### 1. `ShadowDirection2D` có nghĩa duy nhất
Đây là **hướng bóng chạy trên mặt đất**.

- sáng: X dương;
- trưa: X gần 0;
- chiều: X âm;
- moon có quỹ đạo riêng.

Renderer/shader không negate hoặc đoán lại nghĩa của vector.

### 2. Một shared material
`res://assets/materials/world/projected_shadow_shared.tres`

Mọi shadow proxy đều dùng cùng resource này. Per-frame chỉ set 4 uniform chung:

- `shadow_direction`
- `shadow_length01`
- `shadow_strength`
- `shadow_night_factor`

### 3. Per-caster dùng `instance uniform`
Caster chỉ set khi spawn/frame nguồn đổi:

- projection model
- base Y
- height
- noon/max length
- flatten
- tint/opacity/cutoff
- source/canvas bounds

Hiện shader dùng 13 instance uniform, còn dưới practical limit 16 của Godot.

Không có material-per-object.

### 4. Profile thay cho số rải trong scene
Các resource nằm ở:

`res://data/world/shadows/`

Ví dụ:

- `tree_tall.tres`
- `apple_tree.tres`
- `small_flora.tres`
- `rock_small.tres`
- `rock_medium.tres`
- `rock_large.tres`
- `character.tres`
- `slime.tres`
- `cliff_wall.tres`

Scene chủ yếu chỉ còn:

```text
SourcePath
Profile
TextureOverride (nếu cần)
GroundOffset (nếu cần)
```

### 5. Không polling caster
`ShadowCaster2D` không `_Process`.

AnimatedSprite2D dùng signal `FrameChanged` để shadow đổi frame. Player Body dựng deferred được bắt bằng `ChildEnteredTree` của parent.

## Projection models

- `Upright`: cây, người, hoa, cỏ.
- `Volume`: đá/slime, bóng thấp và đặc hơn.
- `RigidDrop`: cliff/wall/layer lớn.

Shader vẫn lấy alpha từ asset thật, không dùng ellipse/blob generic.

Với Upright/Volume, phép chiếu dùng **mirror + shear**: silhouette được lật xuống dưới chân vật trước, rồi phần càng cao càng bị kéo xa theo `ShadowDirection2D`. Cách này tránh tình trạng phép flatten và projection triệt tiêu nhau như V1.6.

## Debug
`Ctrl+F9` đổi 06:00 / 12:00 / 18:00 / 00:00.

Output giờ in thêm:

```text
shadowDir=(x,y) shadowLen=...
```

06:00 và 18:00 phải có dấu X ngược nhau.
