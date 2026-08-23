shader_type canvas_item;
render_mode unshaded, blend_mix;

// Ashes Shadow Core V2.1
//
// V2 cũ cố inverse-sample từng pixel trong một quad mở rộng. Kết quả rất dễ sọc, rách và
// nhìn như sprite bị bóp chứ không phải bóng. V2.1 quay về đúng thứ vertex shader làm tốt:
// biến cả QUAD của asset bằng một phép chiếu affine duy nhất.
//
// Với caster dựng đứng:
//   - trục X của sprite -> trục ngang của bóng (vuông góc hướng bóng);
//   - độ cao Y của sprite -> trục dọc bóng (theo hướng bóng);
//   - UV không bị remap thủ công, nên silhouette giữ nguyên và không sinh sọc/hole.
//
// shadow_direction là hướng BÓNG chạy trên mặt đất, có dấu đầy đủ.
uniform vec2 shadow_direction = vec2(0.0, 1.0);
uniform float shadow_length01 : hint_range(0.0, 1.0) = 0.0;
uniform float shadow_strength : hint_range(0.0, 1.0) = 0.5;
uniform float shadow_night_factor : hint_range(0.0, 1.0) = 0.0;

// Per-caster data. ShadowCaster2D clones the template material, so these are normal uniforms.
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

    // MODEL_MATRIX chứa scale/rotation/skew thực của shadow sprite.
    // Ta dựng phép chiếu trong world-space rồi đổi ngược về local vertex-space.
    mat2 basis = mat2(MODEL_MATRIX[0].xy, MODEL_MATRIX[1].xy);
    mat2 inv_basis = inverse(basis);

    float world_per_local_x = max(length(basis[0]), 0.0001);
    float world_per_local_y = max(length(basis[1]), 0.0001);

    float length_curve = pow(clamp(shadow_length01, 0.0, 1.0), 0.72);
    float cast_length_world = mix(
        caster_noon_length_world,
        caster_max_length_world,
        length_curve
    );

    float flatten = mix(
        caster_noon_flatten,
        caster_horizon_flatten,
        clamp(shadow_length01, 0.0, 1.0)
    );

    vec2 anchor_local = vec2(caster_base_x_local, caster_base_y_local);

    if (caster_projection_model < 1.5) {
        // UPRIGHT / VOLUME
        // X giữ bề ngang asset. Y được hiểu là chiều cao giả của billboard.
        // Sau đó hai trục được đặt xuống mặt đất thành basis (side, direction).
        float x_local = VERTEX.x - anchor_local.x;
        float height_local = clamp(
            anchor_local.y - VERTEX.y,
            0.0,
            max(caster_height_local, 1.0)
        );
        float height01 = height_local / max(caster_height_local, 1.0);

        float lateral_world = x_local * world_per_local_x * caster_width_scale;

        // Footprint depth giữ cho bóng trưa không collapse thành một vạch.
        float footprint_world = height_local * world_per_local_y * flatten;
        float longitudinal_world = footprint_world + cast_length_world * height01;

        vec2 projected_world =
            side_world * lateral_world +
            dir_world * longitudinal_world;

        VERTEX = anchor_local + inv_basis * projected_world;
    } else {
        // RIGID_DROP cho cliff/wall/layer lớn: không giả chiều cao từng pixel,
        // chỉ đẩy nguyên silhouette theo hướng bóng.
        vec2 offset_world = dir_world * cast_length_world;
        VERTEX += inv_basis * offset_world;
    }
}

void fragment() {
    float source_alpha = texture(TEXTURE, UV).a;
    if (source_alpha < caster_alpha_cutoff) {
        discard;
    }

    vec3 night_tint = vec3(0.035, 0.045, 0.075);
    vec3 tint = mix(
        caster_tint.rgb,
        night_tint,
        clamp(shadow_night_factor * 0.42, 0.0, 0.42)
    );
    float alpha = source_alpha * caster_opacity * shadow_strength;
    COLOR = vec4(tint, alpha);
}
