# Field 01 V12.1 - Tree Root Stain

V12 thêm contact shadow mạnh hơn nhưng feedback cho thấy vấn đề không phải chỉ là thiếu bóng đen dưới chân. Cảm giác "bay" đến từ việc đất ngay vùng rễ chưa bị nhuộm tối / dính đất đủ.

## Sửa ở V12.1
- thêm `RootGrounding` sprite dưới từng tree/apple_tree
- dùng texture dạng **soil stain** olive-brown mềm, không phải bóng đen
- giảm lại `ContactShadow` của tree profile để tránh double-dark và tránh nhìn như decal đen
- giữ trunk root AO ở mức vừa (`0.13`)

## File thay đổi
- `assets/graphics/environment/shadows/v5_1/tree_root_grounding_v121.png`
- `assets/graphics/environment/shadows/v5_1/apple_tree_root_grounding_v121.png`
- `scenes/world/props/trees/tree.tscn`
- `scenes/world/props/trees/apple_tree.tscn`
- `data/world/shadows/tree_tall.tres`
- `data/world/shadows/apple_tree.tres`
