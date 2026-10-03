# UI Visual Language Redesign - Tàn Sinh

Ngày: 2026-10-03  
Trạng thái: Đã duyệt hướng thiết kế, chờ duyệt spec  
Phạm vi: B+ - art bible, asset UI mới và tích hợp các asset đang chạy trong Godot

## 1. Mục tiêu

Tái cấu trúc ngôn ngữ hình ảnh của toàn bộ UI/HUD để loại bỏ cảm giác asset sinh tự động, fantasy chung chung và các cụm phong cách không liên quan nhau. Kết quả phải tạo thành một hệ UI có thể mở rộng, đọc tốt trong combat, hỗ trợ tiếng Việt và phản ánh đúng ý đồ của **Tàn Sinh**:

> Thế giới vẫn đẹp và sống động trong khi phần cốt lõi bên dưới đang dần tàn đi.

UI không biến toàn bộ game thành hoang tàn. Bề mặt giao diện vẫn ấm, rõ và có sức sống; dấu hiệu “Tàn” xuất hiện có kiểm soát qua vật liệu lì, đồng xỉn, cạnh bị xói nhẹ, khoảng tối và trạng thái mất màu.

## 2. Phạm vi

### Bao gồm

- Viết lại `docs/visual_language.html` và `docs/visual_language_doc.md` thành art bible có thể dùng trong production.
- Sửa đường dẫn asset hỏng và loại nội dung kỹ thuật không thuộc visual language.
- Xây dựng token dùng chung cho màu, spacing, typography, border, icon, state và motion.
- Thay asset hình ảnh đang hoạt động nhưng mang cảm giác AI/generic.
- Tích hợp bộ visual mới vào UI/HUD runtime hiện có.
- Chuẩn hóa typography tiếng Việt.
- Sửa các lỗi bố cục và readability phát hiện trong audit.
- Dọn asset trùng hoặc legacy sau khi xác nhận không còn callsite.
- Tạo preview mới cho HUD và các component cốt lõi.

### Không bao gồm

- Không chỉnh sửa hoặc đánh giá lại tranh minh họa/portrait Hikaru và Hyou.
- Không thay đổi gameplay, combat balance, quest logic hoặc save data.
- Không triển khai Map và Achievements hoàn chỉnh; hai entry này sẽ được ẩn hoặc disable cho tới khi có nội dung thật.
- Không đổi art direction của world map sang hậu tận thế. Map sống động là chủ ý thiết kế.
- Không viết lại toàn bộ cấu trúc panel đã hoạt động nếu helper hiện tại có thể tái sử dụng.

## 3. Chẩn đoán hiện trạng

Repo hiện trộn nhiều hệ thị giác:

- Nút gradient bóng kiểu web/mobile fantasy.
- Khung JRPG nâu-vàng nhiều chi tiết render.
- Login xanh-xám giống ứng dụng desktop.
- Icon menu manga, tranh khắc sepia và sci-fi cyan.
- Inventory giả pixel từ nguồn 500-790 px rồi thu xuống 16-24 px.
- HUD party từ nguồn hơn 1000 px bị ép về `300x97`.
- Một số panel code-first đã dùng palette nâu khá nhất quán nhưng chưa có theme/font chung.

Nguồn gốc cảm giác “AI vibe” không nằm ở layout chính mà chủ yếu ở:

1. Chi tiết trang trí không có quy luật lặp.
2. Canvas và silhouette thay đổi giữa các state.
3. Asset nguồn quá lớn rồi thu nhỏ bằng nearest.
4. Vật liệu glossy/chrome không liên quan tới thế giới.
5. Nhiều phong cách icon khác nhau cùng xuất hiện trên một màn.
6. Chữ, icon và nhãn bị bake vào texture.

## 4. Định hướng cốt lõi

Tên nội bộ của hệ UI mới: **Living Ash / Tàn Sinh**.

### 4.1 Ba lớp biểu đạt

**Sinh**

- Bề mặt ấm, rõ, có nhịp.
- Accent xanh rêu, tím hoa hoặc màu nhân vật chỉ xuất hiện khi có ý nghĩa.
- Motion ngắn, mượt và có phản hồi trực tiếp.

**Tàn**

- Than ấm, sắt tối, đồng xỉn và vùng màu bị rút bớt.
- Cạnh xói rất nhẹ, không dùng texture nứt dày đặc.
- Disabled, depleted và corrupted được thể hiện bằng mất màu/chuyển động, không chỉ phủ đen.

**Giao thoa**

- Focus/selection là nơi ánh đồng hoặc màu sống nổi lên trên nền tối.
- Hyou dùng xanh băng có kiểm soát; không biến mọi UI mana thành cùng một glow cyan.
- Quest/memory có thể dùng tím hoa nhưng luôn kèm icon hoặc hình dạng riêng.

