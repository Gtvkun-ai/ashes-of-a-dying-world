# Shadow Core V2.1 — stable affine ground projection

## Vì sao V2 bị bỏ
V2 dùng expanded quad + inverse sampling trong fragment shader. Về toán nghe có vẻ ngầu, nhưng với pixel art thực tế nó tạo:
- sọc/hole ở shadow của character;
- silhouette bị kéo rách;
- bounds/scale khó đoán;
- debug rất khó vì projection vừa nằm ở C# vừa nằm trong remap UV.

Đó là abstraction sai cho asset hiện tại.

## V2.1
V2.1 vẫn giữ kiến trúc tốt của V2:
- một `ShadowRenderer2D`;
- một shared `ShaderMaterial`;
- profile dùng chung theo loại asset;
- caster không `_Process()`;
- instance uniform cho data riêng.

Nhưng projection được thay bằng **một affine transform duy nhất ở vertex shader**.
UV của texture không bị remap thủ công nữa.

### Projection basis
Với vật thể dựng đứng:
- trục ngang X của asset được đặt theo vector vuông góc với bóng;
- chiều cao của asset được đặt theo `ShadowDirection2D`;
- điểm chân asset là anchor cố định.

Do đó 06h/18h có thể nằm ở hai phía hoàn toàn khác nhau mà không collapse.

## Celestial direction
Day shadow orbit:
- sáng: khoảng 30°
- trưa: 90° (bóng rất ngắn)
- chiều: khoảng 210°

Tức là sáng -> chiều quay đủ 180°.
Night orbit tiếp tục 210° -> 390°.

## Debug
`Ctrl+F9` đổi 06/12/18/00.
Output có thêm `shadowAngle=...deg`.
Nếu 06 và 18 không cách nhau gần 180°, lỗi nằm ở environment state chứ không phải shader.
