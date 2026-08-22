shader_type canvas_item;

// Calm-puddle pass for Ashes.
// Important: this shader never invents new waves or displaces pixels.
// It only lets highlights already painted into the puddle breathe very slightly.
// Rain ripples belong in a separate VFX overlay later, where they can be art-directed.
global uniform float env_daylight;
global uniform float env_night;
global uniform vec4 env_sun_color;
global uniform float env_wind;
global uniform float env_rain;
global uniform float env_wetness;
global uniform float env_cloudiness;
global uniform float env_water_shimmer;
global uniform float env_water_ripple;

uniform float shimmer_response : hint_range(0.0, 2.0) = 1.0;
uniform float ripple_response : hint_range(0.0, 2.0) = 1.0; // kept for scene compatibility; intentionally unused.
uniform float animation_speed : hint_range(0.0, 4.0) = 0.55;
uniform float pixel_block : hint_range(1.0, 8.0, 1.0) = 2.0; // kept for scene compatibility.

void fragment() {
    vec4 base = COLOR;

    if (base.a <= 0.001) {
        COLOR = base;
        return;
    }

    // COLOR is already texture * CanvasItem modulation in Godot 4.
    // Sample raw texture only to detect highlights without day/night tint changing the threshold.
    vec4 raw = texture(TEXTURE, UV);

    // The Field 1 puddle is hand-painted around G≈0.31 with existing streaks around G≈0.37+.
    // Animate those authored streaks instead of drawing synthetic sine-wave bands on top.
    float painted_highlight = smoothstep(0.345, 0.385, raw.g);

    vec2 texel = floor(UV / TEXTURE_PIXEL_SIZE);
    float local_phase = texel.y * 0.19 + floor(texel.x / 5.0) * 0.07;
    float pulse = 0.5 + 0.5 * sin(
        TIME * animation_speed * (0.82 + env_wind * 0.10) + local_phase
    );

    float sky_visibility = clamp(
        env_daylight * (1.0 - env_cloudiness * 0.60) + env_night * 0.08,
        0.0,
        1.0
    );

    // Rain/wetness increases the response of existing glints, but never spawns fake rings.
    float wet_gain = 0.82 + clamp(max(env_wetness, env_rain), 0.0, 1.0) * 0.20;
    float glint = painted_highlight
        * pulse
        * env_water_shimmer
        * shimmer_response
        * sky_visibility
        * wet_gain;

    vec3 sky_tint = mix(vec3(0.70, 0.82, 1.0), env_sun_color.rgb, env_daylight);
    base.rgb += sky_tint * glint * 0.075;

    // Tiny moon lift for readability. No emissive-looking water.
    base.rgb += vec3(0.006, 0.012, 0.020) * env_night * painted_highlight;

    COLOR = base;
}
