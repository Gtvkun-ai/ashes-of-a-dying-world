shader_type canvas_item;

uniform float speed = 1.0;
uniform float strength = 3.0;

// Thêm biến này để chỉnh vị trí "chốt" gốc cỏ
uniform float height_offset = 0.0; 

void vertex() {
	// Lấy tọa độ Y của điểm ảnh (VERTEX.y)
	// Nếu điểm ảnh nằm càng cao (ngọn cỏ) thì giá trị càng thấp (âm)
	// Nếu điểm ảnh nằm càng thấp (gốc cỏ) thì giá trị càng cao (dương)
	
	float time = TIME * speed;
	
	// Tính toán độ rung
	// VERTEX.y càng lớn (càng gần gốc) thì rung càng ít
	// Trừ đi height_offset để tìm đúng điểm gốc bằng 0
	float sway_amount = sin(time) * strength;
	
	// Áp dụng rung: Chỉ rung những phần nằm cao hơn gốc (VERTEX.y < height_offset)
	// Hàm max(0.0, ...) giúp phần dưới gốc luôn trả về 0 (đứng im)
	float mask = max(0.0, height_offset - VERTEX.y);
	
	// Do mask càng lên cao càng lớn -> nhân thêm số nhỏ để hãm bớt nếu cần
	VERTEX.x += sway_amount * mask * 0.05; 
}