### 4.2 Nguyên tắc hình thức

- Cấu trúc trước, trang trí sau.
- Một component chỉ có một silhouette rõ ràng.
- Ornament giới hạn ở một motif góc đồng bị mòn.
- Không filigree, chrome bạc, glass gloss hoặc nhiều viền lồng nhau.
- Không dùng texture để giả chi tiết ở kích thước mà người chơi không nhìn thấy.
- Trạng thái phải phân biệt được bằng value, hình dạng hoặc chuyển động, không dựa vào màu đơn độc.

## 5. Hệ token

### 5.1 Màu nền và chữ

| Token | Giá trị khởi điểm | Vai trò |
|---|---:|---|
| `surface_void` | `#120F0C` | scrim, vùng sâu nhất |
| `surface_base` | `#1D1813` | nền cửa sổ |
| `surface_raised` | `#29221B` | panel/card được nâng |
| `surface_active` | `#352C22` | hover/selected nhẹ |
| `border_soft` | `#5C4B38` | border thường |
| `border_strong` | `#8A6B42` | focus/section chính |
| `accent_brass` | `#C39A57` | action/focus có kiểm soát |
| `text_primary` | `#F0E5D2` | chữ chính |
| `text_secondary` | `#BFAF98` | chữ phụ |
| `text_disabled` | `#756B5D` | disabled |
| `danger` | `#B9574F` | nguy hiểm/HP thấp |
| `life` | `#7FA56D` | stamina/sinh lực |
| `memory` | `#9A72C7` | memory/companion quest |
| `ice` | `#62B8D9` | Hyou/ice semantic |

Mọi cặp text/background đang hoạt động dùng cho chữ nhỏ phải đạt WCAG AA `4.5:1`. Disabled text có thể thấp hơn nhưng vẫn phải đọc được trong bối cảnh; accent trang trí không được thay thế text label hoặc icon semantic.

### 5.2 Spacing và kích thước

- Grid cơ sở: `4 px`.
- Spacing: `4, 8, 12, 16, 24, 32 px`.
- Border: `1 px`; selection đặc biệt tối đa `2 px`.
- Corner radius: `0-3 px` cho phần lớn UI; pill chỉ dùng cho badge/tag thực sự.
- Icon: `16, 20, 24, 32 px`; portrait và item preview là ngoại lệ có kích thước riêng.
- Button height: `32, 40, 48 px`.
- Status badge: `24 px` trong frame `28 px`.
- Mọi pixel asset phải được vẽ ở kích thước native hoặc scale nguyên.

### 5.3 Motion

- Hover: `80-120 ms`, đổi value/border; không phóng to layout.
- Press: `60-90 ms`, nội dung dịch tối đa `1 px`.
- Panel enter/exit: `140-220 ms` với opacity và offset nhỏ.
- Cooldown/status pulse: biên độ thấp, không dùng glow liên tục trên mọi icon.
- `prefers reduced motion` tương đương trong setting sẽ bỏ pulse/slide không cần thiết.

## 6. Typography

### 6.1 Font runtime

Be Vietnam Pro là font UI mặc định vì đã phù hợp tiếng Việt. Project cần bundle tối thiểu:

- Regular 400
- Medium 500
- SemiBold 600
- Italic 400
- SemiBold Italic 600

Cinzel và IM Fell English không được dùng trong runtime cho tới khi có file font, license và kiểm thử đầy đủ dấu tiếng Việt. Art bible không được hứa một font mà build không có.

### 6.2 Scale

| Role | Size | Weight |
|---|---:|---:|
| Screen title | 22-24 | 600 |
| Section title | 16-18 | 600 |
| Body | 13-15 | 400 |
| Label | 11-12 | 500/600 |
| Micro | 10-11 | 500 |
| Combat number | 14-24 | 600 |

- Không letter-spacing lớn trên tiếng Việt ở kích thước nhỏ.
- Tên dài phải wrap, ellipsis có tooltip hoặc co vùng phụ; không clip im lặng.
- Bold/italic phải dùng font face thật, không gán Regular cho mọi variation.

## 7. Component strategy

### 7.1 Shared chrome

Giữ API và cấu trúc của:

- `scripts/UI/HUD/InventoryPanelChrome.cs`
- `scripts/UI/HUD/PixelButtonSkin.cs`
- Quest journal hai cột
- Party panel
- Skill graph
- Dialog responsive

Thay visual implementation bằng token dùng chung. Ưu tiên `StyleBoxFlat`; chỉ dùng texture cho grain nhẹ hoặc motif không thể diễn đạt bằng primitive.

Một lớp token/theme chung phải cung cấp:

- Palette và typography.
- Button/panel/slot/tab/badge styles.
- Resource bar styles.
- Focus outline.
- Icon sizing và state colors.

Các screen không tự tạo palette riêng nếu không có semantic đặc biệt.

