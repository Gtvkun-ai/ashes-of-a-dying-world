shader_type canvas_item;
render_mode unshaded, blend_mul;

// Ashes Environment V5.6 - world-locked cloud/dapple shadow aligned with sun direction.
global uniform float env_time01;
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_sun_direction;
global uniform vec2 env_key_direction;
global uniform float env_shadow_length;
global uniform vec4 env_sun_color;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_fog;
global uniform float env_cloudiness;
global uniform float env_shadow_strength;
global uniform float env_water_shimmer;
global uniform float env_water_ripple;
uniform float max_strength : hint_range(0.0, 0.3) = 0.09;
uniform float world_scale : hint_range(0.0001, 0.02) = 0.0032;
uniform vec2 wind_dir = vec2(1.0, 0.15);
uniform float speed : hint_range(0.0, 20.0) = 3.2;

varying vec2 world_pos;

float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
               mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
    float s = 0.0;
    float a = 0.58;
    for (int i = 0; i < 3; i++) {
        s += a * vnoise(p);
        p = p * 2.03 + 3.17;
        a *= 0.48;
    }
    return s;
}

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
    float coverage = clamp(env_cloudiness, 0.0, 1.0);
    vec3 cloud_mul = vec3(1.0);

    if (coverage >= 0.025 && env_daylight >= 0.02) {
        vec2 wdir = length(wind_dir) > 0.0001 ? normalize(wind_dir) : vec2(1.0, 0.0);
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        vec2 across = vec2(-to_key.y, to_key.x);
        vec2 p = world_pos * world_scale + wdir * TIME * speed * world_scale;
        float n = fbm(p);
        float low_sun = clamp(env_shadow_length, 0.0, 1.0);
        float beam_band = 0.5 + 0.5 * sin(dot(world_pos, across) * mix(0.012, 0.006, low_sun) + dot(world_pos, to_key) * 0.002);
        float dapple = smoothstep(0.42, 0.86, fbm(world_pos * world_scale * mix(1.55, 0.86, low_sun) + to_key * 7.0));
        float threshold = mix(0.72, 0.43, coverage);
        float patch = smoothstep(threshold - 0.07, threshold + 0.10, n + beam_band * 0.10 + dapple * 0.06);
        float strength = patch * max_strength * env_daylight * (0.35 + coverage * 0.65) * (1.0 + low_sun * 0.22);
        cloud_mul = mix(vec3(1.0), vec3(0.80, 0.86, 0.90), strength / max(max_strength, 0.0001));
    }

    COLOR = vec4(cloud_mul, 1.0);
}
