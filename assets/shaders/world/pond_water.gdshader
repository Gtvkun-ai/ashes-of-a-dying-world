shader_type canvas_item;
render_mode unshaded;

// Pond water: base albedo + authored depth/shoreline/detail masks.
// This is the same environment-aware water model as the old puddle shader, tuned for a larger pond asset.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_elevation;
global uniform float env_key_strength;
global uniform float env_golden_hour;
global uniform float env_shadow_length;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;
global uniform float env_water_shimmer;
global uniform float env_water_ripple;

uniform sampler2D depth_mask : filter_linear, repeat_disable;
uniform sampler2D shoreline_mask : filter_linear, repeat_disable;
uniform sampler2D surface_detail : filter_linear, repeat_disable;
uniform vec3 shallow_tint : source_color = vec3(0.68, 0.88, 0.76);
uniform vec3 deep_tint : source_color = vec3(0.34, 0.58, 0.62);
uniform float depth_color_strength : hint_range(0.0, 1.0) = 0.36;
uniform float shoreline_gain : hint_range(0.0, 0.25) = 0.052;
uniform float shimmer_response : hint_range(0.0, 2.0) = 0.62;
uniform float ripple_response : hint_range(0.0, 2.0) = 0.52;
uniform float animation_speed : hint_range(0.0, 4.0) = 0.50;
uniform float golden_glint : hint_range(0.0, 0.6) = 0.12;
uniform float night_glint : hint_range(0.0, 0.6) = 0.12;

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
        float shore = clamp(texture(shoreline_mask, UV).r, 0.0, 1.0);
        float detail = (texture(surface_detail, UV).r - 0.5) * 2.0;
        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);
        float elevation = clamp(env_key_elevation, 0.0, 1.0);
        float low_sun = clamp(env_shadow_length, 0.0, 1.0);
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        vec2 across = vec2(-to_key.y, to_key.x);

        vec3 depth_tint = mix(shallow_tint, deep_tint, smoothstep(0.05, 0.92, depth));
        result = mix(result, result * depth_tint, depth_color_strength);
        result *= 1.0 + detail * 0.018;

        vec2 pond_centered = UV - vec2(0.5);
        float sun_side = dot(pond_centered, to_key);
        float far_shade = smoothstep(-0.46, 0.42, -sun_side) * (0.35 + depth * 0.65);
        float near_light = smoothstep(-0.24, 0.54, sun_side) * (1.0 - depth * 0.35);
        float key_soft = day * key * (1.0 - env_cloudiness * 0.50);
        result *= 1.0 - far_shade * key_soft * mix(0.030, 0.070, low_sun);
        result += env_key_color.rgb * near_light * key_soft * mix(0.010, 0.026, elevation);

        vec3 shore_color = mix(vec3(0.58, 0.76, 0.66), env_key_color.rgb, golden * 0.45);
        result += shore_color * shore * shoreline_gain * (0.35 + day * 0.65) * (0.70 + near_light * 0.30);

        vec2 pixel = UV / TEXTURE_PIXEL_SIZE;
        vec2 q = floor(pixel / 3.0) * 3.0;
        float phase = dot(q, across) * 0.085 + dot(q, to_key) * 0.025;
        float wave = sin(phase + TIME * animation_speed * (1.0 + env_wind * 0.16));
        wave += sin(phase * 1.71 - TIME * animation_speed * 0.51) * 0.34;
        wave /= 1.34;

        float glint_band = smoothstep(0.80, 0.97, wave + detail * 0.16);
        float reflection_lane = 1.0 - smoothstep(0.035, mix(0.22, 0.12, low_sun), abs(dot(pond_centered, across) - 0.08));
        reflection_lane *= smoothstep(-0.38, 0.56, dot(pond_centered, to_key));
        float broken = step(0.72, hash(floor(q / 5.0) + floor(TIME * 0.40)));
        float sky = day * (1.0 - env_cloudiness * 0.54) + night * 0.48;
        float glint = max(glint_band * broken, reflection_lane * 0.38) * env_water_shimmer * shimmer_response * sky;
        glint *= mix(0.48, 1.0, depth);

        vec3 day_glint = mix(vec3(0.58, 0.78, 0.86), env_key_color.rgb, golden * 0.72);
        result += day_glint * glint * (0.08 + golden * golden_glint + low_sun * 0.06) * day * key;
        result += vec3(0.28, 0.50, 0.76) * glint * night * night_glint;

        float ripple_amount = clamp(max(env_water_ripple, env_rain * 0.80), 0.0, 1.0) * ripple_response;
        vec2 cell = floor(q / 8.0);
        float rnd = hash(cell);
        float pulse = fract(TIME * (0.55 + rnd * 0.42) + rnd * 4.9);
        float ring = 1.0 - smoothstep(0.03, 0.11, abs(pulse - 0.16));
        result += vec3(0.46, 0.68, 0.82) * ring * step(0.84, rnd) * ripple_amount * 0.045;

        vec3 night_water = result * mix(vec3(0.62, 0.78, 1.02), vec3(0.46, 0.68, 1.10), depth);
        night_water += vec3(0.003, 0.014, 0.036) * (0.35 + depth * 0.65);
        result = mix(result, night_water, night * 0.58);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
