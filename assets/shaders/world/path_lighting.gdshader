shader_type canvas_item;

// V4 path material: preserves the authored dirt pixels while giving the path a warmer sun response
// and a readable cool night value.
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
uniform float relief_gain : hint_range(0.0, 0.25) = 0.075;
uniform float golden_gain : hint_range(0.0, 0.30) = 0.15;
uniform float night_lift : hint_range(0.0, 0.25) = 0.075;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec2 px = TEXTURE_PIXEL_SIZE * 1.6;
        float y0 = luma(base.rgb);
        float yl = luma(texture(TEXTURE, UV - vec2(px.x, 0.0)).rgb);
        float yr = luma(texture(TEXTURE, UV + vec2(px.x, 0.0)).rgb);
        float yu = luma(texture(TEXTURE, UV - vec2(0.0, px.y)).rgb);
        float yd = luma(texture(TEXTURE, UV + vec2(0.0, px.y)).rgb);
        float local_mean = (yl + yr + yu + yd) * 0.25;
        float relief = clamp(y0 - local_mean, -0.20, 0.20);
        result *= 1.0 + relief * relief_gain * 3.0;

        float day = clamp(env_daylight, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);
        float hi = smoothstep(0.34, 0.72, y0);
        result += env_key_color.rgb * hi * day * key * (0.025 + golden * golden_gain);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * 0.055;

        float night = clamp(env_night, 0.0, 1.0);
        vec3 cool = result * vec3(0.72, 0.82, 1.02) + vec3(0.010, 0.016, 0.025) * night_lift / 0.25;
        result = mix(result, cool, night * 0.36);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
