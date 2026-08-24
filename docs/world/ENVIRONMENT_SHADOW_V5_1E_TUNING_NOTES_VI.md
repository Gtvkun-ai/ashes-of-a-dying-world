# V5.1e — Shadow Final Tuning Notes

## Mục tiêu
- Compact nhỏ hơn và gần gốc hơn.
- Medium gọn hơn ~10-15%.
- Long giảm bề ngang, đuôi thon hơn nhờ thu width toàn cục.
- Mass shadow lùi vai trò: chỉ gom cụm, không biến ground thành mảng bùn.
- Night cast shadow giảm mạnh.

## Thay đổi chính
- `ShadowCaster2D`: giảm `widthFactor`, giảm `centerBias`, hạ alpha trần, giảm night attenuation.
- `EnvironmentMassShadow2D`: giảm offset, width/depth và opacity của cluster mass; border mass nhẹ hơn.
- `apple_tree.tres` + `tree_tall.tres`: giảm `Opacity`, `WidthScale`, `Flatten` để individual shadow không lấn mass shadow.
