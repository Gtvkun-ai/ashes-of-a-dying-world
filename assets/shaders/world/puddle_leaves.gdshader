shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.4 - restrained wet vegetation / lily-pad response.
global uniform float env_time01;
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_sun_direction;
global uniform vec4 env_sun_color;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_elevation;
global uniform float env_key_strength;
global uniform float env_golden_hour;
global uniform float env_shadow_length;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_fog;
global uniform float env_cloudiness;
global uniform float env_shadow_strength;
global uniform float env_water_shimmer;
global uniform float env_water_ripple;
uniform float wet_highlight : hint_range(0.0, 1.0) = 0.09;
uniform float pulse_speed : hint_range(0.0, 3.0) = 0.50;

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        float pulse = 0.5 + 0.5 * sin(TIME * pulse_speed + UV.x * 23.0 + UV.y * 17.0);
        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        float glint = pulse * max(wet, 0.18) * (1.0 - env_cloudiness * 0.45) * wet_highlight;

        result += env_key_color.rgb * glint * day * (0.10 + golden * 0.18);
        vec3 moon = result * vec3(0.62, 0.82, 1.05) + env_key_color.rgb * glint * night * 0.10;
        result = mix(result, moon, night * 0.42);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
