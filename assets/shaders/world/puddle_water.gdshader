shader_type canvas_item;

// Ashes Water V5.3 - authored depth / shoreline / surface detail.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec2 env_key_direction;
global uniform vec4 env_key_color;
global uniform float env_key_strength;
global uniform float env_golden_hour;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;
global uniform float env_water_shimmer;
global uniform float env_water_ripple;

uniform sampler2D depth_mask : filter_linear, repeat_disable;
uniform sampler2D shoreline_mask : filter_linear, repeat_disable;
uniform sampler2D surface_detail : filter_nearest, repeat_disable;
uniform vec3 shallow_tint : source_color = vec3(0.78, 0.96, 0.86);
uniform vec3 deep_tint : source_color = vec3(0.48, 0.76, 0.88);
uniform float depth_color_strength : hint_range(0.0, 1.0) = 0.30;
uniform float shoreline_gain : hint_range(0.0, 0.25) = 0.075;
uniform float shimmer_response : hint_range(0.0, 2.0) = 0.90;
uniform float ripple_response : hint_range(0.0, 2.0) = 0.72;
uniform float animation_speed : hint_range(0.0, 4.0) = 0.50;
uniform float golden_glint : hint_range(0.0, 0.6) = 0.18;
uniform float night_glint : hint_range(0.0, 0.6) = 0.16;

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

        vec3 depth_tint = mix(shallow_tint, deep_tint, smoothstep(0.05, 0.92, depth));
        result = mix(result, result * depth_tint, depth_color_strength);
        result *= 1.0 + detail * 0.035;

        vec3 shore_color = mix(vec3(0.70, 0.88, 0.76), env_key_color.rgb, golden * 0.55);
        result += shore_color * shore * shoreline_gain * (0.35 + day * 0.65);

        vec2 pixel = UV / TEXTURE_PIXEL_SIZE;
        vec2 q = floor(pixel / 3.0) * 3.0;
        vec2 to_key = length(env_key_direction) > 0.0001 ? normalize(-env_key_direction) : vec2(0.0, -1.0);
        vec2 across = vec2(-to_key.y, to_key.x);
        float phase = dot(q, across) * 0.085 + dot(q, to_key) * 0.025;
        float wave = sin(phase + TIME * animation_speed * (1.0 + env_wind * 0.16));
        wave += sin(phase * 1.71 - TIME * animation_speed * 0.51) * 0.34;
        wave /= 1.34;

        float glint_band = smoothstep(0.78, 0.96, wave + detail * 0.18);
        float broken = step(0.70, hash(floor(q / 5.0) + floor(TIME * 0.40)));
        float sky = day * (1.0 - env_cloudiness * 0.54) + night * 0.48;
        float glint = glint_band * broken * env_water_shimmer * shimmer_response * sky;
        glint *= mix(0.58, 1.0, depth);

        vec3 day_glint = mix(vec3(0.72, 0.88, 0.98), env_key_color.rgb, golden * 0.78);
        result += day_glint * glint * (0.11 + golden * golden_glint) * day * key;
        result += vec3(0.36, 0.62, 0.92) * glint * night * night_glint;

        float ripple_amount = clamp(max(env_water_ripple, env_rain * 0.80), 0.0, 1.0) * ripple_response;
        vec2 cell = floor(q / 8.0);
        float rnd = hash(cell);
        float pulse = fract(TIME * (0.55 + rnd * 0.42) + rnd * 4.9);
        float ring = 1.0 - smoothstep(0.03, 0.11, abs(pulse - 0.16));
        result += vec3(0.55, 0.77, 0.94) * ring * step(0.82, rnd) * ripple_amount * 0.055;

        vec3 night_water = result * mix(vec3(0.68, 0.84, 1.10), vec3(0.55, 0.78, 1.22), depth);
        night_water += vec3(0.003, 0.014, 0.036) * (0.35 + depth * 0.65);
        result = mix(result, night_water, night * 0.58);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
