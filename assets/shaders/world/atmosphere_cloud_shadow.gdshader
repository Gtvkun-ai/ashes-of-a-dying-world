shader_type canvas_item;

uniform float coverage : hint_range(0.0, 1.0) = 0.0;
uniform vec2 wind_dir = vec2(1.0, 0.12);
uniform float speed : hint_range(0.0, 0.2) = 0.025;
uniform vec4 shadow_color : source_color = vec4(0.02, 0.035, 0.05, 1.0);
uniform float strength : hint_range(0.0, 1.0) = 0.0;

const float HP = 256.0;

float hash(vec2 p) {
    p = mod(p, HP);
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
    float a = 0.55;
    for (int i = 0; i < 4; i++) {
        s += a * vnoise(p);
        p = p * 2.0 + 1.3;
        a *= 0.5;
    }
    return s;
}

void fragment() {
    if (coverage < 0.02 || strength < 0.005) {
        COLOR = vec4(0.0);
    } else {
        vec2 wdir = length(wind_dir) > 0.0001 ? normalize(wind_dir) : vec2(1.0, 0.0);
        vec2 uv = UV * vec2(2.8, 2.0) + wdir * TIME * speed * 2.0;
        float n = fbm(uv);
        float patch = smoothstep(0.58 - coverage * 0.42, 0.76, n);
        COLOR = vec4(shadow_color.rgb, patch * strength);
    }
}
