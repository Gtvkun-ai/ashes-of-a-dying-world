shader_type canvas_item;

// Ashes Environment V5.2 - authored rock normal/AO response.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_strength;
global uniform float env_golden_hour;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;

uniform sampler2D normal_map : filter_linear, repeat_disable;
uniform sampler2D ao_map : filter_linear, repeat_disable;
uniform float normal_strength : hint_range(0.0, 0.60) = 0.20;
uniform float ao_strength : hint_range(0.0, 0.60) = 0.26;
uniform float highlight_gain : hint_range(0.0, 0.30) = 0.055;
uniform float golden_gain : hint_range(0.0, 0.30) = 0.070;
uniform float wet_darken : hint_range(0.0, 0.30) = 0.040;

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

        vec3 n = texture(normal_map, UV).rgb * 2.0 - 1.0;
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float directional = dot(n.xy, to_key);
        float key_soft = key * day * (1.0 - cloud * 0.32);
        result *= 1.0 + directional * normal_strength * key_soft;
        result += env_key_color.rgb * max(directional, 0.0) * highlight_gain * key_soft;
        result += vec3(0.085, 0.038, 0.002) * golden * golden_gain * max(directional, 0.15);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        vec3 night_result = result * vec3(0.62, 0.72, 0.98) + vec3(0.004, 0.008, 0.018);
        result = mix(result, night_result, night * 0.50);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
