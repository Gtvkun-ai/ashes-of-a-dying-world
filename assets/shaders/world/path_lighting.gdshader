shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.4 - restrained authored dirt path response.
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
uniform float normal_strength : hint_range(0.0, 0.40) = 0.12;
uniform float ao_strength : hint_range(0.0, 0.50) = 0.19;
uniform float sun_gain : hint_range(0.0, 0.20) = 0.020;
uniform float golden_gain : hint_range(0.0, 0.30) = 0.065;
uniform float wet_darken : hint_range(0.0, 0.30) = 0.075;

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
        float key_soft = key * day * (1.0 - cloud * 0.36);
        result *= 1.0 + directional * normal_strength * key_soft;
        result += env_key_color.rgb * sun_gain * key_soft;

        result += vec3(0.10, 0.050, 0.004) * golden * golden_gain;

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        vec3 night_result = result * vec3(0.66, 0.76, 0.98) + vec3(0.006, 0.010, 0.020);
        result = mix(result, night_result, night * 0.42);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
