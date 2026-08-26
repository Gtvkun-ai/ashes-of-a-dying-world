# Field 01 V12.2 - Root Grounding Alignment Fix

V12.1 gần như không thấy vì `RootGrounding` được đặt ở y=18/20 trong khi đáy rễ thực tế của tree nằm khoảng local y=73 (tree) và y=84 (apple tree). Texture patch vì vậy nằm cao hơn chân rễ hàng chục pixel.

V12.2 sửa theo cách chắc chắn hơn:
- root-grounding texture có **cùng canvas size** với trunk texture
- dùng **cùng Sprite2D offset** với trunk (`-13` / `-14`)
- patch được sinh trực tiếp từ alpha silhouette phần rễ, nên luôn bám đúng vị trí rễ
- patch là đất/grass compressed olive-brown, không phải oval bóng đen
- giảm contact shadow vì soil stain mới là grounding chính

## Adaptive terrain darkening
V12.2 còn thêm `root_grounding.gdshader` dùng `blend_mul`. Root stain không có màu đất cố định nữa: nó **nhân tối trực tiếp terrain đang nằm dưới cây**, nên trên grass vẫn là grass tối hơn, trên dirt/path vẫn là dirt/path tối hơn.
