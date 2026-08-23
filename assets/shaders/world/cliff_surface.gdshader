shader_type canvas_item;

// Ashes Environment V4.0 - plane-aware cliff/stairs material.
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
// 0 = top, 1 = vertical wall, 2 = stairs.
uniform float surface_mode : hint_range(0.0, 2.0, 1.0) = 0.0;
uniform float edge_gain : hint_range(0.0, 0.40) = 0.16;
uniform float cavity_gain : hint_range(0.0, 0.35) = 0.14;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);
        float cloud_soften = 1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.30;
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);

        float is_wall = step(0.5, surface_mode) * (1.0 - step(1.5, surface_mode));
        float is_stairs = step(1.5, surface_mode);
        float is_top = 1.0 - max(is_wall, is_stairs);

        // Stable plane separation first. This keeps the cliff readable even when the key light is overhead.
        result *= 1.0 + is_top * 0.105 * day;
        result *= 1.0 - is_wall * 0.215 * (0.70 + 0.30 * day);
        result *= 1.0 - is_stairs * 0.085 * (0.65 + 0.35 * day);

        // Texture cavities: dark cracks, keep stone highlights. Sample in fragment for Compatibility renderer.
        vec2 px = TEXTURE_PIXEL_SIZE * 1.5;
        float y0 = luma(base.rgb);
        float yl = luma(texture(TEXTURE, UV - vec2(px.x, 0.0)).rgb);
        float yr = luma(texture(TEXTURE, UV + vec2(px.x, 0.0)).rgb);
        float yu = luma(texture(TEXTURE, UV - vec2(0.0, px.y)).rgb);
        float yd = luma(texture(TEXTURE, UV + vec2(0.0, px.y)).rgb);
        float local_mean = (yl + yr + yu + yd) * 0.25;
        float cavity = smoothstep(0.025, 0.18, local_mean - y0);
        result *= 1.0 - cavity * cavity_gain * (0.35 + is_wall * 0.65);

        float top_key = smoothstep(-0.45, 0.85, -to_key.y);
        float wall_key = smoothstep(-0.72, 0.62, to_key.y);
        float artistic_key = mix(key, max(key, 0.78), golden) * cloud_soften;
        result += env_key_color.rgb * is_top * day * artistic_key * (0.030 + top_key * 0.045 + golden * 0.075);
        result += env_key_color.rgb * is_wall * day * artistic_key * wall_key * (0.010 + golden * 0.024);
        result += env_key_color.rgb * is_stairs * day * artistic_key * (0.010 + golden * 0.025);

        // Bright lip on the upper pixel edge is a cheap but very effective height cue.
        vec2 py = vec2(0.0, TEXTURE_PIXEL_SIZE.y * 1.20);
        float alpha_above = texture(TEXTURE, UV - py).a;
        float top_edge = base.a * (1.0 - alpha_above);
        result += env_key_color.rgb * top_edge * edge_gain * day * artistic_key * (is_top + is_wall * 0.34 + is_stairs * 0.30);

        // Vertical faces keep a cool bounce so shadowed rock does not become brown-black sludge.
        result = mix(result, result * vec3(0.82, 0.88, 0.98), is_wall * 0.16 + is_stairs * 0.07);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * (0.020 + is_wall * 0.030);

        vec3 night_result = result * vec3(0.60, 0.72, 0.98) + vec3(0.005, 0.010, 0.022);
        night_result += env_key_color.rgb * top_edge * night * 0.025;
        result = mix(result, night_result, night * 0.52);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
