shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V4.0 - three-look finishing pass: clear day, selective golden hour, readable moonlight.
// This pass intentionally avoids full-screen orange at sunset; warmth is luminance-selective.
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;

global uniform float env_time01;
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_sun_direction;
global uniform vec4 env_sun_color;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_elevation;
global uniform float env_key_strength;
global uniform float env_golden_hour;
global uniform float env_shadow_length;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_fog;
global uniform float env_cloudiness;
global uniform float env_shadow_strength;
global uniform float env_water_shimmer;
global uniform float env_water_ripple;
uniform float contrast : hint_range(0.8, 1.3) = 1.075;
uniform float day_saturation : hint_range(0.75, 1.25) = 1.025;
uniform float golden_saturation : hint_range(0.75, 1.35) = 1.085;
uniform float night_saturation : hint_range(0.6, 1.2) = 0.96;
uniform float golden_strength : hint_range(0.0, 0.5) = 0.23;
uniform float night_blue_strength : hint_range(0.0, 1.0) = 0.62;
uniform float night_exposure : hint_range(0.0, 0.5) = 0.18;
uniform float green_tame : hint_range(0.0, 0.25) = 0.075;
uniform float vignette_strength : hint_range(0.0, 0.15) = 0.030;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 src = textureLod(screen_texture, SCREEN_UV, 0.0);
    vec3 c = src.rgb;
    float night = clamp(env_night, 0.0, 1.0);
    float day = clamp(env_daylight, 0.0, 1.0);
    float golden = clamp(env_golden_hour, 0.0, 1.0);

    c = (c - 0.5) * contrast + 0.5;
    c = max(c, vec3(0.0));

    // Tame fluorescent green while preserving saturated foliage. This targets the current Field 1 palette,
    // where global light tends to push G much harder than R/B.
    float green_excess = max(c.g - max(c.r, c.b), 0.0);
    c.g -= green_excess * green_tame * day;
    c.r += green_excess * green_tame * day * 0.14;

    float y = luma(c);
    float saturation = mix(day_saturation, night_saturation, night);
    saturation = mix(saturation, golden_saturation, golden * day);
    c = mix(vec3(y), c, saturation);

    // Warm only bright/mid-bright values at golden hour; shadows retain cool green separation.
    float hi = smoothstep(0.30, 0.82, y);
    float mid = smoothstep(0.18, 0.48, y) * (1.0 - smoothstep(0.72, 0.94, y));
    c += vec3(0.16, 0.070, -0.010) * hi * golden * golden_strength;
    c += vec3(0.055, 0.018, -0.008) * mid * golden * golden_strength * 0.40;
    c *= mix(vec3(1.0), vec3(0.94, 0.975, 1.025), (1.0 - hi) * golden * 0.075);

    // Readable blue moonlight: lift midtones before tinting, so night does not collapse to green-black.
    if (night > 0.001) {
        float gamma = mix(1.0, 0.80, night * night_exposure / 0.5);
        vec3 lifted = pow(max(c, vec3(0.0001)), vec3(gamma));
        float ny = luma(lifted);
        vec3 moon_chroma = mix(lifted, vec3(ny) * vec3(0.55, 0.82, 1.18), night_blue_strength);
        moon_chroma += vec3(0.006, 0.015, 0.034) * night;
        c = mix(c, moon_chroma, night * 0.78);
    }

    float weather = clamp(env_cloudiness * 0.14 + env_fog * 0.28 + env_rain * 0.12, 0.0, 0.35);
    c = mix(c, vec3(luma(c)) * vec3(0.92, 0.98, 1.03), weather * 0.20);

    // Very soft focus vignette, mostly to keep the forest border from reading like a flat green frame.
    vec2 p = SCREEN_UV * 2.0 - 1.0;
    float vignette = smoothstep(0.42, 1.28, dot(p, p));
    c *= 1.0 - vignette * vignette_strength;

    COLOR = vec4(clamp(c, vec3(0.0), vec3(1.0)), src.a);
}
