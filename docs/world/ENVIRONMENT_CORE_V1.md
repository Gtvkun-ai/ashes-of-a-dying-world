# Environment Core V1.1

## Muc tieu

Core giu world time / weather / derived environment state tach khoi tung map.
Field 1 chi la consumer dau tien, khong so huu clock va khong push shader parameter rieng.

```text
WorldClock
   -> WorldEnvironmentService
      -> EnvironmentState
         -> ShaderGlobalBridge -> RenderingServer global shader uniforms
         -> EnvironmentBinder2D -> CanvasModulate cua map
```

- `WorldClock`: state can luu save (`day`, `time_of_day`).
- `EnvironmentProfile`: profile theo biome/map, gom gradient ngay-dem va thong so nen.
- `EnvironmentWeatherPreset`: weather doc lap voi time-of-day.
- `WorldEnvironmentService`: runtime hub song xuyen scene, tinh derived state mot lan moi frame.
- `ShaderGlobalBridge`: publish state sang GPU mot lan moi frame.
- `EnvironmentBinder2D`: adapter map rat mong; chi set profile va ambient tint.

## Shader globals

`res://override.cfg` khai bao cac ProjectSettings `shader_globals/*` truoc khi Godot compile shader.
File nay duoc Godot merge sau `project.godot`, nen khong can thay/ghi de `project.godot` cua game.

Contract V1.1:

```glsl
global uniform float env_time01;
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_sun_direction;
global uniform vec4 env_sun_color;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_fog;
global uniform float env_cloudiness;
```

Shader consumer khong can group, khong can duoc scan, va C# khong can `SetShaderParameter()` tung material.
Them bao nhieu grass/water/weather shader cung van chi co mot lan publish environment state / frame.

## Field 1 hien tai

- `CanvasModulate` nam tren world canvas nen HUD CanvasLayer khong bi tint theo ngay-dem.
- `whispering_fields.tres` la profile rieng cua biome.
- `09_1_mud_puddles water.png` dang dung `puddle_water.gdshader` va doc global environment state.
- `09_2_mud_puddles water leaf.png` van la layer rieng, khong bi distort theo nuoc.
- `canopy_shadow.gdshader` co san nhung chua gan vao `02_canopy_shadow.png`, vi layer nay con lan pixel dia hinh/cliff that.
- `foliage_wind.gdshader` san sang cho pass flora tiep theo.

## Debug QA (debug build only)

`EnvironmentDebugController` duoc `WorldEnvironmentService` tu tao trong debug build, khong tao UI va khong can InputMap:

- `Ctrl+F9`: cycle 06:00 -> 12:00 -> 18:00 -> 00:00.
- `Ctrl+F10`: cycle time scale `0x -> 1x -> 60x -> 600x`.
- `Ctrl+F12`: cycle weather `clear -> rainy -> stormy` (transition 1.25s).

Moi thao tac print snapshot ngan vao Output de QA shader nhanh.

## Nguyen tac save

Chi save state that:

```text
day
time_of_day
```

`daylight`, `night_factor`, `sun_color`, `wind`, `wetness`... la derived state va duoc tinh lai sau load.

## Huong phat trien tiep

1. Gan `foliage_wind.gdshader` co chu dich vao grass / flower / tree scene.
2. Them material wet-ground rieng, tach khoi puddle water neu visual can khac nhau.
3. Them local `PointLight2D` cho torch / spell, intensity doc `NightFactor` tu service.
4. Them particle module rain / fog / firefly doc cung EnvironmentState.
5. Chi them astronomy / season / biome override sau khi gameplay that su dung den.

## Reference

- Godot global shader uniforms: dung cho environmental effects, update runtime qua `RenderingServer.global_shader_parameter_set`.
- `third_party_refs/Godot4-Weather-System-2D`: tham khao pattern runtime hub + time/weather tach truc.
- `third_party_refs/GODOT-VFX-LIBRARY`: chi dung lam reference VFX, khong keo nguyen dependency vao runtime.
- `temp_assets/DynamicDayNightCycles`: chi tham khao astronomy/day-length, chua dua vao core.
