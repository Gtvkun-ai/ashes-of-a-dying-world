shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.6 - directional authored dirt path response.
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
uniform sampler2D tree_ground_influence_mask : filter_linear, repeat_disable;
uniform vec2 tree_ground_world_size = vec2(1824.0, 1896.0);
uniform vec2 tree_ground_world_origin = vec2(0.0, 0.0);
uniform float tree_ground_strength : hint_range(0.0, 0.30) = 0.0;
uniform float tree_ground_tint_strength : hint_range(0.0, 0.20) = 0.0;

varying vec2 world_pos;
uniform float normal_strength : hint_range(0.0, 0.40) = 0.12;
uniform float ao_strength : hint_range(0.0, 0.50) = 0.19;
uniform float sun_gain : hint_range(0.0, 0.20) = 0.020;
uniform float golden_gain : hint_range(0.0, 0.30) = 0.065;
uniform float wet_darken : hint_range(0.0, 0.30) = 0.075;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);
        float cloud = clamp(env_cloudiness, 0.0, 1.0);

        float ao = texture(ao_map, UV).r;
        result *= 1.0 - (1.0 - ao) * ao_strength;
        result = mix(result, result * vec3(0.965, 0.945, 0.885), day * 0.060);

        vec2 tree_uv = (world_pos - tree_ground_world_origin) / max(tree_ground_world_size, vec2(1.0));
        tree_uv = clamp(tree_uv, vec2(0.0), vec2(1.0));
        float tree_ground = clamp(texture(tree_ground_influence_mask, tree_uv).r, 0.0, 1.0);
        float tree_ground_core = smoothstep(0.18, 0.82, tree_ground);
        result *= 1.0 - tree_ground * tree_ground_strength;
        result = mix(result, result * vec3(0.90, 0.95, 0.88), tree_ground_core * tree_ground_tint_strength);

        vec3 n = texture(normal_map, UV).rgb * 2.0 - 1.0;
        n = normalize(vec3(n.xy, max(n.z, 0.34)));
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float directional = clamp(dot(n.xy, to_key) * 1.30, -1.0, 1.0);
        float key_soft = key * day * (1.0 - cloud * 0.36);
        float lit = smoothstep(-0.18, 0.76, directional);
        float shaded = smoothstep(0.00, 0.84, -directional);
        float top_light = smoothstep(0.38, 0.92, n.z) * clamp(env_key_elevation, 0.0, 1.0);

        result *= 1.0 + lit * normal_strength * key_soft * (0.72 + top_light * 0.28);
        result *= 1.0 - shaded * normal_strength * key_soft * 0.64;
        result = mix(result, result * vec3(0.90, 0.93, 0.98), shaded * key_soft * 0.10);
        result += env_key_color.rgb * (lit * 0.82 + top_light * 0.18) * sun_gain * key_soft;

        float low_sun = clamp(env_shadow_length, 0.0, 1.0);
        result += vec3(0.10, 0.050, 0.004) * golden * golden_gain * (0.42 + lit * 0.58 + low_sun * 0.18);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        vec3 night_result = result * vec3(0.66, 0.76, 0.98) + vec3(0.006, 0.010, 0.020);
        result = mix(result, night_result, night * 0.42);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
