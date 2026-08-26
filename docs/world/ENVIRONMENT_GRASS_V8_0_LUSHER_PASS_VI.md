# Field 01 Grass V8.0 - Lusher Pass

Mục tiêu của V8 là giữ lại nền cỏ đã sạch của V6/V7 nhưng bớt hiền hơn:

- tăng macro sáng/tối có chủ đích
- tăng variation ấm/lạnh trên albedo base
- tăng mật độ micro grass có nhịp
- chuyển `15_grass_field_detail` từ rải lẻ tẻ sang cluster nhỏ có khoảng nghỉ
- làm `14_grass_edge_detail` giàu hơn quanh mép đường
- tăng nhẹ ground relight và cloud-light contrast

## File thay đổi

- `assets/graphics/world/whispering_fields/field_01_layers/00_ground_base.png`
- `assets/graphics/world/whispering_fields/field_01_layers/10_grass_macro_mask.png`
- `assets/graphics/world/whispering_fields/field_01_layers/11_grass_micro_detail.png`
- `assets/graphics/world/whispering_fields/field_01_layers/13_grass_normal_soft.png`
- `assets/graphics/world/whispering_fields/field_01_layers/14_grass_edge_detail.png`
- `assets/graphics/world/whispering_fields/field_01_layers/15_grass_field_detail.png`
- `assets/shaders/world/ground_lighting.gdshader`
- `assets/shaders/world/world_cloud_shadow.gdshader`
- `scenes/world/whispering_fields/field_01.tscn`
- `tools/art/rebuild_field01_grass_v8.py`

## Tuning mới

### Ground
- `macro_strength = 0.265`
- `micro_strength = 0.03`
- `contact_strength = 0.092`
- `normal_strength = 0.082`
- `sun_gain = 0.108`

### Cloud light
- `max_shadow_strength = 0.132`
- `max_sun_gain = 0.060`
- `world_scale = 0.00095`
- `fair_weather_activity = 0.12`
- `sun_open_boost = 1.08`
