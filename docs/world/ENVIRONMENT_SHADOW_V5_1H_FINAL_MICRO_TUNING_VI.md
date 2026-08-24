# V5.1h — Final micro tuning

Theo feedback mới:
- compact phải loan rộng hơn và dính nhau hơn
- bóng phải bo sát từ dưới thân cây
- có thể kéo dài thêm nhẹ cho đẹp

## Điều chỉnh
- `ShadowCaster2D`
  - compact/medium/long `widthFactor` tăng lên
  - `centerBias` giảm rõ để lõi bóng bắt đầu sát gốc
  - alpha trần tăng rất nhẹ
- `EnvironmentMassShadow2D`
  - cluster mass to hơn và đậm hơn một chút
  - vị trí mass shadow gần tâm cụm hơn
  - apple cluster noon/horizon width+depth tăng để compact nhập khối tốt hơn
- profile
  - `WidthScale` và `Opacity` tăng nhẹ để bóng đọc rõ hơn nhưng vẫn mềm
