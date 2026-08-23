# Environment Rendering V5.0 — Clean Runtime Foundation

## Mục tiêu

V5.0 không cố thêm một lớp hiệu ứng mới lên V4.5. Mục tiêu là **cắt bỏ đường runtime gây nợ kiến trúc** trước khi tiếp tục art pass.

V5.0 giữ nguyên:
- `WorldClock`
- `CelestialSolver`
- `EnvironmentState`
- weather/day-night
- `WorldLighting2D`
- atmosphere + color-grade hiện có

V5.0 thay hai đường quan trọng:

1. **Environment GPU state**
   - Bỏ runtime `EnvironmentMaterialBus` khỏi `EnvironmentBinder2D`.
   - Không scan 500+ `ShaderMaterial` mỗi vài giây.
   - `WorldEnvironmentService` gọi `ShaderGlobalBridge.Push(CurrentState)`.
   - 19 environment values được publish bằng Godot global shader uniforms.
   - 11 world shader chính đã chuyển `env_*` từ local `uniform` sang `global uniform`.

2. **Shadow runtime**
   - Bỏ `ShadowRenderer2D` khỏi runtime.
   - Bỏ `projected_shadow_v2.gdshader` khỏi runtime tree/actor/rock shadow.
   - Bỏ `EnvironmentMassShadow2D` khỏi runtime.
   - Không còn `cluster_mass` / `border_mass` heuristic.
   - Mỗi vật thể có một footprint Sprite2D thật nằm trên mặt đất.
   - Cây đứng gần nhau tự overlap alpha, nên tự tạo mass shadow.

## File mới

- `scripts/World/Environment/EnvironmentShadowSystem2D.cs`
- `assets/graphics/environment/shadows/v5/tree_footprint_v5.png`
- `assets/graphics/environment/shadows/v5/apple_tree_footprint_v5.png`
- `assets/graphics/environment/shadows/v5/soft_footprint_v5.png`
- `assets/graphics/environment/shadows/v5/contact_ao_v5.png`

## Ground anchor cây

Tree asset hiện bị split thành canopy + trunk. Vì vậy đáy alpha của canopy không phải chân cây.
V5.0 sửa offset scene để ground anchor bám đáy trunk:

- tree thường: `GroundOffset = Vector2(0, 61)`
- apple tree: `GroundOffset = Vector2(0, 193)` (trước khi scale 0.3 của root)

Đây là data tạm cho asset hiện tại. V5.1 khi thay native-resolution tree asset thì anchor sẽ được author lại sạch hơn.

## Runtime log mong đợi

Khi chạy Field 1, cần thấy:

```text
[ShaderGlobalBridge] READY V5.0 | globals=19 | material_scan=OFF
[EnvironmentBinder2D] READY V5.0 | gpu=global_uniforms | material_scan=OFF | shadow=ground_footprint | mass_shadow=OFF
[EnvironmentShadowSystem2D] READY V5.0 | footprint_casters=... | shader_projection=OFF | mass_heuristic=OFF
```

Không nên còn log kiểu:

```text
BOUND materials=515
EnvironmentMassShadow2D ... cluster_mass=...
ShadowRenderer2D READY V4...
```

## Test hình ảnh

Giữ cùng camera và test:

- 06:00
- 12:00
- 18:00
- 00:00

V5.0 là foundation pass, không phải final art pass. Tiêu chí chính:
- bóng không biến dạng hình học;
- bóng sáng/chiều đổi hướng đúng;
- bóng của cụm cây tự overlap mà không cần mass manager;
- cây/actor/rock vẫn bám đất;
- không có shader compile error do global uniform.

## Rollback

V4.5 source/file cũ **không bị xóa**. Các class V4 như `EnvironmentMaterialBus`, `EnvironmentMassShadow2D`, `ShadowRenderer2D` vẫn còn trong source để rollback/diff, nhưng V5 binder không khởi tạo hay gọi chúng.
