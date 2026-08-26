# Field 01 V10 - Time-of-Day + Object Shadow Pass

Mục tiêu V10:

1. làm sáng / trưa / chiều / tối khác nhau rõ hơn bằng time-of-day grading thực sự
2. cứu bóng cho player, Hyou và đá để người chơi NHÌN THẤY có bóng

## Thay đổi chính

- tăng mạnh tính cách của dawn / noon / afternoon / night qua `world_color_grade.gdshader`
- mở rộng cửa sổ golden hour trong `ShaderGlobalBridge.cs`
- tăng direct-light contrast của biome `whispering_fields.tres`
- phục hồi actor / rock shadows qua:
  - `projected_shadow_v2.gdshader`
  - `ShadowRenderer2D.cs`
  - `character.tres`
  - `rock_small.tres`
  - `rock_medium.tres`
  - `rock_large.tres`

## Ý đồ art

### Time-of-day
- sáng: ấm nhẹ + bóng dài hơn
- trưa: rõ nhất, sáng nhất, ít golden nhất
- chiều: ấm rõ hơn sáng, bóng dài hơn
- tối: lạnh rõ nhưng không wash xám toàn scene

### Object shadow
- player / Hyou: có footprint shadow đọc ra ngay cả ở noon
- đá: có contact + body shadow rõ ràng hơn trên nền cỏ hiện tại

## File thay đổi
- `assets/shaders/world/world_color_grade.gdshader`
- `assets/shaders/world/projected_shadow_v2.gdshader`
- `scripts/World/Environment/ShadowRenderer2D.cs`
- `scripts/World/Environment/ShaderGlobalBridge.cs`
- `data/world/environment/whispering_fields.tres`
- `data/world/shadows/character.tres`
- `data/world/shadows/rock_small.tres`
- `data/world/shadows/rock_medium.tres`
- `data/world/shadows/rock_large.tres`
- `scenes/world/whispering_fields/field_01.tscn`

## Tuning nổi bật
- character shadow: `NoonLengthWorld 4.5`, `MaxLengthWorld 34`, `Opacity 0.44`
- rock small shadow: `NoonLengthWorld 2.2`, `Opacity 0.35`
- rock medium shadow: `NoonLengthWorld 3.2`, `Opacity 0.38`
- rock large shadow: `NoonLengthWorld 4.6`, `Opacity 0.40`
- biome shadow strength: `0.86`
- sun light energy: `0.72`
- day ambient strength: `0.78`
- night ambient strength: `0.64`
