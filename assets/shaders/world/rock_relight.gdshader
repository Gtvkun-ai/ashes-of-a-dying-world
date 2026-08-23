shader_type canvas_item;

// Ashes Environment V3.3 - Compatibility-safe rock relighting.
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
uniform float env_water_shimmer = 0.18;
uniform float env_water_ripple = 0.0;

uniform float bump_pixels : hint_range(1.0, 8.0) = 3.0;
uniform float relight_strength : hint_range(0.0, 1.5) = 0.62;
uniform float highlight_gain : hint_range(0.0, 0.5) = 0.18;
uniform float shade_gain : hint_range(0.0, 0.5) = 0.15;

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

        vec2 to_sun = length(env_sun_direction) > 0.0001
            ? normalize(-env_sun_direction)
            : vec2(0.0, -1.0);
        float side = clamp(dot(pseudo_n, to_sun), -1.0, 1.0);
        float sun_height = smoothstep(0.45, 0.97, abs(normalize(env_sun_direction).y));
        float response = relight_strength * clamp(env_daylight, 0.0, 1.0)
            * (1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.30)
            * mix(0.58, 1.0, sun_height);

        result *= 1.0 + max(side, 0.0) * highlight_gain * response;
        result *= 1.0 - max(-side, 0.0) * shade_gain * response;
        result += env_sun_color.rgb * max(side, 0.0) * response * 0.018;
        result = mix(result, result * vec3(0.80, 0.86, 1.02), clamp(env_night, 0.0, 1.0) * 0.10);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
