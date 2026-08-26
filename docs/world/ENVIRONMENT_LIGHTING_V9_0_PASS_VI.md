# Field 01 Lighting V9.0

Mục tiêu V9: sau khi grass gần chạm trần, tập trung nâng mood bằng ánh sáng và shadow hierarchy.

## Thay đổi chính
- thêm `16_canopy_ambient_shadow.png` cho canopy ambient / forest-edge shade tĩnh
- tune `WorldCloudShadow` thành broad hơn, chậm hơn, mạnh hơn
- tăng nhẹ ground relight để macro authored đọc rõ hơn
- tăng quality của tree footprint shadow (rộng hơn, dài hơn, đậm hơn)
- grade map ấm hơn chút, bớt tame, tăng contrast/vignette rất nhẹ

## File thay đổi
- `assets/graphics/world/whispering_fields/field_01_layers/16_canopy_ambient_shadow.png`
- `assets/shaders/world/world_cloud_shadow.gdshader`
- `scripts/World/Environment/WorldCloudShadow2D.cs`
- `assets/shaders/world/ground_lighting.gdshader`
- `scenes/world/whispering_fields/field_01.tscn`
- `data/world/shadows/tree_tall.tres`
- `data/world/shadows/apple_tree.tres`

## Tuning đáng chú ý
### Cloud shadow
- `max_shadow_strength = 0.148`
- `max_sun_gain = 0.072`
- `world_scale = 0.00082`
- `shadow_softness = 0.052`
- `fair_weather_activity = 0.145`
- retarget `44–84s`
- speed `4.5–8.8 px/s`

### Ground
- `macro_strength = 0.275`
- `micro_strength = 0.032`
- `contact_strength = 0.096`
- `normal_strength = 0.090`
- `sun_gain = 0.116`

### Tree shadows
- tree: `Opacity 0.45`, `WidthScale 1.00`, `34 -> 88`
- apple: `Opacity 0.44`, `WidthScale 1.00`, `36 -> 92`
