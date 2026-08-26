# V12.2b Parse Fix

Sửa lỗi parse của `apple_tree.tscn` trong V12.2.

Nguyên nhân: scene có node `RootGrounding` tham chiếu `ExtResource("16_root_grounding")` nhưng thiếu dòng ext_resource tương ứng, làm `apple_tree.tscn` load fail. Khi `field_01.tscn` instantiate scene này thì parse scene dây chuyền fail, dẫn đến cảm giác cây/đá biến mất.

Fix: thêm ext_resource `apple_tree_root_grounding_v122.png` id `16_root_grounding`.
