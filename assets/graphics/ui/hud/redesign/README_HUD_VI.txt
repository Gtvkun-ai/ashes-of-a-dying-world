HUD REDESIGN - GHI CHÚ TÍCH HỢP
================================

Bộ HUD này được tách thành đúng 5 ảnh để Godot dễ thay đổi portrait, tên và giá trị thanh theo nhân vật.

1) unit_hud_frame.png
   - Nền HUD chính: khung nâu + viền vàng + nameplate + rãnh HP/MP/STA.
   - Có sẵn chữ HP / MP / STA.
   - KHÔNG chứa tên Hikaru và KHÔNG chứa số stat, vì hai phần đó nên là dữ liệu động.

2) unit_hud_portrait_frame.png
   - Viền trang trí đặt ĐÈ LÊN portrait.
   - Phần giữa có alpha trong suốt để ảnh nhân vật hiện bên dưới.

3) unit_hud_hp_fill.png
   - Phần fill màu đỏ của HP.
   - Dùng làm TextureProgressBar.texture_progress.

4) unit_hud_mp_fill.png
   - Phần fill màu xanh dương của MP.
   - Dùng làm TextureProgressBar.texture_progress.

5) unit_hud_stamina_fill.png
   - Phần fill màu xanh lá của Stamina.
   - Dùng làm TextureProgressBar.texture_progress.

THỨ TỰ LAYER TRONG SCENE
------------------------
TextureRect (unit_hud_frame)
├─ Portrait               <- icon nhân vật động
├─ PortraitFrame          <- unit_hud_portrait_frame, nằm đè lên portrait
├─ NameLabel              <- tên nhân vật động
└─ BarsContainer
   ├─ HPBar               <- unit_hud_hp_fill
   ├─ MPBar               <- unit_hud_mp_fill
   └─ StaminaBar          <- unit_hud_stamina_fill

CÁC FILE PROJECT ĐÃ SỬA
-----------------------
- scenes/ui/hud/character_unit_hud.tscn
  + Chuyển sang bộ asset redesign.
  + HUD mới: 300 x 97 px.
  + Thêm PortraitFrame.
  + Căn lại portrait, name và 3 resource bar.

- scenes/ui/hud/party_hud.tscn
  + Đổi chiều rộng vùng HUD bên phải từ 240 px thành 300 px để khớp HUD mới.

GHI CHÚ GODOT
-------------
- Ba ảnh unit_hud_*_fill.png là tên mới nên chưa có file .import trong ZIP.
  Godot sẽ tự tạo .import khi mở project lần đầu. Đây là hành vi bình thường.
- Các asset cũ trong assets/graphics/ui/status/ vẫn được giữ nguyên để không phá scene khác.
- CharacterUnitHUD.cs không cần đổi logic: MaxValue/Value của HP, MP và Stamina vẫn cập nhật như cũ.

TỈNH CHỈNH NHANH
----------------
Nếu muốn HUD nhỏ hơn: giảm custom_minimum_size trong character_unit_hud.tscn,
sau đó scale các offset theo cùng tỉ lệ. Frame gốc có tỉ lệ khoảng 3.1 : 1.
