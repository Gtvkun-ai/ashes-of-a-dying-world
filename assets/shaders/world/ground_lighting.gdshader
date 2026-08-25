shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V6.0 - authored grass composition.
//
// IMPORTANT ART RULE:
// The map decides WHERE grass is bright/dark via authored masks. Runtime shader code
// only relights those authored forms for time/weather. No FBM/noise is allowed to
// invent large composition because that made Field 01 read as painterly texture
// instead of intentional pixel-art ground.
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

uniform float macro_strength : hint_range(0.0, 0.30) = 0.23;
uniform float micro_strength : hint_range(0.0, 0.12) = 0.025;
uniform float contact_strength : hint_range(0.0, 0.30) = 0.085;
uniform float contact_coolth : hint_range(0.0, 0.20) = 0.028;
uniform float normal_strength : hint_range(0.0, 0.20) = 0.070;
uniform float sun_gain : hint_range(0.0, 0.20) = 0.095;
uniform float golden_gain : hint_range(0.0, 0.25) = 0.060;
uniform float wet_darken : hint_range(0.0, 0.20) = 0.040;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
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
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);

        // 0.5 is exact neutral. Positive values are authored clearings/sunlit grass;
        // negative values are authored visual-rest/shade masses.
        float macro = clamp((texture(macro_mask, UV).r - 0.5) * 2.0, -1.0, 1.0);
        float sun_zone = smoothstep(0.03, 0.74, macro);
        float shade_zone = smoothstep(0.03, 0.74, -macro);

        // Sparse micro map is alpha-safe: transparent pixels are fully neutral.
        vec4 micro_sample = texture(micro_detail, UV);
        float micro = clamp((micro_sample.r - 0.5) * 2.0, -1.0, 1.0) * micro_sample.a;

        float contact_raw = clamp(texture(contact_mask, UV).r, 0.0, 1.0);
        float contact = smoothstep(0.035, 0.78, contact_raw);

        // Keep a little authored value structure at all times, but make it most
        // expressive in daylight. This preserves readability through day/night.
        float macro_day_mix = mix(0.58, 1.0, day);
        result *= 1.0 + macro * macro_strength * macro_day_mix;
        result *= 1.0 + micro * micro_strength;

        // Ground integration around path / cliff / pond.
        result *= 1.0 - contact * contact_strength;
        result = mix(result, result * vec3(0.958, 0.980, 1.018), contact * contact_coolth);

        // Authored soft normal response. The V6 normal actually contains form now;
        // it remains restrained so characters/props keep higher contrast priority.
        vec3 n = texture(normal_soft, UV).rgb * 2.0 - 1.0;
        n = normalize(vec3(n.xy, max(n.z, 0.48)));
        float directional = clamp(dot(n.xy, to_key) * 1.55, -1.0, 1.0);
        float lit = smoothstep(-0.14, 0.70, directional);
        float shaded = smoothstep(0.04, 0.74, -directional);
        float top_light = smoothstep(0.45, 0.96, n.z) * elevation;
        float key_soft = key * day * (1.0 - cloud * 0.52);

        // Clearings carry the large readable light shapes seen in the target style.
        // Shade regions are slightly cooler/deeper, not black overlays.
        result += env_key_color.rgb * sun_zone * key_soft * sun_gain * (0.72 + elevation * 0.38);
        result *= 1.0 - shade_zone * key_soft * 0.065;
        result = mix(result, result * vec3(0.925, 0.965, 1.025), shade_zone * key_soft * 0.050);

        // Directional roll is intentionally secondary to the authored macro shapes.
        result *= 1.0 + lit * normal_strength * key_soft * (0.72 + top_light * 0.28);
        result *= 1.0 - shaded * normal_strength * key_soft * 0.68;

        float y = luma(result);
        float highlight_zone = smoothstep(0.34, 0.72, y);
        result += env_key_color.rgb * sun_gain * key_soft * 0.16 * highlight_zone * lit;
        result += vec3(0.072, 0.033, 0.002) * golden * golden_gain * highlight_zone * (0.32 + lit * 0.68);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        vec3 night_result = result * vec3(0.67, 0.82, 1.08) + vec3(0.005, 0.012, 0.026);
        result = mix(result, night_result, night * 0.34);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
