shader_type canvas_item;
render_mode blend_add, unshaded;

uniform vec2 beam_direction = vec2(0.65, 0.75);
uniform vec4 beam_color : source_color = vec4(1.0, 0.86, 0.50, 1.0);
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform float width : hint_range(0.02, 0.60) = 0.22;
uniform float softness : hint_range(0.01, 0.60) = 0.18;
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
        float core = 1.0 - smoothstep(width, width + softness, abs(side - center));
        float secondary = 0.42 * (1.0 - smoothstep(width * 0.55, width * 1.35, abs(side - center - 0.34)));
        float entry = smoothstep(-0.72, -0.08, along);
        float fade = 1.0 - smoothstep(0.28, 0.82, along);
        float grain = 0.78 + 0.22 * vnoise(UV * vec2(7.0, 3.0) + dir * TIME * 0.025);

        float alpha = (core + secondary) * entry * fade * grain * intensity;
        COLOR = vec4(beam_color.rgb, clamp(alpha, 0.0, 1.0) * beam_color.a);
    }
}
