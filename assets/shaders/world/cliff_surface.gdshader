shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.6 - directional normal/AO cliff and stairs material.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_elevation;
global uniform float env_key_strength;
global uniform float env_golden_hour;
global uniform float env_shadow_length;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;

uniform sampler2D normal_map : filter_linear, repeat_disable;
uniform sampler2D ao_map : filter_linear, repeat_disable;
// 1 = vertical cliff wall, 2 = stairs.
uniform float surface_mode : hint_range(1.0, 2.0, 1.0) = 1.0;
uniform float normal_strength : hint_range(0.0, 0.50) = 0.15;
uniform float ao_strength : hint_range(0.0, 0.60) = 0.26;
uniform float wall_darkening : hint_range(0.0, 0.30) = 0.095;
uniform float stairs_darkening : hint_range(0.0, 0.20) = 0.035;
uniform float edge_gain : hint_range(0.0, 0.20) = 0.028;
uniform float golden_gain : hint_range(0.0, 0.30) = 0.050;

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);
        float cloud = clamp(env_cloudiness, 0.0, 1.0);
        float is_stairs = step(1.5, surface_mode);
        float is_wall = 1.0 - is_stairs;

        float ao = texture(ao_map, UV).r;
        result *= 1.0 - (1.0 - ao) * ao_strength;
        result *= 1.0 - is_wall * wall_darkening;
        result *= 1.0 - is_stairs * stairs_darkening;

        vec3 n = texture(normal_map, UV).rgb * 2.0 - 1.0;
        n = normalize(vec3(n.xy, max(n.z, 0.28)));
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float directional = clamp(dot(n.xy, to_key) * mix(1.38, 1.14, is_stairs), -1.0, 1.0);
        float key_soft = key * day * (1.0 - cloud * 0.32);
        float plane_strength = mix(normal_strength, normal_strength * 0.72, is_stairs);
        float lit = smoothstep(-0.16, 0.74, directional);
        float shaded = smoothstep(0.02, 0.84, -directional);
        float top_light = smoothstep(0.38, 0.90, n.z) * clamp(env_key_elevation, 0.0, 1.0);

        result *= 1.0 + lit * plane_strength * key_soft * (0.75 + top_light * 0.25);
        result *= 1.0 - shaded * plane_strength * key_soft * mix(0.86, 0.62, is_stairs);
        result = mix(result, result * vec3(0.86, 0.90, 0.96), shaded * key_soft * mix(0.13, 0.08, is_stairs));

        // Tiny lip cue, not a baked-looking glowing outline.
        float alpha_above = texture(TEXTURE, UV - vec2(0.0, TEXTURE_PIXEL_SIZE.y)).a;
        float top_edge = base.a * (1.0 - alpha_above);
        float edge_lit = mix(0.65, 1.0, lit) * (0.64 + top_light * 0.36);
        result += env_key_color.rgb * top_edge * edge_gain * key_soft * edge_lit;
        result += vec3(0.085, 0.038, 0.002) * golden * golden_gain * (0.35 + lit * 0.65 + clamp(env_shadow_length, 0.0, 1.0) * 0.14);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * mix(0.04, 0.025, is_stairs);

        vec3 night_result = result * vec3(0.62, 0.72, 0.98) + vec3(0.004, 0.008, 0.018);
        result = mix(result, night_result, night * 0.48);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
