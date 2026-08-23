shader_type canvas_item;

uniform float density : hint_range(0.0, 1.0) = 0.0;
uniform vec4 fog_color : source_color = vec4(0.70, 0.78, 0.78, 1.0);
uniform float horizon : hint_range(0.0, 1.0) = 0.54;
uniform float speed = 0.008;
uniform int quality = 1;

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

void fragment() {
    if (density < 0.005) {
        COLOR = vec4(0.0);
    } else {
        float band = 1.0 - clamp(abs(UV.y - horizon) / 0.44, 0.0, 1.0);
        float low_wash = smoothstep(0.18, 0.90, UV.y) * 0.22;
        float drift = quality == 1 ? vnoise(vec2(UV.x * 3.0 + TIME * speed, UV.y * 2.0)) : 0.5;
        float alpha = density * (0.18 + 0.66 * band + low_wash) * (0.72 + 0.42 * drift);
        COLOR = vec4(fog_color.rgb, clamp(alpha, 0.0, 1.0) * fog_color.a);
    }
}
