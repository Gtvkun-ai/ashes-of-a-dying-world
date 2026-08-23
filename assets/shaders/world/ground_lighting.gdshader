shader_type canvas_item;

// V3 ground material: macro tonal variation cố định theo map UV.
// Không rung pixel, không procedural micro-noise. Chỉ phá cảm giác "tấm thảm xanh phẳng".
uniform float env_time01 = 0.5;
uniform float env_daylight = 1.0;
uniform float env_night = 0.0;
uniform vec2 env_sun_direction = vec2(-0.447214, 0.894427);
uniform vec4 env_sun_color : source_color = vec4(1.0);
uniform float env_wind = 0.32;
uniform float env_rain = 0.0;
uniform float env_wetness = 0.0;
uniform float env_fog = 0.0;
uniform float env_cloudiness = 0.1;
uniform float env_shadow_strength = 0.55;
uniform float env_water_shimmer = 0.18;
uniform float env_water_ripple = 0.0;

uniform float macro_strength : hint_range(0.0, 0.15) = 0.045;
uniform float warm_day_gain : hint_range(0.0, 0.15) = 0.025;
uniform float wet_darken : hint_range(0.0, 0.20) = 0.055;

float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float noise2(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x),
        u.y);
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    // Chỉ vài mảng lớn trên cả map. Hai octave đủ, thêm nữa chỉ biến pixel-art thành nhiễu.
    float n0 = noise2(UV * vec2(6.0, 6.6));
    float n1 = noise2(UV * vec2(12.0, 10.0) + 7.3) * 0.35;
    float macro = ((n0 + n1) / 1.35 - 0.5) * 2.0;
    result *= 1.0 + macro * macro_strength;

    float daylight = clamp(env_daylight, 0.0, 1.0);
    result += env_sun_color.rgb * warm_day_gain * daylight * (0.55 + macro * 0.18);

    float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
    result *= 1.0 - wet * wet_darken;
    result = mix(result, result * vec3(0.76, 0.83, 0.96), clamp(env_night, 0.0, 1.0) * 0.10);

    COLOR = vec4(result, base.a);
}
