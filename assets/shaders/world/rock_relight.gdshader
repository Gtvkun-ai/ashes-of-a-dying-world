shader_type canvas_item;

// Ashes Environment V4.0 - compact pseudo-normal rock material.
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
uniform float bump_pixels : hint_range(1.0, 8.0) = 3.0;
uniform float relight_strength : hint_range(0.0, 1.8) = 0.82;
uniform float highlight_gain : hint_range(0.0, 0.55) = 0.22;
uniform float shade_gain : hint_range(0.0, 0.55) = 0.18;
uniform float cavity_gain : hint_range(0.0, 0.35) = 0.11;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec2 px = TEXTURE_PIXEL_SIZE * bump_pixels;
        float y0 = luma(base.rgb);
        float hl = luma(texture(TEXTURE, UV - vec2(px.x, 0.0)).rgb);
        float hr = luma(texture(TEXTURE, UV + vec2(px.x, 0.0)).rgb);
        float hu = luma(texture(TEXTURE, UV - vec2(0.0, px.y)).rgb);
        float hd = luma(texture(TEXTURE, UV + vec2(0.0, px.y)).rgb);
        vec2 pseudo_n = normalize(vec2(hl - hr, hu - hd) + vec2(0.0001));
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float side = clamp(dot(pseudo_n, to_key), -1.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float day = clamp(env_daylight, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);
        float artistic_key = mix(key, max(key, 0.76), golden);
        float response = relight_strength * artistic_key * (1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.30);

        float local_mean = (hl + hr + hu + hd) * 0.25;
        float cavity = smoothstep(0.025, 0.16, local_mean - y0);
        result *= 1.0 - cavity * cavity_gain;
        result *= 1.0 + max(side, 0.0) * highlight_gain * response * day;
        result *= 1.0 - max(-side, 0.0) * shade_gain * response * day;
        result += env_key_color.rgb * max(side, 0.0) * response * day * (0.022 + golden * 0.070);

        float night = clamp(env_night, 0.0, 1.0);
        vec3 moon = result * vec3(0.60, 0.72, 1.02) + vec3(0.006, 0.010, 0.022);
        moon += env_key_color.rgb * max(side, 0.0) * response * night * 0.030;
        result = mix(result, moon, night * 0.48);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