### 7.2 Button

- Một canvas và silhouette cho normal/hover/focus/pressed/disabled.
- Không bake text.
- Focus khác hover: focus có border/marker rõ cho keyboard/gamepad.
- Press không đổi kích thước component.
- Có primary, secondary và danger; tab là state của cùng hệ chứ không phải họ asset mới.

### 7.3 Icon

- Bộ glyph duy nhất theo lưới `24 px`, nét `1-2 px`.
- Menu/category dùng icon đơn sắc; màu đến từ state của component.
- Item art có thể chi tiết hơn nhưng không được dùng thay cho navigation glyph.
- Atlas phải có cell nguyên, không rác, không crop số lẻ.
- Mỗi semantic quan trọng có hình dạng riêng để hỗ trợ người mù màu.

## 8. HUD runtime

### 8.1 Party HUD

- Giữ vị trí neo bên phải.
- Kích thước khởi điểm vẫn là `300x97`, nhưng scene được dựng lại bằng component native thay vì ảnh frame `1423x458` bị ép nhỏ.
- Portrait là layer độc lập và không bị chỉnh sửa artwork.
- Tên, level, skill, resource và status là node độc lập.
- HP/MP/STA không bake label vào texture.
- Thanh resource dùng trough, fill và state low-resource; không glossy.
- Status strip phải nằm trong chiều cao layout hoặc container có reserved space, không treo xuống unit kế tiếp.
- Selected character dùng border/accent `1-2 px`, không dùng outline trắng `20 px`.

### 8.2 World HUD stack

Các lớp trên actor phải có lane/stack chung:

1. Target marker
2. Enemy health/status
3. Damage/heal numbers
4. XP/level feedback

Không component nào tự chọn offset âm độc lập mà không qua coordinator. Target marker phải là icon pixel thật, không dùng ký tự font `▼`.

### 8.3 Enemy health

- Có trough, fill và frame native.
- Đọc được cả khi gần hết máu.
- Có vùng dành cho status effect nhưng không che thanh.
- Boss/elite là variant có chủ đích, không chỉ phóng to thanh thường.

### 8.4 Companion command menu

- Giữ logic context menu hiện tại.
- Dùng cùng panel/button/token với menu khác.
- Bản V2 được dùng tạm làm reference; asset vàng bóng cũ bị loại.

## 9. Screen UI

### 9.1 Main screen

Màn `LOGIN` hiện tại được thay hoàn toàn bằng main menu đúng ngữ nghĩa:

- Nút chính hiển thị `Tiếp tục` khi có save và `Bắt đầu` khi chưa có save
- `Trò chơi mới` chỉ hiện khi đã có save và dùng luồng khởi tạo mới hiện có
- Cài đặt
- Thoát

Không bake chữ vào ảnh. Dùng Control/container responsive, keyboard/gamepad focus và typography tiếng Việt. Giữ `StartGameFromSnapshotAsync` cùng logic load/quit hiện có trong `ScreenMain.cs`; luồng UI mới không tự xóa hoặc sửa save data.

### 9.2 Game menu

- Thay avatar `main_hud.png` bằng icon menu `32-48 px`.
- Menu grid có scrim và panel skin rõ.
- Character, Inventory, Skills, Quests, Party, Settings dùng cùng icon set.
- Map và Achievements bị ẩn/disable cho tới khi có nội dung.
- Party không tái sử dụng icon Character.

### 9.3 Inventory, Character, Party, Quest, Skill, Settings

- Giữ workflow và layout chính đã có.
- Việt hóa toàn bộ string hiển thị.
- Xóa mock data như số tiền cố định.
- Placeholder được chuyển thành empty state có chủ đích.
- Control trông tương tác phải thực sự tương tác; label giả dropdown bị loại hoặc đổi thành `OptionButton`.
- Skill node trở về cùng radius/border/grid của hệ UI và hỗ trợ tên hai dòng.
- Slider, checkbox và option control được skin đồng nhất.
- Copy kỹ thuật/developer-facing trong Settings được viết lại cho người chơi.

### 9.4 Dialog

- Giữ cụm frame pixel hiện tại và các chỉnh sửa đang có trong worktree.
- Choice hỗ trợ wrap, chiều rộng responsive và scroll khi nhiều lựa chọn.
- Focus state phải khác hover state.
- Asset choice không dùng phải được dọn sau khi xác nhận style thực tế dùng `StyleBoxFlat`.

## 10. Asset policy

### Giữ

- `assets/graphics/ui/dialog/map_dialog/*`
- `assets/graphics/ui/hud/status_effects/*`
- Portrait/character illustration do người dùng quản lý
- `inventory/grain.png` chỉ khi kiểm tra tile seam đạt yêu cầu; nếu không sẽ thay bằng tile native nhỏ

### Thay

