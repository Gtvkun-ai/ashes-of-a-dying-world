shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.6 - art-directed directional grass skin.
// This material owns ground relighting. Godot Light2D is intentionally ignored here to avoid double-lighting.
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

uniform sampler2D macro_mask : filter_linear, repeat_disable;
uniform sampler2D micro_detail : filter_nearest, repeat_disable;
uniform sampler2D contact_mask : filter_linear, repeat_disable;
uniform sampler2D normal_soft : filter_linear, repeat_disable;

uniform sampler2D tree_ground_influence_mask : filter_linear, repeat_disable;
uniform vec2 tree_ground_world_size = vec2(1824.0, 1896.0);
uniform vec2 tree_ground_world_origin = vec2(0.0, 0.0);
uniform float tree_ground_strength : hint_range(0.0, 0.30) = 0.0;
uniform float tree_ground_tint_strength : hint_range(0.0, 0.20) = 0.0;

uniform float macro_strength : hint_range(0.0, 0.20) = 0.012;
uniform float micro_strength : hint_range(0.0, 0.12) = 0.0;
uniform float contact_strength : hint_range(0.0, 0.30) = 0.135;
uniform float contact_coolth : hint_range(0.0, 0.20) = 0.025;
uniform float normal_strength : hint_range(0.0, 0.20) = 0.045;
uniform float sun_gain : hint_range(0.0, 0.20) = 0.012;
uniform float golden_gain : hint_range(0.0, 0.25) = 0.030;
uniform float wet_darken : hint_range(0.0, 0.20) = 0.055;

varying vec2 world_pos;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

float hash(vec2 p) {
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 19.19);
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
    for (int i = 0; i < 3; i++) {
        s += a * vnoise(p);
        p = p * 2.04 + 6.13;
        a *= 0.48;
    }
    return s;
}

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

        float elevation = clamp(env_key_elevation, 0.0, 1.0);
        float shadow_length = clamp(env_shadow_length, 0.0, 1.0);
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        vec2 across_key = vec2(-to_key.y, to_key.x);

        float macro = clamp((texture(macro_mask, UV).r - 0.5) * 2.0, -0.85, 0.85);

        // V5.4: transparent micro pixels are neutral. The previous code read transparent black as -1,
        // which darkened almost the entire ground. The current Field 1 micro texture is decorative art,
        // so its scene strength is zero; this alpha-safe path keeps the shader correct for future masks.
        vec4 micro_sample = texture(micro_detail, UV);
        float micro = clamp((micro_sample.r - 0.5) * 2.0, -1.0, 1.0) * micro_sample.a;

        float contact_raw = clamp(texture(contact_mask, UV).r, 0.0, 1.0);
        float contact = smoothstep(0.035, 0.78, contact_raw);

        vec2 tree_uv = (world_pos - tree_ground_world_origin) / max(tree_ground_world_size, vec2(1.0));
        tree_uv = clamp(tree_uv, vec2(0.0), vec2(1.0));
        float tree_ground = clamp(texture(tree_ground_influence_mask, tree_uv).r, 0.0, 1.0);
        float tree_ground_core = smoothstep(0.18, 0.82, tree_ground);

        float broad = fbm(world_pos * 0.00115 + to_key * 2.4);
        float broad_band = sin(dot(world_pos, across_key) * 0.0032 + dot(world_pos, to_key) * 0.0012);
        float broad_light = smoothstep(0.43, 0.76, broad + broad_band * 0.12);
        float broad_shade = smoothstep(0.50, 0.84, 1.0 - broad - broad_band * 0.08);

        float canopy_scale = mix(0.0052, 0.0033, shadow_length);
        float forest_patch = fbm(world_pos * canopy_scale + to_key * 0.75);
        float banding = sin(dot(world_pos, across_key) * 0.010 + dot(world_pos, to_key) * 0.003);
        float dapple = smoothstep(0.48, 0.82, forest_patch + banding * 0.11);
        float open_sun = 1.0 - smoothstep(0.34, 0.82, forest_patch - banding * 0.045);

        result *= 1.0 + macro * macro_strength;
        result *= 1.0 + micro * micro_strength;

        // Ground integration around path / cliff / pond. Keep it broad and quiet: darker soil contact,
        // only a hint of coolness, never a white/blue outline.
        result *= 1.0 - contact * contact_strength;
        result = mix(result, result * vec3(0.965, 0.985, 1.018), contact * contact_coolth);

        // V13: tree-ground influence is authored from real tree placements in the scene.
        // It darkens and slightly compresses color under the roots so trees feel planted into the ground,
        // instead of just receiving a separate shadow decal.
        result *= 1.0 - tree_ground * tree_ground_strength;
        result = mix(result, result * vec3(0.90, 0.96, 0.86), tree_ground_core * tree_ground_tint_strength);

        // Very soft authored normal response. It adds large-form rolloff without making grass glossy.
        vec3 n = texture(normal_soft, UV).rgb * 2.0 - 1.0;
        n = normalize(vec3(n.xy, max(n.z, 0.42)));
        float directional = clamp(dot(n.xy, to_key) * 1.45, -1.0, 1.0);
        float key_soft = key * day * (1.0 - cloud * 0.48);
        float lit = smoothstep(-0.18, 0.74, directional);
        float shaded = smoothstep(0.02, 0.78, -directional);
        float top_light = smoothstep(0.40, 0.94, n.z) * elevation;
        float dapple_strength = key_soft * mix(0.035, 0.105, shadow_length);

        result *= 1.0 - broad_shade * key_soft * 0.055;
        result += env_key_color.rgb * broad_light * key_soft * sun_gain * (0.70 + elevation * 0.40);
        result = mix(result, result * vec3(0.92, 0.97, 0.91), broad_light * key_soft * 0.035);
        result *= 1.0 + lit * normal_strength * key_soft * (0.76 + top_light * 0.24);
        result *= 1.0 - shaded * normal_strength * key_soft * 0.82;
        result *= 1.0 - dapple * dapple_strength * (0.76 + broad_shade * 0.24);
        result += env_key_color.rgb * open_sun * key_soft * sun_gain * (0.40 + elevation * 0.60);
        result = mix(result, result * vec3(0.88, 0.94, 0.96), dapple * key_soft * 0.055);

        float y = luma(result);
        float highlight_zone = smoothstep(0.40, 0.76, y);
        result += env_key_color.rgb * sun_gain * key_soft * (0.16 + highlight_zone * 0.34) * lit;
        result += vec3(0.070, 0.031, 0.001) * golden * golden_gain * highlight_zone * (0.35 + lit * 0.65);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        vec3 night_result = result * vec3(0.67, 0.82, 1.08) + vec3(0.005, 0.012, 0.026);
        result = mix(result, night_result, night * 0.34);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
