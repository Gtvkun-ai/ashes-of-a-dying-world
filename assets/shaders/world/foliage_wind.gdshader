shader_type canvas_item;

// Ashes Environment V1.2.
// Wind state is global; per-material values only describe how a specific plant reacts.
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

uniform float max_bend_pixels : hint_range(0.0, 12.0) = 1.25;
uniform float sway_speed : hint_range(0.0, 4.0) = 1.0;
uniform float anchor_y : hint_range(0.25, 1.0) = 0.94;
uniform float gust_strength : hint_range(0.0, 1.0) = 0.28;
uniform float rain_stiffness : hint_range(0.0, 1.0) = 0.28;

void vertex() {
    // UV.y = 1 is the base for upright sprites. Squaring the mask keeps the trunk/stem stable.
    float crown = clamp((anchor_y - UV.y) / max(anchor_y, 0.001), 0.0, 1.0);
    crown *= crown;

    // Borrowed architectural idea from WeatherSystem2D: world position de-syncs every prop.
    // This avoids an entire forest swaying in military formation while keeping zero scripts per instance.
    vec2 world_origin = MODEL_MATRIX[3].xy;
    float phase = world_origin.x * 0.012 + world_origin.y * 0.0067;

    float base_wave = sin(TIME * sway_speed + phase);
    base_wave += sin(TIME * sway_speed * 2.17 + phase * 1.71) * 0.30;

    float gust = 0.5 + 0.5 * sin(TIME * 0.23 + phase * 0.37);
    float gust_gain = mix(1.0, 1.0 + gust, gust_strength);

    // Heavy rain bends foliage less freely. Rain still raises env_wind through the weather preset,
    // so storms feel stronger without turning every stem into rubber.
    float rain_damp = mix(1.0, 1.0 - rain_stiffness, clamp(env_rain, 0.0, 1.0));
    float bend = base_wave * max(env_wind, 0.0) * gust_gain * rain_damp;

    VERTEX.x += bend * max_bend_pixels * crown;
}
