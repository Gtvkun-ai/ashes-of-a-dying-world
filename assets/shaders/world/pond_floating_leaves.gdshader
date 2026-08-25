shader_type canvas_item;
render_mode unshaded;

// Subtle wet highlight for pond leaf overlays.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_elevation;
global uniform float env_golden_hour;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;

uniform float wet_highlight : hint_range(0.0, 1.0) = 0.08;
uniform float pulse_speed : hint_range(0.0, 3.0) = 0.42;

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        float pulse = 0.5 + 0.5 * sin(TIME * pulse_speed + UV.x * 23.0 + UV.y * 17.0);
        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        vec2 pseudo_n = normalize((UV - vec2(0.5, 0.60)) * vec2(1.7, 1.1) + vec2(0.0001));
        float lit = smoothstep(-0.10, 0.80, dot(pseudo_n, to_key));
        float glint = pulse * max(wet, 0.16) * (1.0 - env_cloudiness * 0.45) * wet_highlight;

        result *= 1.0 + lit * day * clamp(env_key_elevation, 0.0, 1.0) * 0.025;
        result *= 1.0 - (1.0 - lit) * day * 0.030;
        result += env_key_color.rgb * glint * day * (0.08 + golden * 0.15) * (0.55 + lit * 0.45);
        vec3 moon = result * vec3(0.62, 0.82, 1.05) + env_key_color.rgb * glint * night * 0.08;
        result = mix(result, moon, night * 0.42);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
