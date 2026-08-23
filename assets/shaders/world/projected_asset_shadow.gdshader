shader_type canvas_item;
render_mode unshaded, blend_mix;

// Ashes Environment V1.6
//
// V1.5 mới chỉ "ép silhouette nằm xuống đất". V1.6 đổi sang phép chiếu ngược (inverse projection):
// - mỗi pixel của bóng được map ngược về pixel nguồn trong asset thật;
// - vector bóng có dấu đầy đủ, nên sáng/chiều sẽ lật trái/phải theo celestial light;
// - phần đỉnh asset đi xa hơn phần chân asset, thay vì mọi thứ chỉ bị bóp dẹt theo một hướng chung.

uniform vec2 shadow_direction = vec2(0.55, 0.83);
uniform float projection_pixels : hint_range(0.0, 512.0) = 12.0;
uniform float flatten_factor : hint_range(0.02, 1.0) = 0.18;
uniform float base_y_pixels = 8.0;
uniform float virtual_height_pixels : hint_range(1.0, 2048.0) = 32.0;
uniform float projection_mode : hint_range(0.0, 1.0) = 0.0;
uniform vec4 shadow_color : source_color = vec4(0.045, 0.07, 0.04, 0.34);
uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.08;

// Kích thước quad output (đã bao gồm padding do C# mở rộng) và vị trí sprite nguồn bên trong quad đó.
uniform vec2 canvas_size_pixels = vec2(64.0, 64.0);
uniform vec2 source_origin_pixels = vec2(16.0, 16.0);
uniform vec2 source_size_pixels = vec2(32.0, 32.0);

void vertex() {
    // Quad đã được C# mở rộng sẵn bằng scale. Vertex giữ nguyên để fragment làm inverse projection.
}

void fragment() {
    vec2 dir = length(shadow_direction) > 0.0001
        ? normalize(shadow_direction)
        : vec2(0.0, 1.0);

    // Tọa độ hiện tại trên mặt đất, quy về local pixel-space của sprite nguồn.
    vec2 ground_px = UV * canvas_size_pixels - source_origin_pixels;

    vec2 source_px = vec2(-1.0);

    if (projection_mode < 0.5) {
        // Ground-plane projection.
        // Công thức thuận của V1.6:
        //   qx = x + dir.x * P * h
        //   qy = base_y + (y - base_y) * F + dir.y * P * h
        //   h  = (base_y - y) / H
        //
        // Shader bóng làm NGƯỢC lại: từ pixel q trên mặt đất suy ra pixel (x, y) trong asset gốc.
        float denom = (dir.y * projection_pixels / max(virtual_height_pixels, 1.0)) - flatten_factor;
        if (abs(denom) < 0.0001) {
            discard;
        }

        float t = (ground_px.y - base_y_pixels) / denom;
        float y = base_y_pixels - t;
        float h01 = t / max(virtual_height_pixels, 1.0);
        float x = ground_px.x - dir.x * projection_pixels * h01;
        source_px = vec2(x, y);
    } else {
        // Rigid-drop mode cho cliff / wall / layer map lớn.
        source_px = ground_px - dir * projection_pixels;
    }

    if (source_px.x < 0.0 || source_px.y < 0.0 || source_px.x >= source_size_pixels.x || source_px.y >= source_size_pixels.y) {
        discard;
    }

    vec2 source_uv = source_px / max(source_size_pixels, vec2(1.0));
    float source_alpha = texture(TEXTURE, source_uv).a;

    if (source_alpha < alpha_cutoff) {
        discard;
    }

    COLOR = vec4(shadow_color.rgb, source_alpha * shadow_color.a);
}
