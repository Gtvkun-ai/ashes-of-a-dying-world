shader_type canvas_item;
render_mode unshaded;

// Ashes Environment V5.7 - restrained foliage shape + wind.
// Foliage owns its directional form; Light2D is ignored to prevent a second highlight pass.
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

uniform sampler2D foliage_normal : filter_nearest;
uniform sampler2D foliage_ao : filter_nearest;
uniform float authored_ao_strength : hint_range(0.0, 0.45) = 0.0;
uniform float relight_strength : hint_range(0.0, 1.8) = 0.72;
uniform float sun_highlight : hint_range(0.0, 0.7) = 0.16;
uniform float shade_depth : hint_range(0.0, 0.7) = 0.16;
uniform float crown_ao : hint_range(0.0, 0.35) = 0.085;
uniform float golden_rim : hint_range(0.0, 0.35) = 0.07;
uniform vec3 cool_shadow_tint : source_color = vec3(0.80, 0.88, 0.89);

uniform float max_bend_pixels : hint_range(0.0, 12.0) = 1.25;
uniform float sway_speed : hint_range(0.0, 4.0) = 1.0;
uniform float anchor_y : hint_range(0.25, 1.0) = 0.94;
uniform float gust_strength : hint_range(0.0, 1.0) = 0.28;
uniform float rain_stiffness : hint_range(0.0, 1.0) = 0.28;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

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
    float effective_wind = max(env_wind, 0.12);
    VERTEX.x += base_wave * effective_wind * gust_gain * rain_damp * max_bend_pixels * crown;
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec3 n = texture(foliage_normal, UV).rgb * 2.0 - 1.0;
        n = normalize(vec3(n.xy, max(n.z, 0.30)));

        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float side = clamp(dot(n.xy, to_key) * 1.45, -1.0, 1.0);
        float lit = smoothstep(-0.12, 0.72, side);
        float shaded = smoothstep(0.08, 0.80, -side);

        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);
        float cloud_soften = 1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.42;

        // Low sun still reads, but it no longer amplifies the baked lime highlights in the albedo.
        float artistic_key = mix(key, max(key, 0.66), golden);
        artistic_key = mix(artistic_key, max(artistic_key, 0.44), night * 0.30);
        float response = relight_strength * artistic_key * cloud_soften;

        float day_response = response * day;
        float night_response = response * night * 0.44;
        result *= 1.0 + lit * sun_highlight * day_response;
        result *= 1.0 - shaded * shade_depth * day_response;
        result = mix(result, result * cool_shadow_tint, shaded * day_response * 0.24);
        result += env_key_color.rgb * lit * day_response * (0.016 + golden * golden_rim);

        if (authored_ao_strength > 0.001) {
            float authored_ao = texture(foliage_ao, UV).r;
            result *= mix(1.0 - authored_ao_strength, 1.0, clamp(authored_ao, 0.0, 1.0));
        }

        // Keep the interior / lower crown as the main volume cue. This is more stable than bright relighting.
        float lower_crown = smoothstep(0.54, 1.0, UV.y);
        float side_mass = smoothstep(0.60, 1.0, abs(UV.x - 0.5) * 2.0);
        result *= 1.0 - lower_crown * crown_ao * (0.86 + day * 0.14);
        result *= 1.0 - side_mass * crown_ao * 0.15;

        if (night > 0.001) {
            vec3 moon_base = result * vec3(0.58, 0.76, 0.96) + vec3(0.004, 0.012, 0.024);
            moon_base += env_key_color.rgb * lit * night_response * 0.045;
            moon_base *= 1.0 - shaded * night_response * 0.08;
            result = mix(result, moon_base, night * 0.58);
        }

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result += env_key_color.rgb * lit * wet * 0.012;

        // The authored canopy is deliberately punchy, but the map reads more natural if lime peaks
        // are compressed after relighting instead of letting every tree glow equally.
        float green_excess = max(result.g - max(result.r, result.b) * 1.03, 0.0);
        float peak = smoothstep(0.34, 0.78, luma(result));
        float tame = green_excess * day * (0.11 + peak * 0.10);
        result.g -= tame;
        result.r += tame * 0.11;
        result.b += tame * 0.025;
        result = mix(result, result * vec3(0.955, 0.975, 0.930), peak * day * 0.035);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
