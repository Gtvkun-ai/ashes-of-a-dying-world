shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.6 - unified directional rock normal/AO response.
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
uniform float normal_strength : hint_range(0.0, 0.60) = 0.22;
uniform float ao_strength : hint_range(0.0, 0.60) = 0.32;
uniform float highlight_gain : hint_range(0.0, 0.30) = 0.040;
uniform float golden_gain : hint_range(0.0, 0.30) = 0.050;
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

        vec3 n = texture(normal_map, UV).rgb * 2.0 - 1.0;
        n = normalize(vec3(n.xy, max(n.z, 0.26)));
        float ao = texture(ao_map, UV).r;
        float cavity = 1.0 - clamp(ao, 0.0, 1.0);
        result *= 1.0 - cavity * ao_strength;

        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float directional = clamp(dot(n.xy, to_key) * 1.22, -1.0, 1.0);
        float key_soft = key * day * (1.0 - cloud * 0.32);
        float light_side = smoothstep(-0.14, 0.76, directional);
        float shade_side = smoothstep(0.02, 0.82, -directional);
        float top_light = smoothstep(0.36, 0.92, n.z) * clamp(env_key_elevation, 0.0, 1.0);

        result *= 1.0 + light_side * normal_strength * key_soft;
        result *= 1.0 - shade_side * normal_strength * key_soft * 0.72;
        result *= 1.0 + top_light * normal_strength * key_soft * 0.18;
        result = mix(result, result * vec3(0.88, 0.92, 0.96), shade_side * key_soft * 0.16);
        result += env_key_color.rgb * (light_side * 0.80 + top_light * 0.20) * highlight_gain * key_soft;
        result += vec3(0.085, 0.038, 0.002) * golden * golden_gain * max(light_side, 0.15) * (1.0 + clamp(env_shadow_length, 0.0, 1.0) * 0.18);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        vec3 night_result = result * vec3(0.62, 0.72, 0.98) + vec3(0.004, 0.008, 0.018);
        result = mix(result, night_result, night * 0.50);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
