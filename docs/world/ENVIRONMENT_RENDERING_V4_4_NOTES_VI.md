# Environment Rendering V4.4 — Mass Shadow / Cliff / Border Pass

## Mục tiêu

V4.3 đã sửa được hình học bóng cây: bóng không còn thành kim/vệt méo và footprint lúc 12h đã đọc đúng hơn.
V4.4 không thay phép chiếu đó nữa. Bản này thêm **lớp bóng khối lớn (mass shadow / penumbra)** để nhiều cây đứng gần nhau trông như một grove thật, thay vì mỗi cây tự có một dấu oval riêng.

## Thay đổi chính

### 1. `EnvironmentMassShadow2D.cs` — mới

- Scan scene **một lần** khi load map, không scan mỗi frame.
- Tự tìm `Props/AppleTrees` và gom cây gần nhau thành cluster.
- Cluster có từ 3 cây trở lên sẽ nhận một penumbra mềm chung.
- Bóng từng cây của `ShadowCaster2D` vẫn giữ nguyên, nên mass shadow chỉ làm nhiệm vụ nối các bóng lại với nhau.
- Tự tìm hàng cây biên trong `Props/Trees` và tạo shadow pool theo từng đoạn.
- Border shadow có `inward gate`: chỉ mạnh khi hướng bóng thực sự đi vào trong map. Khi bóng đi ra ngoài map, nó gần như biến mất.
- Ban đêm mass shadow giảm mạnh để moonlight vẫn sạch và dễ đọc.

### 2. `mass_shadow_blob_v44.png` — mới

- Mask penumbra riêng, mềm hơn contact shadow.
- Core không quá đen để khi chồng với bóng cá thể vẫn giữ texture cỏ.
- Dùng Linear filtering riêng; sprite/art pixel gốc vẫn giữ Nearest.

### 3. `EnvironmentBinder2D.cs`

- Tạo `EnvironmentMassShadow2D` runtime cùng ShadowRenderer.
- Cùng nhận một `EnvironmentState`, nên hướng/độ dài/đêm/mây luôn đồng bộ với bóng cây hiện tại.
- Log mới: `shadow_core=V4.4-mass+alpha-bounds`.

### 4. Cliff shadow

`cliff_wall.tres` được chỉnh theo nguyên tắc vật thể thấp/rộng:

- `NoonLengthWorld`: 3 -> 2
- `MaxLengthWorld`: 42 -> 28
- `Opacity`: 0.30 -> 0.34

Cliff không nên quăng một silhouette dài như cây; nó cần core ngắn hơn nhưng có trọng lượng hơn sát chân vách.

## Art direction

- **12:00:** individual footprint là chính; mass shadow chỉ là vùng tối rất nhẹ dưới grove.
- **06:00 / 18:00:** mass mở dài và nối các bóng cây thành một khối lớn hơn.
- **00:00:** mass giảm mạnh, tránh làm đêm bị bùn/đen.
- **Border:** bên nào đang quay vào hướng bóng thì có pool rõ hơn; phía ngược lại tự fade.

## Test đề nghị

Giữ cùng một camera rồi chụp:

1. 12:00 — kiểm tra cluster 4 cây không thành một mảng đen.
2. 18:00 — kiểm tra bóng cluster nhập thành một mass chung.
3. 06:00 — kiểm tra mass lật sang phía đối diện.
4. 00:00 — kiểm tra moonlight vẫn sạch, mass gần như chỉ còn AO nhẹ.

Nếu mass shadow hơi mạnh/yếu, chỉnh **Opacity trong `EnvironmentMassShadow2D.cs`**, không cần đụng lại phép projection V4.3.
