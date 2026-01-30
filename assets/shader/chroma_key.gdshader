shader_type canvas_item;

// Màu nền cần xóa (Mặc định là xanh lá chuẩn)
uniform vec3 chroma_key : source_color = vec3(0.0, 1.0, 0.0);

// Ngưỡng xóa (0.3 - 0.5 thường là ổn)
uniform float similarity : hint_range(0.0, 1.0) = 0.4;

// Độ mượt chuyển đổi alpha (giữ thấp tầm 0.05 - 0.1)
uniform float smoothness : hint_range(0.0, 1.0) = 0.05;

// QUAN TRỌNG: Cường độ khử ám màu (0.0 = giữ nguyên viền xanh, 1.0 = biến viền xanh thành xám/trắng)
uniform float spill : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    vec4 color = texture(TEXTURE, UV);
    
    // --- BƯỚC 1: TẠO MASK (ALPHA) ---
    // Chuyển màu RGB sang YUV để tách độ sáng (Y) và màu sắc (CrCb)
    // Cách này giúp nhận diện màu xanh chính xác hơn là tính distance RGB thông thường
    
    float mask = 0.0;
    
    // Tính khoảng cách màu đơn giản nhưng hiệu quả cho Green Screen
    // Green channel chênh lệch so với Red và Blue càng lớn thì càng là nền
    float max_rb = max(color.r, color.b);
    float k_val = color.g - max_rb; // Độ "xanh" của pixel
    
    // Tính toán độ trong suốt
    float cutoff = similarity - smoothness;
    mask = 1.0 - smoothstep(cutoff, similarity + smoothness, k_val);
    
    // --- BƯỚC 2: DESPILL (KHỬ ÁM MÀU TRÊN VIỀN) ---
    // Nếu pixel này vẫn còn hiện (mask > 0) nhưng có màu Green quá cao
    // Ta sẽ ép màu Green xuống
    
    if (color.g > max_rb) {
        // Tính lượng màu xanh thừa ra
        float spill_val = (color.g - max_rb) * spill;
        
        // Trừ bớt màu xanh đi -> Biến Light Green thành White/Gray
        color.g -= spill_val;
        
        // Tùy chọn: Có thể cộng bù vào Red/Blue để giữ độ sáng (nếu cần)
        // color.r += spill_val * 0.5;
        // color.b += spill_val * 0.5;
    }
    
    COLOR = vec4(color.rgb, color.a * mask);
}