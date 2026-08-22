shader_type canvas_item;

uniform float env_time01 = 0.5;
uniform float env_daylight = 1.0;
uniform float env_night = 0.0;
uniform vec2 env_sun_direction = vec2(-0.45, 0.9);
uniform vec4 env_sun_color : source_color = vec4(1.0);
uniform float env_wind = 0.2;
uniform float env_rain = 0.0;
uniform float env_wetness = 0.0;
uniform float env_fog = 0.0;
uniform float env_cloudiness = 0.1;

uniform float shadow_strength : hint_range(0.0, 1.0) = 0.55;
uniform float breathe_strength : hint_range(0.0, 0.2) = 0.035;

void fragment() {
    vec4 base = COLOR;
    float breeze = sin(TIME * (0.45 + env_wind * 0.4) + UV.x * 15.0 + UV.y * 9.0);
    float cloud_soften = 1.0 - env_cloudiness * 0.35;
    float strength = shadow_strength * env_daylight * cloud_soften;
    strength *= 1.0 + breeze * breathe_strength;

    COLOR = vec4(base.rgb * mix(1.0, 0.82, strength), base.a * mix(0.38, 1.0, strength));
}
