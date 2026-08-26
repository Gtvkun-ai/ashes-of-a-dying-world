# Field 01 V13 - Tree Ground Influence

## Vấn đề
Grounding của cây không được giải quyết chỉ bằng footprint shadow hoặc sprite stain, vì ground/cỏ không hề biết vị trí thật của cây. Kết quả là cây vẫn đọc như đang đặt trên mặt đất thay vì cắm vào mặt đất.

## Hướng 2
V13 triển khai hướng 2 ở mức practical cho repo hiện tại:
- `EnvironmentBinder2D` quét `Props/Trees`
- lấy vị trí gốc cây từ `CollisionShape2D` / scene type
- bake một **tree-ground influence mask** runtime theo canvas của `GroundBase`
- đẩy mask này vào `ground_lighting.gdshader`
- shader ground và dirt path darken/tint cục bộ ngay dưới rễ cây

## Kết quả mong đợi
- cây bám cỏ hơn, nhất là quanh gốc
- path dưới cây bị nén tối nhẹ nếu cây đứng trên path
- không còn phụ thuộc vào một oval shadow riêng để giả lập grounding

## File đổi
- `scripts/World/Environment/EnvironmentBinder2D.cs`
- `assets/shaders/world/ground_lighting.gdshader`
