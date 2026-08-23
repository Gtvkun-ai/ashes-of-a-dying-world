# Environment Rendering V4.5 — Sửa mass shadow không được khởi tạo

## Lỗi V4.4
Log runtime báo:

```text
[EnvironmentMassShadow2D] READY V4.4 | cluster_mass=0 border_mass=0
```

Đây không phải do thuật toán gom cụm cây. Nguyên nhân là `GetTree().CurrentScene` trong game
là `screen_main`, trong khi `field_01` nằm sâu bên trong. V4.4 tìm đường dẫn `Props/AppleTrees`
và `Props/Trees` từ `screen_main`, nên không thấy cây nào.

## V4.5
- `EnvironmentMassShadow2D` đi ngược ancestor để tìm map gần nhất có `Props/Trees` hoặc
  `Props/AppleTrees`.
- Có fallback recursive từ CurrentScene nếu hierarchy sau này thay đổi.
- Log READY in ra luôn path root đã tìm được.
- Giảm nhẹ opacity mass trong lần đầu bật thật để tránh cụm cây/biên map thành mảng đen quá mạnh.

Với Field 1 hiện tại, kỳ vọng log có ít nhất:
- `cluster_mass=1` cho cụm 4 apple tree bên trái.
- nhiều `border_mass` cho hàng cây bao quanh map.

Nếu vẫn ra 0, log `root=...` sẽ cho biết chính xác component đang bind vào scene nào.
