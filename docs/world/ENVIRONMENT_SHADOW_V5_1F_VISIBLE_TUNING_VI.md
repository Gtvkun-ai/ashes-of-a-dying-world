# V5.1f — Visible Shadow Tuning

Pass này cố ý tạo khác biệt nhìn thấy rõ.

- Compact canonical mask: depth ~= 0.34 * width.
- Medium: depth ~= 0.62 * width.
- Long: depth ~= 1.28 * width.
- Runtime chỉ rotate + uniform-scale, không kéo dọc footprint lần hai.
- Individual shadow alpha giảm còn ~0.31/0.32.
- Mass shadow nhỏ và nhẹ hơn để chỉ dồn cụm, không tạo mảng bùn.
- Log Binder + MassShadow đổi thành `V5.1f` để xác nhận patch đã chạy.
