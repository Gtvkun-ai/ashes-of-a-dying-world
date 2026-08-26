shader_type canvas_item;
render_mode unshaded, blend_mul;

// Ashes Environment V7.0 - broad world-space cloud light / shadow pass.
//
// ART CONTRACT:
// - This pass is dynamic atmosphere, NOT terrain composition.
// - Shapes are deliberately huge and low-frequency so they read as cloud masses,
//   never as grass/noise texture.
// - WorldCloudShadow2D.cs owns the random weather-cell drift. The shader only
//   evaluates a stable world-space field at the supplied flow offset.
// - Output is multiplicative and may go slightly above 1.0 in open-sun gaps.

global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_key_direction;
global uniform float env_key_strength;
global uniform float env_shadow_length;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_fog;
global uniform float env_cloudiness;

// Final local contrast. Keep these restrained: authored map lighting remains the base.
uniform float max_shadow_strength : hint_range(0.0, 0.24) = 0.158;
uniform float max_sun_gain : hint_range(0.0, 0.12) = 0.082;

// 0.0011 ~= cloud features hundreds of world pixels wide on Field 01.
uniform float world_scale : hint_range(0.00025, 0.004, 0.00005) = 0.00078;
uniform float shadow_softness : hint_range(0.01, 0.12) = 0.050;
uniform float fair_weather_activity : hint_range(0.0, 0.30) = 0.170;

// Runtime-owned controls. flow_offset is integrated on CPU so changing wind direction
// does not make the pattern jump (TIME * changing_direction would jump visibly).
uniform vec2 flow_offset = vec2(0.0);
uniform vec2 seed_offset = vec2(17.0, 41.0);
uniform float density_bias : hint_range(-0.16, 0.16) = 0.0;
uniform float sun_open_boost : hint_range(0.5, 1.5) = 1.08;

varying vec2 world_pos;

float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x),
        u.y
    );
}

// Only a few octaves: enough organic contour, not enough micro-noise to become texture.
float broad_fbm(vec2 p) {
    float value = 0.0;
    float amp = 0.64;
    value += amp * vnoise(p);
    p = p * 1.91 + vec2(5.13, -3.71);
    amp *= 0.46;
    value += amp * vnoise(p);
    p = p * 1.83 + vec2(-2.67, 4.29);
    amp *= 0.46;
    value += amp * vnoise(p);
    return value / 1.0705;
}

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
    float day = clamp(env_daylight, 0.0, 1.0);
    float night = clamp(env_night, 0.0, 1.0);
    float key = clamp(env_key_strength, 0.0, 1.0);
    float weather_cloud = clamp(env_cloudiness, 0.0, 1.0);

    // Clear weather may still carry a few slow fair-weather clouds. This does NOT
    // globally darken the scene; it only lets sparse moving cloud masses exist.
    float coverage = 1.0 - (1.0 - weather_cloud) * (1.0 - fair_weather_activity);
    coverage = clamp(coverage + density_bias, 0.0, 1.0);

    vec3 multiplier = vec3(1.0);
    if (day > 0.015 && coverage > 0.025) {
        vec2 p = world_pos * world_scale + flow_offset + seed_offset;

        // Very low-frequency domain warp. The warp breaks obvious round blobs while
        // preserving cloud masses at 300-1000+ world-pixel scale.
        vec2 warp = vec2(
            vnoise(p * 0.47 + vec2(13.7, -9.2)),
            vnoise(p * 0.43 + vec2(-7.1, 16.3))
        ) - vec2(0.5);
        p += warp * 0.72;

        float near_mass = broad_fbm(p);
        float far_mass = broad_fbm(p * 0.53 + vec2(-11.4, 8.6));
        float field = mix(near_mass, far_mass, 0.43);

        // More cloudiness lowers the threshold -> larger connected shadow masses.
        float threshold = mix(0.360, 0.180, coverage);
        float soft = shadow_softness;
        float shadow_mask = smoothstep(threshold - soft, threshold + soft, field);

        // Open-sun gaps use the opposite broad field, not a high-frequency dapple mask.
        // They are strongest with broken cloud cover and disappear toward overcast/night.
        float open_threshold = mix(0.235, 0.190, coverage);
        float open_mask = 1.0 - smoothstep(open_threshold - soft * 1.15, open_threshold + soft * 1.15, field);
        float broken_sky = smoothstep(0.055, 0.20, coverage) * (1.0 - smoothstep(0.72, 0.98, coverage));

        float low_sun = clamp(env_shadow_length, 0.0, 1.0);
        float direct_visibility = day * mix(0.58, 1.0, key);
        float rain_soften = 1.0 - clamp(env_rain, 0.0, 1.0) * 0.38;
        float fog_soften = 1.0 - clamp(env_fog, 0.0, 1.0) * 0.42;

        float shadow_strength = shadow_mask
            * max_shadow_strength
            * direct_visibility
            * mix(0.78, 1.10, coverage)
            * mix(1.0, 1.08, low_sun)
            * rain_soften
            * fog_soften;

        float sun_strength = open_mask
            * max_sun_gain
            * direct_visibility
            * broken_sky
            * (1.0 - weather_cloud * 0.58)
            * sun_open_boost;

        // Cooler cloud shade; warm/yellow-biased open sun. Values are multipliers.
        multiplier -= shadow_strength * vec3(1.00, 0.78, 0.42);
        multiplier += sun_strength * vec3(1.00, 0.84, 0.36);
    }

    // Keep nighttime exactly neutral; day fade above already handles dawn/dusk.
    multiplier = mix(multiplier, vec3(1.0), night * 0.92);
    COLOR = vec4(max(multiplier, vec3(0.72)), 1.0);
}
