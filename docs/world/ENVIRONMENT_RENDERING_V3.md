# Environment Rendering V3 — Whispering Fields

Mục tiêu của V3 là chuyển Field 1 từ "ảnh map + filter nhẹ" sang pipeline có form/lighting rõ ràng nhưng vẫn giữ pixel-art gốc.

## Thay đổi chính

- Shadow Core V3:
  - Tree/apple tree dùng **dedicated shadow mask**, không lấy artwork màu rồi ép xuống đất.
  - Bóng không còn biến mất ở bình minh/hoàng hôn vì key-light thấp.
  - Max cast distance được scale lại theo kích thước thật của map.
  - Thêm **contact shadow/AO sát chân** cho tree, rock, actor, slime.
- Foliage V3:
  - Sinh pseudo-normal map từ canopy hiện có.
  - Tán cây phản ứng với hướng mặt trời, có warm highlight / cool shade nhẹ.
  - Wind vẫn giữ độc lập với lighting.
- Rock relighting:
  - Suy pseudo-normal từ luminance của chính atlas, không cần sửa asset gốc.
- Cliff pass:
  - Cliff top / wall / stairs có response khác nhau.
  - Có contact AO ở chân cliff để height break đọc rõ hơn.
- Water V2:
  - `10_puddle_depth_mask.png` không còn placeholder đen; mask được sinh từ khoảng cách tới shoreline.
  - Có shallow/deep tint, shoreline highlight, oriented glint, rain/ripple nhưng không warp silhouette.
- Ground:
  - Macro variation map-locked, không procedural micro-noise.
- Cloud shadow:
  - Field 1 dùng world-space Polygon2D nên pattern bám map, không bám camera.
  - Screen-space cloud cũ tự tắt nếu tìm thấy `WorldCloudShadow`.
- Color grade:
  - Bật finishing pass nhẹ để gom palette sau lighting.

## Những giá trị nên test đầu tiên trong Godot

1. Test 06:30 / 09:00 / 12:00 / 16:30 / 18:15.
2. Quan sát tree shadow có đổi hướng sang hai phía khác nhau trước/sau trưa không.
3. Quan sát chân tree/rock/player có contact AO ổn định không.
4. Quan sát tán cây: highlight phải đổi bên theo sun direction nhưng không được bóng như nhựa.
5. Quan sát cliff/stairs: top sáng hơn wall, chân wall có AO nhưng không thành viền đen.
6. Quan sát pond: bờ sáng nhẹ, giữa sâu hơn, glint chạy nhưng mép nước đứng yên.

## Knob chính nếu cần tune sau screenshot đầu tiên

- `data/world/environment/whispering_fields.tres`
  - `DayAmbientStrength`
  - `SunLightEnergy`
  - `ShadowStrength`
  - `WaterShimmerStrength`
- `data/world/shadows/tree_tall.tres`, `apple_tree.tres`
  - `MaxLengthWorld`
  - `Opacity`
  - `ContactOpacity`
- `field_01.tscn`
  - `ShaderMaterial_world_cloud_v3/max_strength`
  - `ShaderMaterial_grade_v3/contrast`
- `foliage_wind.gdshader`
  - `relight_strength`, `sun_highlight`, `shade_depth`

## Giới hạn hiện tại

Source package được gửi không có project root (`project.godot` / `.csproj`), nên patch này đã được static-validate về path, resource ID và balance syntax nhưng chưa thể chạy Godot/Mono compile trong container. Lần test đầu tiên cần lấy screenshot + log Godot để tune tiếp, đặc biệt là alpha/length của shadow và mức color grade.
