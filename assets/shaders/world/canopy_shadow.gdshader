shader_type canvas_item;

global uniform float env_time01;
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_sun_direction;
global uniform vec4 env_sun_color;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_fog;
global uniform float env_cloudiness;
global uniform float env_shadow_strength;

uniform float breathe_strength : hint_range(0.0, 0.2) = 0.035;

void fragment() {
    vec4 base = COLOR;
    float breeze = sin(TIME * (0.45 + env_wind * 0.4) + UV.x * 15.0 + UV.y * 9.0);
    float cloud_soften = 1.0 - env_cloudiness * 0.35;
    float strength = env_shadow_strength * env_daylight * cloud_soften;
    strength *= 1.0 + breeze * breathe_strength;

    COLOR = vec4(base.rgb * mix(1.0, 0.82, strength), base.a * mix(0.38, 1.0, strength));
}
