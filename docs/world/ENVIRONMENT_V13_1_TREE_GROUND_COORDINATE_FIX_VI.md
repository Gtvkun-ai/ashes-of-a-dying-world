# Field 01 V13.1 - Tree Ground Coordinate Fix

## Root cause V13 nhìn như không có tác dụng
Field 01 dùng ground source `456x474` nhưng render ở scale x4 (`1824x1896` world px).

V13 đã:
- ghi **world-space position** của cây trực tiếp vào mask `456x474`
- shader cũng chia `world_pos` cho `456x474`

=> phần lớn cây ở x/y > 456/474 bị clamp ra cạnh mask. Log vẫn báo `roots=267`, nhưng mask nằm sai coordinate space nên gần như không thấy gì.

## V13.1 sửa
- bake mask bằng `GroundBase.ToLocal(rootWorldPosition)` để đưa root về source-pixel coordinate
- convert radius world -> source pixel theo `GroundBase.GlobalScale`
- shader sample mask bằng `world_origin + world_size` (`1824x1896` ở Field 01)
- tăng nhẹ darken/tint để ground response đọc ra rõ nhưng vẫn không thành shadow blob
- log mới in `source`, `scale`, `world`, `painted` để xác nhận mapping đúng

Expected log:
`TREE_GROUND_V13.1 | trees=267 painted=... | source=456x474 scale=(4,4) world=1824x1896`
