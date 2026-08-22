shader_type canvas_item;

// Ashes Environment Core V1.1: state comes from project-wide global shader uniforms.
// res://override.cfg declares these before shaders are compiled.
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

uniform float shimmer_strength : hint_range(0.0, 1.0) = 0.12;
uniform float ripple_strength : hint_range(0.0, 1.0) = 0.08;
uniform float animation_speed : hint_range(0.0, 4.0) = 0.7;
uniform float pixel_block : hint_range(1.0, 8.0, 1.0) = 2.0;

float soft_band(float value, float width) {
    return 1.0 - smoothstep(0.0, width, abs(value));
}

void fragment() {
    vec4 base = COLOR;
    if (base.a <= 0.001) {
        COLOR = base;
        return;
    }

    // Quantize the effect sampling, not the source texture. The puddle silhouette stays pixel-perfect.
    vec2 block_uv = TEXTURE_PIXEL_SIZE * max(pixel_block, 1.0);
    vec2 q = floor(UV / block_uv) * block_uv;

    float wind_speed = animation_speed * (0.75 + env_wind * 0.35);
    float wave_a = sin((q.x * 34.0 + q.y * 21.0) + TIME * wind_speed * 2.0);
    float wave_b = sin((q.x * -18.0 + q.y * 41.0) - TIME * wind_speed * 1.35);
    float shimmer = soft_band(wave_a + wave_b * 0.45, 0.34);

    // Rain does not warp the artwork. It only adds small moving highlight rings/bands.
    float rain_phase = sin(length(q - vec2(0.63, 0.47)) * 95.0 - TIME * 5.0);
    float rain_ring = soft_band(rain_phase, 0.16) * env_rain;

    float sky_visibility = clamp(env_daylight * (1.0 - env_cloudiness * 0.55) + env_night * 0.18, 0.0, 1.0);
    float wet_boost = mix(0.65, 1.15, clamp(env_wetness, 0.0, 1.0));
    float highlight = shimmer * shimmer_strength * sky_visibility * wet_boost;
    highlight += rain_ring * ripple_strength;

    vec3 sun_tint = mix(vec3(0.72, 0.83, 1.0), env_sun_color.rgb, env_daylight);
    vec3 result = base.rgb + sun_tint * highlight * 0.16;

    // A tiny cool lift at night keeps the puddle readable without glowing like radioactive soup.
    result += vec3(0.02, 0.035, 0.055) * env_night * (0.35 + shimmer * 0.25);

    COLOR = vec4(result, base.a);
}
