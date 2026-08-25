shader_type canvas_item;
render_mode blend_add, unshaded;

uniform vec2 beam_direction = vec2(0.65, 0.75);
uniform vec4 beam_color : source_color = vec4(1.0, 0.86, 0.50, 1.0);
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform float width : hint_range(0.02, 0.60) = 0.16;
uniform float softness : hint_range(0.01, 0.60) = 0.28;
uniform float center : hint_range(-1.0, 1.0) = -0.22;

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

void fragment() {
    if (intensity < 0.005) {
        COLOR = vec4(0.0);
    } else {
        vec2 dir = length(beam_direction) > 0.0001 ? normalize(beam_direction) : normalize(vec2(0.65, 0.75));
        vec2 side_axis = vec2(-dir.y, dir.x);
        vec2 p = UV - vec2(0.5);

        float along = dot(p, dir);
        float side = dot(p, side_axis);
        float shaft_a = 1.0 - smoothstep(width, width + softness, abs(side - center));
        float shaft_b = 0.54 * (1.0 - smoothstep(width * 0.70, width * 1.92, abs(side - center - 0.34)));
        float shaft_c = 0.38 * (1.0 - smoothstep(width * 0.58, width * 1.60, abs(side - center + 0.46)));
        float shaft_d = 0.24 * (1.0 - smoothstep(width * 0.44, width * 1.38, abs(side - center + 0.78)));
        float shafts = max(shaft_a, max(shaft_b, shaft_c));
        shafts = max(shafts, shaft_d);

        float entry = smoothstep(-0.88, -0.18, along);
        float fade = 1.0 - smoothstep(0.34, 1.02, along);
        float haze = smoothstep(-0.20, 0.82, UV.y) * (1.0 - smoothstep(0.90, 1.05, UV.y));
        float broken = 0.62 + 0.38 * vnoise(vec2(side * 4.6, along * 2.0) + dir * TIME * 0.018);
        float canopy_break = smoothstep(0.24, 0.86, vnoise(UV * vec2(5.0, 3.4) + side_axis * 0.9));
        float grain = 0.86 + 0.14 * vnoise(UV * vec2(9.0, 4.6) + dir * TIME * 0.018);

        float alpha = shafts * entry * fade * haze * broken * canopy_break * grain * intensity;
        COLOR = vec4(beam_color.rgb, clamp(alpha, 0.0, 1.0) * beam_color.a);
    }
}
