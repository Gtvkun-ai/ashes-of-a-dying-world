shader_type canvas_item;
render_mode unshaded, blend_mix;

// Ashes Environment V1.5
// Bóng được tạo trực tiếp từ alpha của CHÍNH sprite nguồn.
// Không vẽ ellipse/blob giả nữa: texture cây -> bóng cây, đá -> bóng đá, hoa -> bóng hoa.

uniform vec2 shadow_direction = vec2(0.55, 0.83);
uniform float projection_pixels : hint_range(0.0, 512.0) = 12.0;
uniform float flatten_factor : hint_range(0.02, 1.0) = 0.18;
uniform float base_y_pixels = 8.0;
uniform float virtual_height_pixels : hint_range(1.0, 2048.0) = 32.0;
uniform float projection_mode : hint_range(0.0, 1.0) = 0.0;
uniform vec4 shadow_color : source_color = vec4(0.045, 0.07, 0.04, 0.34);
uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.08;

void vertex() {
    vec2 p = VERTEX;
    vec2 dir = length(shadow_direction) > 0.0001
        ? normalize(shadow_direction)
        : vec2(0.0, 1.0);

    if (projection_mode < 0.5) {
        // Ground-plane projection:
        // - pixel ở chân vật gần như đứng yên;
        // - pixel càng cao càng bị kéo xa theo hướng tia sáng;
        // - trục Y bị ép xuống để sprite trông như nằm trên mặt đất.
        float height01 = clamp(
            (base_y_pixels - p.y) / max(virtual_height_pixels, 1.0),
            0.0,
            1.0
        );

        p.y = base_y_pixels + (p.y - base_y_pixels) * flatten_factor;
        p += dir * projection_pixels * height01;
    } else {
        // Rigid/drop mode cho cliff wall / kiến trúc dạng layer lớn.
        // Không thể ép cả một ảnh map về một chân chung, nên giữ nguyên silhouette
        // và chỉ dịch cả layer theo hướng celestial light.
        p += dir * projection_pixels;
    }

    VERTEX = p;
}

void fragment() {
    float source_alpha = texture(TEXTURE, UV).a;

    // Cắt alpha nền mờ của vài asset tải ngoài (đặc biệt apple_tree.png),
    // tránh tạo một hình chữ nhật xám khổng lồ quanh bóng.
    if (source_alpha < alpha_cutoff) {
        discard;
    }

    COLOR = vec4(shadow_color.rgb, source_alpha * shadow_color.a);
}
