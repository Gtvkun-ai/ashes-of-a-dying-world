# Environment Core V1

## Muc tieu

Core moi giu **world time** va **environment state** tach khoi tung map. Field 1 chi la consumer dau tien.

```text
WorldClock
   -> WorldEnvironmentService
      -> EnvironmentState
         -> EnvironmentBinder2D
            -> CanvasModulate
            -> shader consumers
```

- `WorldClock`: state can luu save (`day`, `time_of_day`).
- `EnvironmentProfile`: profile theo biome/map, gom gradient ngay-dem va thong so nen.
- `EnvironmentWeatherPreset`: weather doc lap voi time-of-day.
- `WorldEnvironmentService`: runtime hub song xuyen world scene, tinh derived state.
- `EnvironmentBinder2D`: adapter map. Core khong biet node cua Field 1.

## Shader contract V1

Shader muon nhan environment state can:

1. Co `ShaderMaterial`.
2. Node chua material nam trong group `environment_shader_consumer`.
3. Khai bao cac uniform can dung trong contract sau:

```glsl
uniform float env_time01;
uniform float env_daylight;
uniform float env_night;
uniform vec2 env_sun_direction;
uniform vec4 env_sun_color;
uniform float env_wind;
uniform float env_rain;
uniform float env_wetness;
uniform float env_fog;
uniform float env_cloudiness;
```

V1 dung local uniforms qua binder thay vi `global uniform`, vi source bundle hien tai khong kem top-level `project.godot`. Transport nay co the doi sang global shader uniforms sau ma khong can doi `EnvironmentState` hay API gameplay.

## Field 1 hien tai

- `CanvasModulate` da duoc dua ve world canvas de ngay-dem khong tint UI CanvasLayer.
- `whispering_fields.tres` la profile rieng cua biome.
- Puddle water (`09_1_mud_puddles water.png`) da opt-in shader environment.
- Puddle leaves (`09_2_mud_puddles water leaf.png`) van la layer rieng va khong bi distort theo nuoc.
- `canopy_shadow.gdshader` da co nhung **chua gan** vao `02_canopy_shadow.png`, vi layer hien tai con chua pixel cliff/ground that. Animate layer nay se lam ca dia hinh rung theo bong.
- `foliage_wind.gdshader` da san sang de gan co/cay trong pass tiep theo.

## Tham khao asset ben ngoai

Cac repo trong bundle duoc dung nhu reference architecture / VFX reference:

- `third_party_refs/Godot4-Weather-System-2D`: hoc pattern runtime hub + TimeOfDay/WeatherPreset tach truc + effect consumers.
- `temp_assets/DynamicDayNightCycles`: tham khao astronomy/day-length, **chua dua vao core** vi qua nang cho nhu cau hien tai.
- `third_party_refs/GODOT-VFX-LIBRARY`: tham khao rain/fireflies/leaves/water VFX. Chua copy nguyen addon vao runtime vi scene cua addon phu thuoc `res://addons/vfx_library/...` va se keo theo dependency khong can thiet.

Runtime code/shader trong `scripts/World/Environment` va `assets/shaders/world` la integration cua Ashes; khong copy nguyen source addon vao core.

## Huong phat trien tiep

1. Gan foliage wind co chu dich cho grass/tree scene.
2. Them `EnvironmentParticles2D` doc `RainAmount`, `WindStrength`, `FogAmount` de render rain/fog/fireflies.
3. Them local dynamic light cho torch/spell va cho intensity phan ung `NightFactor`.
4. Khi top-level `project.godot` co mat trong source bundle, co the chuyen shader transport sang Godot global uniforms neu profiling cho thay can thiet.
5. Astronomy/season chi them nhu module ben ngoai khi gameplay thuc su dung den.
