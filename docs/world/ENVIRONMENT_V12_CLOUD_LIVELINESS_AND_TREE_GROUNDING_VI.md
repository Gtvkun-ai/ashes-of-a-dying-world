# Field 01 V12 - Cloud Liveliness + Tree Grounding

Mục tiêu V12:

1. làm cloud-light sống động hơn nhưng không quá procedural
2. sửa cảm giác cây bị bay khỏi mặt đất bằng grounding/contact đúng hơn

## Tree grounding
- tăng contact AO cho `tree_tall.tres` và `apple_tree.tres`
- kéo `ContactOffset` về 0 để AO bám ngay chân rễ
- giảm `centerBias` cho `ArtDirectedFootprint` để bóng lớn ăn gần gốc hơn
- tăng `root_ao` ở trunk material (`0.145`)

## Cloud liveliness
- retarget nhanh hơn: `28–52s`
- hướng cloud jitter lớn hơn: `18°`
- speed range: `5.5–11.0 px/s`
- density / sun-open jitter tăng
- broad cloud pass mạnh hơn nhẹ (`0.158 / 0.082`)

## File thay đổi
- `data/world/shadows/tree_tall.tres`
- `data/world/shadows/apple_tree.tres`
- `scenes/world/props/trees/tree.tscn`
- `scenes/world/props/trees/apple_tree.tscn`
- `assets/shaders/world/trunk_relight.gdshader`
- `scripts/World/Environment/ShadowCaster2D.cs`
- `scripts/World/Environment/WorldCloudShadow2D.cs`
- `assets/shaders/world/world_cloud_shadow.gdshader`
- `scenes/world/whispering_fields/field_01.tscn`
