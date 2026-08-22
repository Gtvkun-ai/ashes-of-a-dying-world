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
