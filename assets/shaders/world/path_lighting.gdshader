shader_type canvas_item;

// V4 path material: preserves the authored dirt pixels while giving the path a warmer sun response
// and a readable cool night value.
uniform float env_time01 = 0.5;
uniform float env_daylight = 1.0;
uniform float env_night = 0.0;
uniform vec2 env_sun_direction = vec2(-0.447214, 0.894427);
uniform vec4 env_sun_color : source_color = vec4(1.0);
uniform vec2 env_key_direction = vec2(0.0, 1.0);
uniform vec4 env_key_color : source_color = vec4(1.0);
uniform float env_key_elevation = 1.0;
uniform float env_key_strength = 1.0;
uniform float env_golden_hour = 0.0;
uniform float env_shadow_length = 0.0;
uniform float env_wind = 0.32;
uniform float env_rain = 0.0;
uniform float env_wetness = 0.0;
uniform float env_fog = 0.0;
uniform float env_cloudiness = 0.1;
uniform float env_shadow_strength = 0.55;
uniform float env_water_shimmer = 0.18;
uniform float env_water_ripple = 0.0;

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