- `assets/graphics/ui/menus/login/*`
- `assets/graphics/ui/buttons/button_{primary,secondary,danger}_*.png`
- `assets/graphics/ui/buttons/{character,inventory}.png`
- `assets/graphics/ui/hud/main_hud.png`
- `assets/graphics/ui/hud/redesign/unit_hud_*`
- `assets/graphics/ui/status/enemy_hp_bar.png`
- `assets/graphics/ui/icons/{menu_action_icons_sheet,stat_icons_sheet}.png`
- `assets/graphics/ui/inventory/category_*.png`
- Các main stat frame/fill sẽ được re-export native hoặc thay bằng code style

### Dọn sau khi xác nhận callsite

- Ba `hud/redesign/resource_frame_*` trùng với `unit_hud_*_fill`
- `hud_preview_annotated.png` khỏi runtime asset directory
- `hud/companion_command_menu_panel.png`
- `hud/companion_command_menu_button.png`
- `status/resource_panel_frame.png`
- `status/resource_frame_*.png`
- `StatHexagonChart.cs` nếu vẫn không có callsite
- Hidden cooldown HUD compatibility path nếu không còn consumer

Không xóa asset trước khi runtime reference scan và test xác nhận an toàn.

## 11. Accessibility và readability

- Text nhỏ đạt contrast tối thiểu `4.5:1`.
- Không dùng màu đơn độc để phân biệt resource/status/quest type.
- Focus keyboard/gamepad luôn nhìn thấy.
- Tooltip phải nhận input; node có tooltip không được để `MouseFilter.Ignore` nếu điều đó làm tooltip vô hiệu.
- HUD được kiểm tra ở `1600x900`, viewport nhỏ hơn và UI scale lớn.
- Tên tiếng Việt dài, dấu kết hợp và text wrap được kiểm thử.
- Animation không được làm thay đổi kích thước layout.

## 12. Migration sequence

1. Tạo token/theme và typography chung.
2. Thay button system bằng native styles hoặc 9-slice native sạch.
3. Rebuild party HUD và enemy/world HUD stack.
4. Rebuild main screen và game-menu launcher.
5. Thay menu/category/stat icon set.
6. Reskin Inventory/Character/Party/Quest/Skill/Settings qua shared chrome.
7. Hoàn thiện dialog choice và focus states mà không ghi đè thay đổi hiện có.
8. Cập nhật art bible và preview bằng runtime mới.
9. Scan/xóa asset legacy, sửa test contract và import.

Mỗi bước phải giữ project chạy được; asset cũ chỉ bị xóa sau khi consumer cuối cùng đã chuyển.

## 13. Kiểm thử và nghiệm thu

### Automated

- Chạy các contract test hiện có trong `tools/tests`.
- Bổ sung test cho đường dẫn asset mới, font/theme và absence của legacy runtime references.
- Kiểm tra scene/resource parse bằng Godot headless.
- Kiểm tra C# build.

### Visual

- Chụp main screen, world HUD, party HUD, menu grid, inventory, quest, party, skill, settings và dialog.
- Kiểm tra desktop `1600x900` và viewport nhỏ hơn.
- Kiểm tra normal/hover/focus/pressed/disabled.
- Kiểm tra combat đồng thời: target + enemy HP + status + damage + XP.
- Kiểm tra party ba thành viên có status mà không overlap.
- Kiểm tra tên dài và text tiếng Việt.
- So sánh screenshot với preview trong art bible.

### Tiêu chí hoàn thành

- Không còn asset runtime active mang một hệ vật liệu khác biệt rõ rệt.
- Không còn texture source quá lớn bị thu xuống kích thước icon nhỏ bằng nearest.
- Không còn chữ tiếng Anh bake trong asset hoặc copy prototype trên màn chính.
- UI gameplay và UI menu dùng cùng token, typography và icon grammar.
- Portrait vẫn là artwork độc lập của người dùng.
- Map sáng và sống động vẫn đúng chủ ý; UI không áp đặt tông hậu tận thế lên world art.
- Art bible mô tả đúng build thật, không chứa link cục bộ hỏng hoặc font chưa tồn tại.

## 14. Rủi ro và biện pháp

- **Blast radius lớn:** chuyển theo từng subsystem và giữ fallback trong lúc migration.
- **Dirty dialog worktree:** đọc và tích hợp thay đổi hiện tại, không reset hoặc ghi đè.
- **Asset import churn:** hạn chế đổi tên không cần thiết; cập nhật UID/reference có kiểm soát.
- **Typography thay đổi layout:** kiểm thử text dài trước khi khóa kích thước.
- **Xóa nhầm legacy:** dùng reference scan, Godot load và tests trước khi xóa.
- **Style đẹp nhưng combat khó đọc:** ưu tiên value hierarchy và screenshot runtime hơn mockup tĩnh.
