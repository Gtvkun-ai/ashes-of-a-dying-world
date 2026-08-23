shader_type canvas_item;

// Ashes Environment V3.3 - Compatibility-safe foliage motion + directional relighting.
// Không dùng early-return trong fragment() vì Godot 4.7 Compatibility từ chối return;
// normal map này là pseudo-normal được derive từ cụm lá, không phải PBR normal thật.
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

uniform sampler2D foliage_normal : filter_nearest;
uniform float relight_strength : hint_range(0.0, 1.5) = 0.92;
uniform float sun_highlight : hint_range(0.0, 0.6) = 0.27;
uniform float shade_depth : hint_range(0.0, 0.6) = 0.21;
uniform float crown_ao : hint_range(0.0, 0.3) = 0.085;
uniform vec3 cool_shadow_tint : source_color = vec3(0.78, 0.86, 0.82);

uniform float max_bend_pixels : hint_range(0.0, 12.0) = 1.25;
uniform float sway_speed : hint_range(0.0, 4.0) = 1.0;
uniform float anchor_y : hint_range(0.25, 1.0) = 0.94;
uniform float gust_strength : hint_range(0.0, 1.0) = 0.28;
uniform float rain_stiffness : hint_range(0.0, 1.0) = 0.28;

void vertex() {
    float crown = clamp((anchor_y - UV.y) / max(anchor_y, 0.001), 0.0, 1.0);
    crown *= crown;
    vec2 world_origin = MODEL_MATRIX[3].xy;
    float phase = world_origin.x * 0.012 + world_origin.y * 0.0067;
    float base_wave = sin(TIME * sway_speed + phase);
    base_wave += sin(TIME * sway_speed * 2.17 + phase * 1.71) * 0.30;
    float gust = 0.5 + 0.5 * sin(TIME * 0.23 + phase * 0.37);
    float gust_gain = mix(1.0, 1.0 + gust, gust_strength);
    float rain_damp = mix(1.0, 1.0 - rain_stiffness, clamp(env_rain, 0.0, 1.0));
    float effective_wind = max(env_wind, 0.14);
    VERTEX.x += base_wave * effective_wind * gust_gain * rain_damp * max_bend_pixels * crown;
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec3 n = texture(foliage_normal, UV).rgb * 2.0 - 1.0;
        n = normalize(vec3(n.xy, max(n.z, 0.20)));

        vec2 to_sun = length(env_sun_direction) > 0.0001 ? normalize(-env_sun_direction) : vec2(0.0, -1.0);
        float side = clamp(dot(n.xy, to_sun) * 2.45, -1.0, 1.0);
        float lit = smoothstep(-0.04, 0.84, side);
        float shaded = smoothstep(0.04, 0.88, -side);

        float daylight = clamp(env_daylight, 0.0, 1.0);
        float cloud_soften = 1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.32;

        // env_sun_direction.y gần 1 khi mặt trời cao, ~0.5 lúc sát chân trời.
        // Dùng nó như proxy intensity để sunset không bị highlight mạnh như 12h.
        float sun_height = smoothstep(0.45, 0.97, abs(normalize(env_sun_direction).y));
        float key_gain = mix(0.52, 1.0, sun_height);
        float response = relight_strength * daylight * cloud_soften * key_gain;

        result *= 1.0 + lit * sun_highlight * response;
        result *= 1.0 - shaded * shade_depth * response;
        result = mix(result, result * cool_shadow_tint, shaded * response * 0.28);
        result += env_sun_color.rgb * lit * response * 0.038;

        // Phần thấp của tán luôn tối nhẹ để giữ cảm giác canopy có khối.
        float lower_crown = smoothstep(0.58, 1.0, UV.y);
        result *= 1.0 - lower_crown * crown_ao * (0.68 + 0.32 * daylight);

        // Highlight nhỏ ở nửa trên, phụ thuộc hướng mặt trời, tránh kiểu toàn tán phát sáng.
        float upper_mass = 1.0 - smoothstep(0.18, 0.62, UV.y);
        result += env_sun_color.rgb * upper_mass * lit * response * 0.020;

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result += env_sun_color.rgb * lit * wet * 0.018;
        result = mix(result, result * vec3(0.76, 0.84, 1.02), clamp(env_night, 0.0, 1.0) * 0.16);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
