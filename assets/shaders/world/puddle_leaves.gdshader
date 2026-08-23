shader_type canvas_item;

// Ashes Environment V4.0 - small wet vegetation / lily-pad response.
uniform float env_time01 = 0.5;
uniform float env_daylight = 1.0;
uniform float env_night = 0.0;
uniform vec2 env_sun_direction = vec2(-0.447214, 0.894427);
uniform vec4 env_sun_color : source_color = vec4(1.0);
uniform vec2 env_key_direction = vec2(0.0, 1.0);
uniform vec4 env_key_color : source_color = vec4(1.0);
uniform float env_key_elevation = 1.0;
uniform float env_key_strength = 1.0;
uniform float env_golden_hour = 0.0;
uniform float env_shadow_length = 0.0;
uniform float env_wind = 0.32;
uniform float env_rain = 0.0;
uniform float env_wetness = 0.0;
uniform float env_fog = 0.0;
uniform float env_cloudiness = 0.1;
uniform float env_shadow_strength = 0.55;
uniform float env_water_shimmer = 0.18;
uniform float env_water_ripple = 0.0;

uniform float wet_highlight : hint_range(0.0, 1.0) = 0.12;
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
