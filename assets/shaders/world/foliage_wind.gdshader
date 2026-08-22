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

uniform float max_bend_pixels : hint_range(0.0, 8.0) = 1.25;
uniform float sway_speed : hint_range(0.0, 4.0) = 1.0;
uniform float anchor_y : hint_range(0.0, 1.0) = 0.92;

void vertex() {
    // UV.y gần anchor_y là gốc cây/cỏ: giữ gần như đứng yên, chỉ phần ngọn bị bẻ.
    float crown = clamp((anchor_y - UV.y) / max(anchor_y, 0.001), 0.0, 1.0);
    crown *= crown;

    float sway = sin(TIME * sway_speed + UV.y * 2.1);
    sway += sin(TIME * sway_speed * 2.17 + UV.x * 3.7) * 0.28;

    VERTEX.x += sway * max_bend_pixels * env_wind * crown;
}
