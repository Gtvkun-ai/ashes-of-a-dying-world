shader_type canvas_item;
render_mode unshaded;

// Lightweight directional relight for small flora without authored normal maps.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_elevation;
global uniform float env_key_strength;
global uniform float env_golden_hour;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;

uniform float relight_strength : hint_range(0.0, 0.40) = 0.16;
uniform float shade_strength : hint_range(0.0, 0.40) = 0.12;
uniform float color_cohesion : hint_range(0.0, 0.30) = 0.08;
uniform float root_contact : hint_range(0.0, 0.30) = 0.08;
uniform vec3 cool_shadow_tint : source_color = vec3(0.82, 0.90, 0.90);

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec2 px = TEXTURE_PIXEL_SIZE * 1.5;
        float left = texture(TEXTURE, UV - vec2(px.x, 0.0)).a;
        float right = texture(TEXTURE, UV + vec2(px.x, 0.0)).a;
        float up = texture(TEXTURE, UV - vec2(0.0, px.y)).a;
        float down = texture(TEXTURE, UV + vec2(0.0, px.y)).a;
        vec2 edge_n = normalize(vec2(left - right, up - down) + (UV - vec2(0.5, 0.62)) * 0.22 + vec2(0.0001));

        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float side = clamp(dot(edge_n, to_key) * 1.35, -1.0, 1.0);
        float lit = smoothstep(-0.12, 0.74, side);
        float shaded = smoothstep(0.02, 0.82, -side);

        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key_soft = clamp(env_key_strength, 0.0, 1.0) * day * (1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.38);
        float top = (1.0 - smoothstep(0.08, 0.72, UV.y)) * clamp(env_key_elevation, 0.0, 1.0);

        result *= 1.0 + lit * relight_strength * key_soft * (0.72 + top * 0.28);
        result *= 1.0 - shaded * shade_strength * key_soft;
        result = mix(result, result * cool_shadow_tint, shaded * key_soft * 0.16);
        result += env_key_color.rgb * lit * key_soft * (0.010 + golden * 0.030);

        float y = luma(result);
        vec3 cohesive = result * vec3(0.94, 0.985, 0.88);
        result = mix(result, cohesive, color_cohesion * day * smoothstep(0.12, 0.70, y));
        result *= 1.0 - smoothstep(0.64, 1.0, UV.y) * root_contact;

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result += env_key_color.rgb * lit * wet * 0.010;

        vec3 night_result = result * vec3(0.62, 0.78, 1.00) + vec3(0.003, 0.008, 0.018);
        result = mix(result, night_result, night * 0.44);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
