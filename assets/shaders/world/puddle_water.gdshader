shader_type canvas_item;

// Ashes Water V4.0 - authored shoreline depth + restrained pixel glints.
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

uniform sampler2D depth_mask : filter_nearest;
uniform vec3 shallow_tint : source_color = vec3(0.70, 0.94, 0.84);
uniform vec3 deep_tint : source_color = vec3(0.26, 0.62, 0.72);
uniform float depth_color_strength : hint_range(0.0, 1.0) = 0.62;
uniform float shoreline_gain : hint_range(0.0, 0.35) = 0.14;
uniform float shimmer_response : hint_range(0.0, 2.5) = 1.45;
uniform float ripple_response : hint_range(0.0, 2.0) = 1.05;
uniform float animation_speed : hint_range(0.0, 4.0) = 0.62;
uniform float pixel_block : hint_range(1.0, 8.0, 1.0) = 2.0;
uniform float golden_glint : hint_range(0.0, 0.8) = 0.40;
uniform float night_glint : hint_range(0.0, 0.8) = 0.28;

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
        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);

        vec3 depth_tint = mix(shallow_tint, deep_tint, smoothstep(0.05, 0.90, depth));
        result = mix(base.rgb, base.rgb * depth_tint, depth_color_strength);

        // A narrow bright shoreline reads as water depth without fake foam on a quiet pond.
        float shore = (1.0 - smoothstep(0.02, 0.20, depth)) * base.a;
        vec3 shore_color = mix(vec3(0.72, 0.91, 0.82), env_key_color.rgb, golden * 0.65);
        result += shore_color * shore * shoreline_gain * (0.35 + day * 0.65);

        vec2 texel = UV / TEXTURE_PIXEL_SIZE;
        vec2 q = floor(texel / pixel_block) * pixel_block;
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        vec2 across = vec2(-to_key.y, to_key.x);
        float phase = dot(q, across) * 0.105 + dot(q, to_key) * 0.030;
        float wave = sin(phase + TIME * animation_speed * (1.0 + env_wind * 0.20));
        wave += sin(phase * 1.79 - TIME * animation_speed * 0.58) * 0.38;
        wave /= 1.38;

        float glint_band = smoothstep(0.70, 0.94, wave);
        float broken = step(0.60, hash(floor(q / 4.0) + floor(TIME * 0.48)));
        float sky = day * (1.0 - env_cloudiness * 0.52) + night * 0.58;
        float glint = glint_band * broken * env_water_shimmer * shimmer_response * sky;
        glint *= mix(0.64, 1.0, depth);

        vec3 day_glint = mix(vec3(0.76, 0.91, 1.0), env_key_color.rgb, golden * 0.90);
        float day_glint_gain = 0.17 + golden * golden_glint;
        result += day_glint * glint * day_glint_gain * day;
        result += vec3(0.40, 0.68, 1.0) * glint * night * night_glint;

        // Sparse rain/ambient ripples, quantized to pixel cells.
        float ripple_amount = clamp(max(env_water_ripple, env_rain * 0.85), 0.0, 1.0) * ripple_response;
        vec2 cell = floor(q / 7.0);
        float rnd = hash(cell);
        float pulse = fract(TIME * (0.62 + rnd * 0.50) + rnd * 5.17);
        float ring = 1.0 - smoothstep(0.025, 0.105, abs(pulse - 0.16));
        float ripple = ring * step(0.76, rnd) * ripple_amount;
        result += mix(vec3(0.66, 0.83, 0.96), vec3(0.48, 0.70, 1.0), night) * ripple * 0.085;

        // Deep water carries the moon-blue target color; shallow shelves stay greener.
        vec3 night_water = result * mix(vec3(0.62, 0.83, 1.12), vec3(0.48, 0.78, 1.28), depth);
        night_water += vec3(0.004, 0.018, 0.048) * (0.35 + depth * 0.65);
        result = mix(result, night_water, night * 0.64);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - depth * 0.022 - wet * 0.014;
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
