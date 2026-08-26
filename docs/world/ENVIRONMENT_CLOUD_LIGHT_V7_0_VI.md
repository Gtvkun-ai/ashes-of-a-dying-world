# Environment Cloud Light V7.0

## Mục tiêu

Field 01 dùng một pass `WorldCloudShadow` world-space để tạo **mảng nắng lớn + bóng mây lớn di chuyển** mà không biến ground thành procedural noise.

Static art vẫn quyết định composition nền (`10_grass_macro_mask`, edge/contact/tuft). Cloud pass chỉ là atmosphere động.

## Runtime

- Scene node: `EnvironmentFX/WorldCloudShadow`
- Shader: `assets/shaders/world/world_cloud_shadow.gdshader`
- Controller: `scripts/World/Environment/WorldCloudShadow2D.cs`
- Weather input: `EnvironmentState.Cloudiness`, daylight, rain, fog, wind, key light.

`WorldAtmosphere2D` đã có logic tự tắt cloud overlay screen-space cũ khi tìm thấy node `WorldCloudShadow`, nên hai cloud pass không chồng lên nhau.

## Random cloud cells

Mỗi lần load scene (Seed = 0):

- phase cloud được random một lần;
- mỗi 36-72 giây chọn một weather-cell mới;
- hướng gió jitter ±16 độ;
- tốc độ 6.5-12.5 world px/s;
- density và độ mở nắng thay đổi nhẹ;
- mọi thay đổi được lerp chậm, không pop.

Movement dùng `flow_offset` tích phân trên CPU. Không dùng `TIME * direction` vì đổi direction theo runtime sẽ làm noise teleport.

## Scale / contrast hiện tại

- `world_scale = 0.0011`: cloud mass cỡ vài trăm tới hơn 1000 world px.
- `max_shadow_strength = 0.115`: bóng mây full-strength khoảng 6-12% tùy daylight/weather.
- `max_sun_gain = 0.048`: open-sun gap tăng khoảng 3-5%.
- `fair_weather_activity = 0.10`: clear weather vẫn có cloud patch thưa.

## Tuning nhanh

Nếu cloud quá nhỏ/bận: giảm `WorldScale` xuống `0.00085-0.0010`.

Nếu cloud quá to/phẳng: tăng `WorldScale` lên `0.00125-0.00145`.

Nếu bóng quá mạnh: `max_shadow_strength = 0.085-0.10`.

Nếu muốn reference có sunny clearing rõ hơn: `max_sun_gain = 0.055-0.065`, không tăng micro-noise.

Nếu mây chạy nhanh: giảm `MinSpeed/MaxSpeed`; không sửa shader TIME vì V7 không dùng TIME cho drift.
