shader_type canvas_item;

// Ashes Environment V3.3 - Compatibility-safe cliff relighting.
// Field 1 đã tách CliffTop / CliffWall / Stairs nên ta art-direct từng mặt thay vì đoán 3D từ một ảnh.
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

// 0 = top, 1 = vertical wall, 2 = stairs.
uniform float surface_mode : hint_range(0.0, 2.0, 1.0) = 0.0;
uniform float edge_gain : hint_range(0.0, 0.35) = 0.12;

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        float daylight = clamp(env_daylight, 0.0, 1.0);
        float cloud_soften = 1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.28;
        vec2 to_sun = length(env_sun_direction) > 0.0001
            ? normalize(-env_sun_direction)
            : vec2(0.0, -1.0);

        float is_wall = step(0.5, surface_mode) * (1.0 - step(1.5, surface_mode));
        float is_stairs = step(1.5, surface_mode);
        float is_top = 1.0 - max(is_wall, is_stairs);

        // Tách plane value trước. Mặt đứng luôn tối hơn top để cliff có volume cả khi noon.
        result *= 1.0 + is_top * 0.095 * daylight;
        result *= 1.0 - is_wall * 0.18 * (0.62 + 0.38 * daylight);
        result *= 1.0 - is_stairs * 0.065 * daylight;

        // Wall của map chủ yếu quay xuống màn hình. Khi nắng từ phía trên màn hình,
        // top bắt sáng hơn; khi nắng từ dưới, wall có chút warm bounce thay vì đổi trắng/đen cực đoan.
        float top_key = smoothstep(-0.35, 0.90, -to_sun.y);
        float wall_key = smoothstep(-0.65, 0.60, to_sun.y);
        result += env_sun_color.rgb * is_top * daylight * cloud_soften * (0.022 + top_key * 0.035);
        result += env_sun_color.rgb * is_wall * daylight * cloud_soften * wall_key * 0.012;
        result = mix(result, result * vec3(0.82, 0.87, 0.93), is_wall * 0.15 + is_stairs * 0.055);

        // Pixel edge highlight. TEXTURE sample nằm trực tiếp trong fragment để Compatibility renderer chịu compile.
        vec2 py = vec2(0.0, TEXTURE_PIXEL_SIZE.y * 1.25);
        float alpha_above = texture(TEXTURE, UV - py).a;
        float top_edge = base.a * (1.0 - alpha_above);
        result += env_sun_color.rgb * top_edge * edge_gain * daylight * (is_top + is_wall * 0.28);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * (0.018 + is_wall * 0.025);
        result = mix(result, result * vec3(0.76, 0.84, 1.02), clamp(env_night, 0.0, 1.0) * 0.11);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
