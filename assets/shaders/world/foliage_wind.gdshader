shader_type canvas_item;

// Ashes Environment V1.3.
// Local uniform contract: no Project Settings Shader Globals required.
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
uniform float env_water_shimmer = 0.12;
uniform float env_water_ripple = 0.0;

uniform float max_bend_pixels : hint_range(0.0, 12.0) = 1.25;
uniform float sway_speed : hint_range(0.0, 4.0) = 1.0;
uniform float anchor_y : hint_range(0.25, 1.0) = 0.94;
uniform float gust_strength : hint_range(0.0, 1.0) = 0.28;
uniform float rain_stiffness : hint_range(0.0, 1.0) = 0.28;

void vertex() {
    // UV.y=1 là gốc sprite. Bình phương mask giữ thân/gốc rất chắc.
    float crown = clamp((anchor_y - UV.y) / max(anchor_y, 0.001), 0.0, 1.0);
    crown *= crown;

    vec2 world_origin = MODEL_MATRIX[3].xy;
    float phase = world_origin.x * 0.012 + world_origin.y * 0.0067;

    float base_wave = sin(TIME * sway_speed + phase);
    base_wave += sin(TIME * sway_speed * 2.17 + phase * 1.71) * 0.30;

    float gust = 0.5 + 0.5 * sin(TIME * 0.23 + phase * 0.37);
    float gust_gain = mix(1.0, 1.0 + gust, gust_strength);
    float rain_damp = mix(1.0, 1.0 - rain_stiffness, clamp(env_rain, 0.0, 1.0));

    // Failsafe 0.16 làm foliage vẫn có hơi thở nếu service chưa bind trong frame đầu.
    float effective_wind = max(env_wind, 0.16);
    float bend = base_wave * effective_wind * gust_gain * rain_damp;

    VERTEX.x += bend * max_bend_pixels * crown;
}
