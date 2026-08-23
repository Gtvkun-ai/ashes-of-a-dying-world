shader_type canvas_item;

// Ashes Environment V4.0 - foliage shape + light, tuned against the day/golden/night target frames.
// The authored albedo stays crisp; pseudo-normal only redistributes value/color across the crown.
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

uniform sampler2D foliage_normal : filter_nearest;
uniform float relight_strength : hint_range(0.0, 1.8) = 1.12;
uniform float sun_highlight : hint_range(0.0, 0.7) = 0.31;
uniform float shade_depth : hint_range(0.0, 0.7) = 0.27;
uniform float crown_ao : hint_range(0.0, 0.35) = 0.115;
uniform float golden_rim : hint_range(0.0, 0.35) = 0.15;
uniform vec3 cool_shadow_tint : source_color = vec3(0.76, 0.86, 0.88);

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
    float effective_wind = max(env_wind, 0.12);
    VERTEX.x += base_wave * effective_wind * gust_gain * rain_damp * max_bend_pixels * crown;
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec3 n = texture(foliage_normal, UV).rgb * 2.0 - 1.0;
        n = normalize(vec3(n.xy, max(n.z, 0.24)));

        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        float side = clamp(dot(n.xy, to_key) * 2.10, -1.0, 1.0);
        float lit = smoothstep(-0.06, 0.74, side);
        float shaded = smoothstep(0.02, 0.82, -side);

        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);
        float cloud_soften = 1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.36;

        // Low sun is visually allowed to punch above its raw physical energy. This is art direction,
        // not a second ambient multiplier, so sunset stays gold instead of turning the whole map olive.
        float artistic_key = mix(key, max(key, 0.76), golden);
        artistic_key = mix(artistic_key, max(artistic_key, 0.50), night * 0.35);
        float response = relight_strength * artistic_key * cloud_soften;

        float day_response = response * day;
        float night_response = response * night * 0.46;
        result *= 1.0 + lit * sun_highlight * day_response;
        result *= 1.0 - shaded * shade_depth * day_response;
        result = mix(result, result * cool_shadow_tint, shaded * day_response * 0.34);
        result += env_key_color.rgb * lit * day_response * (0.030 + golden * golden_rim);

        // Lower crown and interior leaf masses remain darker at noon too, so the canopy reads as volume.
        float lower_crown = smoothstep(0.54, 1.0, UV.y);
        float side_mass = smoothstep(0.58, 1.0, abs(UV.x - 0.5) * 2.0);
        result *= 1.0 - lower_crown * crown_ao * (0.82 + day * 0.18);
        result *= 1.0 - side_mass * crown_ao * 0.18;

        // Night target: teal/blue moon response with enough top-edge separation to remain readable.
        if (night > 0.001) {
            vec3 moon_base = result * vec3(0.58, 0.76, 0.96) + vec3(0.004, 0.012, 0.024);
            moon_base += env_key_color.rgb * lit * night_response * 0.055;
            moon_base *= 1.0 - shaded * night_response * 0.10;
            result = mix(result, moon_base, night * 0.58);
        }

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result += env_key_color.rgb * lit * wet * 0.020;
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
