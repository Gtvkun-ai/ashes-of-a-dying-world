shader_type canvas_item;

// Ashes Environment V5.3 - authored grass skin.
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

uniform float macro_strength : hint_range(0.0, 0.20) = 0.065;
uniform float micro_strength : hint_range(0.0, 0.12) = 0.032;
uniform float contact_strength : hint_range(0.0, 0.30) = 0.095;
uniform float contact_coolth : hint_range(0.0, 0.20) = 0.045;
uniform float normal_strength : hint_range(0.0, 0.20) = 0.055;
uniform float sun_gain : hint_range(0.0, 0.20) = 0.042;
uniform float golden_gain : hint_range(0.0, 0.25) = 0.075;
uniform float wet_darken : hint_range(0.0, 0.20) = 0.050;

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

        float macro = (texture(macro_mask, UV).r - 0.5) * 2.0;
        float micro = (texture(micro_detail, UV).r - 0.5) * 2.0;
        float contact = texture(contact_mask, UV).r;

        result *= 1.0 + macro * macro_strength;
        result *= 1.0 + micro * micro_strength;

        // Static integration around path / cliff / pond. This replaces legacy full-map overlays.
        result *= 1.0 - contact * contact_strength;
        result = mix(result, result * vec3(0.93, 0.98, 1.035), contact * contact_coolth);

        // Very soft authored normal response: visible as volume, never as shiny grass.
        vec3 n = texture(normal_soft, UV).rgb * 2.0 - 1.0;
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float directional = dot(n.xy, to_key);
        float key_soft = key * day * (1.0 - cloud * 0.45);
        result *= 1.0 + directional * normal_strength * key_soft;

        float y = luma(result);
        float highlight_zone = smoothstep(0.34, 0.72, y);
        result += env_key_color.rgb * sun_gain * key_soft * (0.35 + highlight_zone * 0.65);
        result += vec3(0.085, 0.040, 0.002) * golden * golden_gain * highlight_zone;

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        vec3 night_result = result * vec3(0.67, 0.82, 1.08) + vec3(0.005, 0.012, 0.026);
        result = mix(result, night_result, night * 0.34);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
