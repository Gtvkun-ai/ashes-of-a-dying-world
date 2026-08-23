shader_type canvas_item;

uniform float env_time01 = 0.5;
uniform float env_daylight = 1.0;
uniform float env_night = 0.0;
uniform vec2 env_sun_direction = vec2(-0.447214, 0.894427);
uniform vec4 env_sun_color : source_color = vec4(1.0);
uniform float env_wind = 0.32;
uniform float env_rain = 0.0;
uniform float env_wetness = 0.0;
uniform float env_fog = 0.0;
uniform float env_cloudiness = 0.1;
uniform float env_shadow_strength = 0.55;
uniform float env_water_shimmer = 0.18;
uniform float env_water_ripple = 0.0;

uniform float wet_highlight : hint_range(0.0, 1.0) = 0.10;
uniform float pulse_speed : hint_range(0.0, 3.0) = 0.55;

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        float pulse = 0.5 + 0.5 * sin(TIME * pulse_speed + UV.x * 23.0 + UV.y * 17.0);
        float sky = env_daylight * (1.0 - env_cloudiness * 0.55);
        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        float glint = pulse * wet * sky * wet_highlight;

        result = base.rgb + env_sun_color.rgb * glint * 0.12;
        result = mix(result, result * vec3(0.88, 0.94, 1.04), env_night * 0.10);
    }

    COLOR = vec4(result, base.a);
}
