shader_type canvas_item;

// Leaves stay geometrically fixed so they remain aligned to the painted puddle.
// Only their wet surface response changes with daylight/rain.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec4 env_sun_color;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;

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
