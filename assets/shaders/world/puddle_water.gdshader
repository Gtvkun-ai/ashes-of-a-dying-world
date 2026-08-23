shader_type canvas_item;

// Ashes Water V2.1 / Environment V3.3 - depth-aware, pixel-safe, Compatibility-safe.
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

uniform sampler2D depth_mask : filter_nearest;
uniform vec3 shallow_tint : source_color = vec3(0.76, 0.95, 0.82);
uniform vec3 deep_tint : source_color = vec3(0.34, 0.66, 0.72);
uniform float depth_color_strength : hint_range(0.0, 1.0) = 0.46;
uniform float shoreline_gain : hint_range(0.0, 0.3) = 0.095;
uniform float shimmer_response : hint_range(0.0, 2.0) = 1.0;
uniform float ripple_response : hint_range(0.0, 2.0) = 1.0;
uniform float animation_speed : hint_range(0.0, 4.0) = 0.65;
uniform float pixel_block : hint_range(1.0, 8.0, 1.0) = 2.0;

float hash(vec2 p) {
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 19.19);
    return fract(p.x * p.y);
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        float depth = clamp(texture(depth_mask, UV).r, 0.0, 1.0);
        vec3 depth_tint = mix(shallow_tint, deep_tint, smoothstep(0.08, 0.92, depth));
        result = mix(base.rgb, base.rgb * depth_tint, depth_color_strength);

        float daylight = clamp(env_daylight, 0.0, 1.0);
        float sky_visibility = clamp(daylight * (1.0 - env_cloudiness * 0.58) + env_night * 0.12, 0.0, 1.0);

        float shore = (1.0 - smoothstep(0.025, 0.23, depth)) * base.a;
        result += mix(vec3(0.74, 0.90, 0.80), env_sun_color.rgb, daylight) * shore * shoreline_gain * sky_visibility;

        vec2 texel = UV / TEXTURE_PIXEL_SIZE;
        vec2 q = floor(texel / pixel_block) * pixel_block;
        vec2 to_sun = length(env_sun_direction) > 0.0001 ? normalize(-env_sun_direction) : vec2(0.0, -1.0);
        vec2 across = vec2(-to_sun.y, to_sun.x);

        float phase = dot(q, across) * 0.115 + dot(q, to_sun) * 0.025;
        float wave = sin(phase + TIME * animation_speed * (1.0 + env_wind * 0.22));
        wave += sin(phase * 1.73 - TIME * animation_speed * 0.61) * 0.42;
        wave = wave / 1.42;

        float glint_band = smoothstep(0.68, 0.92, wave);
        float broken = step(0.56, hash(floor(q / 4.0) + floor(TIME * 0.55)));
        float glint = glint_band * broken * env_water_shimmer * shimmer_response * sky_visibility;
        glint *= mix(0.72, 1.0, depth);
        result += mix(vec3(0.78, 0.90, 1.0), env_sun_color.rgb, daylight) * glint * 0.22;

        float ripple_amount = clamp(max(env_water_ripple, env_rain * 0.85), 0.0, 1.0) * ripple_response;
        vec2 cell = floor(q / 7.0);
        float rnd = hash(cell);
        float pulse = fract(TIME * (0.65 + rnd * 0.55) + rnd * 5.17);
        float ring = 1.0 - smoothstep(0.03, 0.11, abs(pulse - 0.16));
        float ripple = ring * step(0.72, rnd) * ripple_amount;
        result += vec3(0.68, 0.82, 0.94) * ripple * 0.075;

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - depth * 0.030 - wet * 0.018;
        result = mix(result, result * vec3(0.72, 0.82, 1.04), clamp(env_night, 0.0, 1.0) * 0.16);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
