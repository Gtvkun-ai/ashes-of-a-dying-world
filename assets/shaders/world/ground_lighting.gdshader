shader_type canvas_item;

// Ashes Environment V4.0 - authored ground response.
// GroundBase stays pixel-perfect; this pass only adds broad material variation, cavity shade,
// water-edge moisture and time-of-day split lighting. No moving micro-noise.
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
// R=macro value field, G=terrain cavity/edge proximity, B=pond moisture proximity.
uniform sampler2D art_mask : filter_nearest;
// Grayscale contact map for path/cliff/pond/rock grounding.
uniform sampler2D grass_contact_mask : filter_nearest;
uniform float macro_strength : hint_range(0.0, 0.20) = 0.060;
uniform float cavity_strength : hint_range(0.0, 0.25) = 0.105;
uniform float moisture_strength : hint_range(0.0, 0.20) = 0.055;
uniform float grass_contact_strength : hint_range(0.0, 0.16) = 0.050;
uniform float grass_contact_coolth : hint_range(0.0, 0.18) = 0.060;
uniform float sun_wash : hint_range(0.0, 0.20) = 0.070;
uniform float golden_gain : hint_range(0.0, 0.25) = 0.115;
uniform float wet_darken : hint_range(0.0, 0.20) = 0.060;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 base = COLOR;
    vec3 result = base.rgb;

    if (base.a > 0.001) {
        vec3 authored = texture(art_mask, UV).rgb;
        float grass_contact = texture(grass_contact_mask, UV).r;
        float macro = (authored.r - 0.5) * 2.0;
        float cavity = authored.g;
        float moisture = authored.b;
        float day = clamp(env_daylight, 0.0, 1.0);
        float night = clamp(env_night, 0.0, 1.0);
        float golden = clamp(env_golden_hour, 0.0, 1.0);
        float key = clamp(env_key_strength, 0.0, 1.0);

        // Large calm patches, not procedural grass static.
        result *= 1.0 + macro * macro_strength;

        // Terrain edge shade helps cliff/shore/tree-border areas sit into the ground.
        float cavity_gain = cavity_strength * (0.72 + day * 0.28);
        result *= 1.0 - cavity * cavity_gain;

        // Slightly cooler, darker soil around the pond. It remains subtle under the water sprite.
        vec3 moist_tint = vec3(0.86, 0.96, 0.90);
        result = mix(result, result * moist_tint, moisture * moisture_strength / 0.20);

        // Subtle grounding around authored objects without turning the grass muddy.
        vec3 contact_tint = vec3(0.88, 0.96, 0.84);
        result = mix(result, result * contact_tint, grass_contact * grass_contact_coolth);
        result *= 1.0 - grass_contact * grass_contact_strength;

        // Sun only kisses the brighter macro fields. This is what avoids the old full-screen orange/olive wash.
        float sun_patch = smoothstep(-0.22, 0.72, macro);
        float cloud_soften = 1.0 - clamp(env_cloudiness, 0.0, 1.0) * 0.42;
        float key_gain = key * cloud_soften * day;
        result += env_key_color.rgb * sun_patch * sun_wash * key_gain * (0.42 + golden * 0.58);

        // Golden hour is selective: highlights warm, shadows remain green/cool like the target paintover.
        float y = luma(result);
        float highlight_zone = smoothstep(0.30, 0.72, y) * (0.55 + sun_patch * 0.45);
        result += vec3(0.095, 0.045, 0.002) * highlight_zone * golden * golden_gain / 0.25;
        result *= mix(vec3(1.0), vec3(0.95, 0.975, 1.015), (1.0 - highlight_zone) * golden * 0.055);

        float wet = clamp(max(env_wetness, env_rain), 0.0, 1.0);
        result *= 1.0 - wet * wet_darken;

        // Moonlit ground must be blue-green and readable, not a nearly-black green filter.
        vec3 night_result = result * vec3(0.66, 0.82, 1.10) + vec3(0.006, 0.014, 0.030);
        result = mix(result, night_result, night * 0.30);
    }

    COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), base.a);
}
