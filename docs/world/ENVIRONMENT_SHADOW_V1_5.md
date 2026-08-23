# Environment Shadow V1.5 — Asset Projection

## Mục tiêu

V1.4 dùng ellipse/blob để giả bóng prop. Cách đó rẻ nhưng sai silhouette và đặc biệt xấu khi bóng dài.
V1.5 chuyển sang **asset-projected shadow**: lấy alpha của chính sprite/frame hiện tại rồi shader ép nó xuống mặt đất.

## Shader

`assets/shaders/world/projected_asset_shadow.gdshader`

Có 2 mode:

- `GroundPlane`: cây, đá, hoa, cỏ, nhân vật. Pixel càng cao trên sprite càng bị kéo xa theo hướng key light; toàn silhouette được ép dẹt quanh chân asset.
- `RigidDrop`: cliff/wall layer lớn. Giữ nguyên silhouette rồi dịch nguyên layer theo hướng key light, tránh ép cả map về một đường chân chung.

Shader chỉ đọc alpha của texture nguồn, RGB gốc bị bỏ. `alpha_cutoff` loại halo/background alpha thấp của asset tải ngoài.

## ProjectedShadow2D

`ProjectedShadow2D.cs` tạo một Sprite2D proxy ẩn phía sau visual và đồng bộ:

- texture hiện tại của Sprite2D/AnimatedSprite2D;
- transform/scale/flip;
- frame animation theo nhịp chung của `EnvironmentShadowBus`;
- hướng, độ dài, độ dẹt, màu và opacity theo `CelestialSolver`.

Không có `_Process()` riêng trên từng prop. Field 1 có hàng trăm foliage vẫn dùng một ShadowBus cập nhật chung.

## Field 1 đã nối

- tree + apple tree: dùng **asset nguyên bản** làm shadow source, dù visual runtime đã tách trunk/canopy;
- stone_01..05: dùng đúng AtlasTexture/frame của từng loại đá;
- red/purple/blue flower, grass, grass_patch: dùng frame animation hiện tại;
- CliffWall: dùng rigid silhouette drop từ chính `05_cliff_wall.png`;
- Hikaru, Hyou và slime: dùng frame body hiện tại; Hikaru được resolve lại sau khi BodyScene dựng deferred.

Contact shadow nhỏ của flora vẫn giữ lại để chân vật luôn bám đất; projected shadow là lớp ánh sáng động phía trên nó.

## Tuning

Mỗi caster có:

- `BaseY01`: chân thật của asset trong texture;
- `GroundOffset`: canh chân asset xuống ground;
- `VirtualHeightPixels`;
- `NoonProjectionPixels` / `MaxProjectionPixels`;
- `NoonFlatten` / `HorizonFlatten`;
- `BaseOpacity` / `AlphaCutoff`.

Không dùng một ellipse chung cho mọi thứ nữa.
