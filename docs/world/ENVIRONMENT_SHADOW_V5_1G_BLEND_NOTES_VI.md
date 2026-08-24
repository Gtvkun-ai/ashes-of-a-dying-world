# V5.1g — Blend / Spread tuning

Theo feedback mới:
- Compact phải loan to hơn và dính nhau rõ hơn.
- Bóng phải bo sát từ dưới thân cây.
- Có thể dài thêm nhẹ để trông đẹp hơn.

## Thay đổi
- Tăng `widthFactor` của authored footprint, đặc biệt ở compact. Vì scale là uniform nên bóng vừa rộng hơn vừa dài hơn một chút.
- Giảm `centerBias` để lõi bóng bắt đầu gần gốc hơn.
- Tăng kích thước/opacity `EnvironmentMassShadow2D` cho cluster noon/golden để các khối compact nhập lại.
- Version string đổi sang `V5.1g` để dễ xác nhận patch đã chạy.
