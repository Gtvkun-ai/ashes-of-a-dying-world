shader_type canvas_item;

// Calm puddle V1.3. Không distort silhouette, không vẽ sine-wave giả lên nước.
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

uniform float shimmer_response : hint_range(0.0, 2.0) = 1.0;
uniform float ripple_response : hint_range(0.0, 2.0) = 1.0;
uniform float animation_speed : hint_range(0.0, 4.0) = 0.48;
uniform float pixel_block : hint_range(1.0, 8.0, 1.0) = 2.0;

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec4 raw = texture(TEXTURE, UV);
        float painted_highlight = smoothstep(0.345, 0.385, raw.g);

        vec2 texel = floor(UV / TEXTURE_PIXEL_SIZE);
        float local_phase = texel.y * 0.19 + floor(texel.x / 5.0) * 0.07;
        float pulse = 0.5 + 0.5 * sin(TIME * animation_speed * (0.82 + env_wind * 0.10) + local_phase);

        float sky_visibility = clamp(
            env_daylight * (1.0 - env_cloudiness * 0.60) + env_night * 0.08,
            0.0,
            1.0
        );
        float wet_gain = 0.82 + clamp(max(env_wetness, env_rain), 0.0, 1.0) * 0.20;
        float glint = painted_highlight * pulse * env_water_shimmer * shimmer_response * sky_visibility * wet_gain;

        vec3 sky_tint = mix(vec3(0.70, 0.82, 1.0), env_sun_color.rgb, env_daylight);
        result += sky_tint * glint * 0.075;
        result += vec3(0.006, 0.012, 0.020) * env_night * painted_highlight;
    }

    COLOR = vec4(result, base.a);
}
