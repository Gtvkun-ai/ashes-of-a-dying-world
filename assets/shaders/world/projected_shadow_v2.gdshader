shader_type canvas_item;
render_mode unshaded, blend_mix;

// Ashes Shadow Core V3.3
// Model 3 = ArtDirectedFootprint. Tree shadow source is already a flattened ground silhouette;
// shader only rotates + stretches that footprint by celestial direction/length.
// Đây là khác biệt quan trọng so với V2/V3.2: không còn ép một cây dựng đứng thành tấm cardboard nằm xuống đất.
uniform vec2 shadow_direction = vec2(0.0, 1.0);
uniform float shadow_length01 : hint_range(0.0, 1.0) = 0.0;
uniform float shadow_strength : hint_range(0.0, 1.0) = 0.5;
uniform float shadow_night_factor : hint_range(0.0, 1.0) = 0.0;

uniform float caster_projection_model = 0.0;
uniform float caster_base_x_local = 0.0;
uniform float caster_base_y_local = 16.0;
uniform float caster_height_local = 24.0;
uniform float caster_width_scale = 1.0;
uniform float caster_noon_length_world = 1.0;
uniform float caster_max_length_world = 24.0;
uniform float caster_noon_flatten = 0.10;
uniform float caster_horizon_flatten = 0.15;
uniform vec4 caster_tint : source_color = vec4(0.04, 0.065, 0.04, 1.0);
uniform float caster_opacity = 0.30;
uniform float caster_alpha_cutoff = 0.08;

void vertex() {
    vec2 dir_world = length(shadow_direction) > 0.0001
        ? normalize(shadow_direction)
        : vec2(0.0, 1.0);
    vec2 side_world = vec2(-dir_world.y, dir_world.x);

    mat2 basis = mat2(MODEL_MATRIX[0].xy, MODEL_MATRIX[1].xy);
    mat2 inv_basis = inverse(basis);

    float world_per_local_x = max(length(basis[0]), 0.0001);
    float world_per_local_y = max(length(basis[1]), 0.0001);

    float length_curve = pow(clamp(shadow_length01, 0.0, 1.0), 0.64);
    float cast_length_world = mix(caster_noon_length_world, caster_max_length_world, length_curve);
    float flatten = mix(caster_noon_flatten, caster_horizon_flatten, clamp(shadow_length01, 0.0, 1.0));

    vec2 anchor_local = vec2(caster_base_x_local, caster_base_y_local);
    float x_local = VERTEX.x - anchor_local.x;
    float height_local = clamp(anchor_local.y - VERTEX.y, 0.0, max(caster_height_local, 1.0));
    float height01 = height_local / max(caster_height_local, 1.0);

    if (caster_projection_model < 1.5) {
        // Legacy upright / low-volume projection. Dùng cho nhân vật và đá.
        float lateral_world = x_local * world_per_local_x * caster_width_scale;
        float footprint_world = height_local * world_per_local_y * flatten;
        float longitudinal_world = footprint_world + cast_length_world * height01;
        vec2 projected_world = side_world * lateral_world + dir_world * longitudinal_world;
        VERTEX = anchor_local + inv_basis * projected_world;
    } else if (caster_projection_model < 2.5) {
        // RigidDrop cho cliff/tường lớn.
        VERTEX += inv_basis * (dir_world * cast_length_world);
    } else {
        // Art-directed footprint: texture đã là bóng nằm trên mặt đất.
        // Chỉ map canonical +Y footprint sang hướng bóng và scale chiều dài.
        float noon_width = 0.64;
        float horizon_width = 1.0;
        float width_gain = mix(noon_width, horizon_width, length_curve);
        float lateral_world = x_local * world_per_local_x * caster_width_scale * width_gain;
        float longitudinal_world = cast_length_world * height01;
        vec2 projected_world = side_world * lateral_world + dir_world * longitudinal_world;
        VERTEX = anchor_local + inv_basis * projected_world;
    }
}

void fragment() {
    float spread = mix(0.45, 1.20, clamp(shadow_length01, 0.0, 1.0));
    vec2 px = TEXTURE_PIXEL_SIZE * spread;

    float raw_alpha = texture(TEXTURE, UV).a;
    float source_alpha = raw_alpha * 0.68;
    source_alpha += texture(TEXTURE, UV + vec2(px.x, 0.0)).a * 0.08;
    source_alpha += texture(TEXTURE, UV - vec2(px.x, 0.0)).a * 0.08;
    source_alpha += texture(TEXTURE, UV + vec2(0.0, px.y)).a * 0.08;
    source_alpha += texture(TEXTURE, UV - vec2(0.0, px.y)).a * 0.08;
    source_alpha = clamp(source_alpha, 0.0, 1.0);

    if (raw_alpha < caster_alpha_cutoff && source_alpha < 0.025) {
        discard;
    }

    vec3 night_tint = vec3(0.028, 0.036, 0.058);
    vec3 tint = mix(caster_tint.rgb, night_tint, clamp(shadow_night_factor * 0.28, 0.0, 0.28));

    // Bóng xa mềm/nhẹ hơn một chút, tránh mảng blob đen lúc 18h.
    float distance_fade = mix(1.0, 0.82, clamp(shadow_length01, 0.0, 1.0));
    float alpha = source_alpha * caster_opacity * shadow_strength * distance_fade;
    COLOR = vec4(tint, clamp(alpha, 0.0, 0.62));
}
