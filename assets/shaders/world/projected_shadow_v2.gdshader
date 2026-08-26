shader_type canvas_item;
render_mode unshaded, blend_mix;

// Ashes Shadow Core V4.0
// Tree masks are authored ground footprints. Actor/rock legacy projection remains compact.
uniform vec2 shadow_direction = vec2(0.0, 1.0);
uniform float shadow_length01 : hint_range(0.0, 1.0) = 0.0;
uniform float shadow_strength : hint_range(0.0, 1.0) = 0.5;
uniform float shadow_night_factor : hint_range(0.0, 1.0) = 0.0;

uniform float caster_projection_model = 0.0;
uniform float caster_base_x_local = 0.0;
uniform float caster_base_y_local = 16.0;
uniform float caster_height_local = 24.0;
uniform float caster_footprint_center_y_local = 0.0;
uniform float caster_footprint_depth_local = 24.0;
uniform float caster_width_scale = 1.0;
uniform float caster_noon_length_world = 1.0;
uniform float caster_max_length_world = 24.0;
uniform float caster_noon_flatten = 0.10;
uniform float caster_horizon_flatten = 0.15;
uniform vec4 caster_tint : source_color = vec4(0.04, 0.065, 0.04, 1.0);
uniform float caster_opacity = 0.30;
uniform float caster_alpha_cutoff = 0.08;

void vertex() {
    vec2 dir_world = length(shadow_direction) > 0.0001 ? normalize(shadow_direction) : vec2(0.0, 1.0);
    vec2 side_world = vec2(-dir_world.y, dir_world.x);
    mat2 basis = mat2(MODEL_MATRIX[0].xy, MODEL_MATRIX[1].xy);
    mat2 inv_basis = inverse(basis);
    float world_per_local_x = max(length(basis[0]), 0.0001);
    float world_per_local_y = max(length(basis[1]), 0.0001);

    float length_curve = pow(clamp(shadow_length01, 0.0, 1.0), 0.70);
    float cast_length_world = mix(caster_noon_length_world, caster_max_length_world, length_curve);
    float flatten = mix(caster_noon_flatten, caster_horizon_flatten, clamp(shadow_length01, 0.0, 1.0));
    vec2 anchor_local = vec2(caster_base_x_local, caster_base_y_local);
    float x_local = VERTEX.x - anchor_local.x;
    float height_local = clamp(anchor_local.y - VERTEX.y, 0.0, max(caster_height_local, 1.0));
    float height01 = height_local / max(caster_height_local, 1.0);

    if (caster_projection_model < 1.5) {
        float lateral_world = x_local * world_per_local_x * caster_width_scale;
        float footprint_world = height_local * world_per_local_y * flatten;
        float longitudinal_world = footprint_world + cast_length_world * height01;
        vec2 projected_world = side_world * lateral_world + dir_world * longitudinal_world;
        VERTEX = anchor_local + inv_basis * projected_world;
    } else if (caster_projection_model < 2.5) {
        VERTEX += inv_basis * (dir_world * cast_length_world);
    } else {
        // V4.3: authored footprint thật sự nằm trên ground-plane.
        // CPU truyền alpha-bounds center/depth của mask, vì vậy quad trong suốt khổng lồ của
        // texture không còn làm sai phép chiếu. Near edge luôn chạm gốc, far edge đi theo nắng.
        float y_from_center_local = caster_footprint_center_y_local - VERTEX.y;
        float authored_depth_world = max(caster_footprint_depth_local * world_per_local_y, 1.0);
        float desired_depth_world = cast_length_world;
        float depth_scale = desired_depth_world / authored_depth_world;

        // Trưa: bóng canopy compact nhưng vẫn có diện tích. Low sun: mở đúng mass của tán cây.
        float width_gain = mix(0.58, 1.04, length_curve);
        float lateral_world = x_local * world_per_local_x * caster_width_scale * width_gain;

        // Center của footprint đi ra nửa chiều dài để near edge nằm ngay chân cây.
        float longitudinal_world = y_from_center_local * world_per_local_y * depth_scale
            + desired_depth_world * 0.50;
        vec2 projected_world = side_world * lateral_world + dir_world * longitudinal_world;
        VERTEX = anchor_local + inv_basis * projected_world;
    }
}

void fragment() {
    float long_shadow = clamp(shadow_length01, 0.0, 1.0);
    float spread = mix(0.52, 1.55, long_shadow);
    vec2 px = TEXTURE_PIXEL_SIZE * spread;
    float raw_alpha = texture(TEXTURE, UV).a;
    float source_alpha = raw_alpha * 0.72;
    source_alpha += texture(TEXTURE, UV + vec2(px.x, 0.0)).a * 0.095;
    source_alpha += texture(TEXTURE, UV - vec2(px.x, 0.0)).a * 0.095;
    source_alpha += texture(TEXTURE, UV + vec2(0.0, px.y)).a * 0.095;
    source_alpha += texture(TEXTURE, UV - vec2(0.0, px.y)).a * 0.095;
    source_alpha += texture(TEXTURE, UV + px).a * 0.034;
    source_alpha += texture(TEXTURE, UV - px).a * 0.034;
    source_alpha = clamp(source_alpha, 0.0, 1.0);

    if (raw_alpha < caster_alpha_cutoff && source_alpha < 0.018) {
        discard;
    }

    // Directional fade is authored into the footprint PNG now. Keep fragment math boring
    // and stable so the shadow keeps its shape instead of turning into a blade.
    vec3 day_tint = caster_tint.rgb;
    vec3 night_tint = vec3(0.026, 0.040, 0.068);
    vec3 tint = mix(day_tint, night_tint, clamp(shadow_night_factor * 0.46, 0.0, 0.46));
    float distance_fade = mix(1.02, 0.985, long_shadow);
    float alpha = source_alpha * caster_opacity * shadow_strength * distance_fade * 1.08;
    COLOR = vec4(tint, clamp(alpha, 0.0, 0.78));
}
