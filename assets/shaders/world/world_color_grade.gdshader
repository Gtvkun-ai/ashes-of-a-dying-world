shader_type canvas_item;
render_mode unshaded;

// Finishing pass nhẹ. Shader này không "cứu" material sai; chỉ gom palette sau khi lighting đã đúng.
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;

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

uniform float contrast : hint_range(0.8, 1.25) = 1.055;
uniform float day_saturation : hint_range(0.8, 1.25) = 1.045;
uniform float night_saturation : hint_range(0.6, 1.1) = 0.88;
uniform float warm_highlights : hint_range(0.0, 0.15) = 0.032;
uniform float cool_shadows : hint_range(0.0, 0.15) = 0.038;

float luma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
    vec4 src = textureLod(screen_texture, SCREEN_UV, 0.0);
    vec3 c = src.rgb;

    c = (c - 0.5) * contrast + 0.5;
    float y = luma(c);
    float saturation = mix(day_saturation, night_saturation, clamp(env_night, 0.0, 1.0));
    c = mix(vec3(y), c, saturation);

    float hi = smoothstep(0.52, 0.92, y) * clamp(env_daylight, 0.0, 1.0);
    float lo = 1.0 - smoothstep(0.16, 0.48, y);
    c += env_sun_color.rgb * hi * warm_highlights;
    c *= mix(vec3(1.0), vec3(0.90, 0.95, 1.02), lo * cool_shadows / 0.038);

    float weather = clamp(env_cloudiness * 0.16 + env_fog * 0.30 + env_rain * 0.12, 0.0, 0.35);
    c = mix(c, vec3(luma(c)) * vec3(0.92, 0.97, 1.02), weather * 0.24);
    COLOR = vec4(clamp(c, vec3(0.0), vec3(1.0)), src.a);
}
