shader_type canvas_item;
render_mode unshaded;

// V5.4 restrained trunk/wood relight. Tiny material, but it matters because bright roots are a major cue in the target art.
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
uniform float bump_pixels : hint_range(1.0, 6.0) = 2.0;
uniform float highlight_gain : hint_range(0.0, 0.50) = 0.12;
uniform float shade_gain : hint_range(0.0, 0.50) = 0.12;
uniform float root_ao : hint_range(0.0, 0.30) = 0.11;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec2 px = TEXTURE_PIXEL_SIZE * bump_pixels;
        float hl = luma(texture(TEXTURE, UV - vec2(px.x, 0.0)).rgb);
        float hr = luma(texture(TEXTURE, UV + vec2(px.x, 0.0)).rgb);
        float hu = luma(texture(TEXTURE, UV - vec2(0.0, px.y)).rgb);
        float hd = luma(texture(TEXTURE, UV + vec2(0.0, px.y)).rgb);
        vec2 pseudo_n = normalize(vec2(hl - hr, hu - hd) + vec2(0.0001));
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float side = clamp(dot(pseudo_n, to_key), -1.0, 1.0);
        float response = clamp(env_key_strength, 0.0, 1.0) * (1.0 - env_cloudiness * 0.30);
        float golden = clamp(env_golden_hour, 0.0, 1.0);

        result *= 1.0 + max(side, 0.0) * highlight_gain * response;
        result *= 1.0 - max(-side, 0.0) * shade_gain * response;
        result += env_key_color.rgb * max(side, 0.0) * response * (0.012 + golden * 0.036);

        float root = smoothstep(0.62, 1.0, UV.y);
        result *= 1.0 - root * root_ao;

        float night = clamp(env_night, 0.0, 1.0);
        result = mix(result, result * vec3(0.64, 0.73, 0.94) + vec3(0.004, 0.008, 0.016), night * 0.32);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
