shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.4 - restrained finishing pass.
// The grade now compresses fluorescent greens and contrast instead of trying to create depth globally.
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

uniform float contrast : hint_range(0.8, 1.3) = 1.015;
uniform float day_saturation : hint_range(0.75, 1.25) = 0.94;
uniform float golden_saturation : hint_range(0.75, 1.35) = 0.99;
uniform float night_saturation : hint_range(0.6, 1.2) = 0.92;
uniform float golden_strength : hint_range(0.0, 0.5) = 0.14;
uniform float night_blue_strength : hint_range(0.0, 1.0) = 0.58;
uniform float night_exposure : hint_range(0.0, 0.5) = 0.16;
uniform float green_tame : hint_range(0.0, 0.25) = 0.14;
uniform float vignette_strength : hint_range(0.0, 0.15) = 0.018;
uniform float cohesion_strength : hint_range(0.0, 0.25) = 0.08;
uniform vec3 cohesion_tint : source_color = vec3(0.94, 0.985, 0.86);

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 src = textureLod(screen_texture, SCREEN_UV, 0.0);
    vec3 c = src.rgb;
    float night = clamp(env_night, 0.0, 1.0);
    float day = clamp(env_daylight, 0.0, 1.0);
    float golden = clamp(env_golden_hour, 0.0, 1.0);
    float key = clamp(env_key_strength, 0.0, 1.0);
    float elevation = clamp(env_key_elevation, 0.0, 1.0);
    float low_sun = clamp(env_shadow_length, 0.0, 1.0);

    c = (c - 0.5) * contrast + 0.5;
    c = max(c, vec3(0.0));

    // Target only green dominance. Keep the hue green, but remove the lime/neon spike.
    float green_excess = max(c.g - max(c.r, c.b), 0.0);
    float green_trim = green_excess * green_tame * day;
    c.g -= green_trim;
    c.r += green_trim * 0.12;
    c.b += green_trim * 0.025;

    float y = luma(c);
    float mid_band = smoothstep(0.10, 0.44, y) * (1.0 - smoothstep(0.86, 1.0, y));
    vec3 cohesive = c * cohesion_tint + vec3(0.006, 0.004, -0.003) * day;
    c = mix(c, cohesive, cohesion_strength * day * mid_band);
    y = luma(c);

    vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
    vec2 p_dir = SCREEN_UV - vec2(0.5);
    float key_side = dot(p_dir, to_key);
    float directional_warm = smoothstep(-0.30, 0.72, key_side) * day * key * (0.40 + elevation * 0.60);
    float directional_cool = smoothstep(-0.05, 0.78, -key_side) * day * key * (0.35 + low_sun * 0.65);
    c += env_key_color.rgb * directional_warm * 0.013;
    c *= mix(vec3(1.0), vec3(0.955, 0.980, 1.020), directional_cool * 0.034);

    float saturation = mix(day_saturation, night_saturation, night);
    saturation = mix(saturation, golden_saturation, golden * day);
    c = mix(vec3(y), c, saturation);

    // Warm only the readable upper values; no orange veil over the whole scene.
    float hi = smoothstep(0.34, 0.84, y);
    float mid = smoothstep(0.22, 0.50, y) * (1.0 - smoothstep(0.70, 0.94, y));
    c += vec3(0.13, 0.052, -0.008) * hi * golden * golden_strength;
    c += vec3(0.040, 0.012, -0.005) * mid * golden * golden_strength * 0.34;
    c *= mix(vec3(1.0), vec3(0.96, 0.982, 1.018), (1.0 - hi) * golden * 0.055);

    if (night > 0.001) {
        float gamma = mix(1.0, 0.82, night * night_exposure / 0.5);
        vec3 lifted = pow(max(c, vec3(0.0001)), vec3(gamma));
        float ny = luma(lifted);
        vec3 moon_chroma = mix(lifted, vec3(ny) * vec3(0.56, 0.82, 1.16), night_blue_strength);
        moon_chroma += vec3(0.006, 0.015, 0.032) * night;
        c = mix(c, moon_chroma, night * 0.76);
    }

    float weather = clamp(env_cloudiness * 0.14 + env_fog * 0.28 + env_rain * 0.12, 0.0, 0.35);
    c = mix(c, vec3(luma(c)) * vec3(0.93, 0.985, 1.025), weather * 0.18);

    vec2 p = SCREEN_UV * 2.0 - 1.0;
    float vignette = smoothstep(0.46, 1.30, dot(p, p));
    c *= 1.0 - vignette * vignette_strength;

    COLOR = vec4(clamp(c, vec3(0.0), vec3(1.0)), src.a);
}
