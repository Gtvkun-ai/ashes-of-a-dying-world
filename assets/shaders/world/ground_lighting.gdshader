shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.5 - clean painterly grass skin.
// This material owns ground relighting. Godot Light2D is intentionally ignored here to avoid double-lighting.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_strength;
global uniform float env_golden_hour;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;

uniform sampler2D macro_mask : filter_linear, repeat_disable;
uniform sampler2D micro_detail : filter_nearest, repeat_disable;
uniform sampler2D contact_mask : filter_linear, repeat_disable;
uniform sampler2D normal_soft : filter_linear, repeat_disable;

uniform float macro_strength : hint_range(0.0, 0.20) = 0.012;
uniform float micro_strength : hint_range(0.0, 0.12) = 0.0;
uniform float contact_strength : hint_range(0.0, 0.30) = 0.135;
uniform float contact_coolth : hint_range(0.0, 0.20) = 0.025;
uniform float normal_strength : hint_range(0.0, 0.20) = 0.0;
uniform float sun_gain : hint_range(0.0, 0.20) = 0.012;
uniform float golden_gain : hint_range(0.0, 0.25) = 0.030;
uniform float wet_darken : hint_range(0.0, 0.20) = 0.055;

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

        float macro = clamp((texture(macro_mask, UV).r - 0.5) * 2.0, -0.85, 0.85);

        // V5.4: transparent micro pixels are neutral. The previous code read transparent black as -1,
        // which darkened almost the entire ground. The current Field 1 micro texture is decorative art,
        // so its scene strength is zero; this alpha-safe path keeps the shader correct for future masks.
        vec4 micro_sample = texture(micro_detail, UV);
        float micro = clamp((micro_sample.r - 0.5) * 2.0, -1.0, 1.0) * micro_sample.a;

        float contact_raw = clamp(texture(contact_mask, UV).r, 0.0, 1.0);
        float contact = smoothstep(0.035, 0.78, contact_raw);

        result *= 1.0 + macro * macro_strength;
        result *= 1.0 + micro * micro_strength;

        // Ground integration around path / cliff / pond. Keep it broad and quiet: darker soil contact,
        // only a hint of coolness, never a white/blue outline.
        result *= 1.0 - contact * contact_strength;
        result = mix(result, result * vec3(0.965, 0.985, 1.018), contact * contact_coolth);

        // Very soft authored normal response. It adds large-form rolloff without making grass glossy.
        vec3 n = texture(normal_soft, UV).rgb * 2.0 - 1.0;
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float directional = clamp(dot(n.xy, to_key), -0.75, 0.75);
        float key_soft = key * day * (1.0 - cloud * 0.48);
        float light_side = max(directional, 0.0);
        float shade_side = max(-directional, 0.0);
        result *= 1.0 + light_side * normal_strength * key_soft;
        result *= 1.0 - shade_side * normal_strength * key_soft * 0.72;

        float y = luma(result);
        float highlight_zone = smoothstep(0.40, 0.76, y);
        result += env_key_color.rgb * sun_gain * key_soft * (0.22 + highlight_zone * 0.48);
        result += vec3(0.070, 0.031, 0.001) * golden * golden_gain * highlight_zone;

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        vec3 night_result = result * vec3(0.67, 0.82, 1.08) + vec3(0.005, 0.012, 0.026);
        result = mix(result, night_result, night * 0.34);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
