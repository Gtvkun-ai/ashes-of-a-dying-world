shader_type canvas_item;
render_mode unshaded, blend_mul;

// Ashes Environment V3.3 - world-locked cloud shadow, Compatibility-safe.
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
        vec2 p = world_pos * world_scale + wdir * TIME * speed * world_scale;
        float n = fbm(p);
        float threshold = mix(0.72, 0.43, coverage);
        float patch = smoothstep(threshold - 0.07, threshold + 0.10, n);
        float strength = patch * max_strength * env_daylight * (0.35 + coverage * 0.65);
        cloud_mul = mix(vec3(1.0), vec3(0.82, 0.87, 0.91), strength / max(max_strength, 0.0001));
    }

    COLOR = vec4(cloud_mul, 1.0);
}
