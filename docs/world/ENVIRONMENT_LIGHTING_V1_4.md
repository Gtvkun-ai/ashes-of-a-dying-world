# Environment Lighting V1.4 — Celestial + Projected Shadow

## Mục tiêu
V1.4 sửa đúng vấn đề Field 1 bị “phẳng”: trước đây `CanvasModulate` chỉ tint toàn map, còn `SunDirection`
không có direct-light/shadow consumer đủ rõ. Vì vậy 12:00 nhìn gần như ảnh gốc và cây/đá không bám đất.

## Luồng mới

```text
WorldClock
  -> WorldEnvironmentService
     -> CelestialSolver
        -> Sun/Moon direction + elevation + energy
     -> EnvironmentState
        -> CanvasModulate (ambient)
        -> WorldLighting2D (DirectionalLight2D sun + moon)
        -> EnvironmentShadowBus -> ProjectedShadow2D
        -> EnvironmentMaterialBus -> water/foliage shader
        -> AmbientFireflies2D (night only)
```

## Vì sao không dùng LightOccluder2D cho cây ngay
Bóng hình học của Godot hợp với tường/cliff/building. Với cây top-down pixel-art, một blob projected shadow có kiểm soát
đẹp và ổn định hơn. Pass sau có thể thêm LightOccluder2D cho kiến trúc lớn mà không phải sửa core.

## Asset bên ngoài được tận dụng
`assets/graphics/vfx/environment/firefly.png` được copy từ `third_party_refs/GODOT-VFX-LIBRARY`
(MIT License, Copyright (c) 2024 Godot VFX Library). Chỉ asset firefly được đưa vào runtime; không kéo nguyên addon.

Pattern “unified sun/moon state” tham khảo `third_party_refs/Godot4-Weather-System-2D` (MIT, Greg/livingsgb),
nhưng công thức C# trong `CelestialSolver.cs` là implementation riêng của Ashes.

## Test nhanh trong debug build
- `Ctrl+F9`: đổi 06:00 -> 12:00 -> 18:00 -> 00:00.
- 12:00: bóng cây/đá ngắn, ambient không trắng tuyệt đối, sun direct-light hoạt động.
- 06:00 / 18:00: bóng dài và đổi hướng rõ.
- 00:00: moon light xanh nhẹ, firefly xuất hiện.
- `Ctrl+F12`: storm làm direct-light yếu đi do cloud attenuation.

Output nên có:

```text
[WorldLighting2D] READY sun+moon directional lighting
[EnvironmentBinder2D] BOUND materials=... projected_shadows=...
[EnvironmentDebug] ... sunEl=... sunE=... moonEl=... moonE=...
```
