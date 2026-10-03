# Visual Language — Ashes of a Dying World

> *"Thế giới đang chết — nhưng hoa vẫn nở trên tàn tro."*

**📄 File HTML tương tác:** [visual_language.html](file:///C:/Users/Gtvkun/.gemini/antigravity-ide/brain/ea4771a2-1985-427d-90bb-170f5a82c44a/visual_language.html) ← Mở file này để xem toàn bộ visual language dạng interactive web page (có màu sắc thực, animation, copy hex code khi click swatch).

---

## 01 · Nhận diện tổng quan

| Thuộc tính | Giá trị |
|---|---|
| **Thể loại** | 2D Action RPG · Party · Narrative-driven |
| **Engine** | Godot 4.7 + C# |
| **Viewport** | 1600 × 900 px |
| **Rendering** | GL Compatibility · Pixel-perfect · nearest filter |
| **FPS** | max 144 |
| **Font game** | Be Vietnam Pro (đầy đủ tiếng Việt) |
| **Tông cảm xúc** | Melancholic beauty — buồn nhưng không tuyệt vọng |
| **Ngôn ngữ** | Tiếng Việt xuyên suốt (quest, dialog, UI) |

---

## 02 · Hệ thống màu sắc

### 🌑 World — Tàn Tro & Đất Cháy

| Tên | Hex | Dùng cho |
|---|---|---|
| Ash Black | `#0D0A07` | Nền chính, bầu trời đêm |
| Deep Brown | `#1A110A` | Nền UI panel |
| Charred Wood | `#2C1F10` | Khung HUD, gỗ cháy |
| Warm Shadow | `#3D2E1A` | Bóng ấm |
| Dust Stone | `#6B5C3E` | Text phụ, label mờ |
| Dry Grass | `#8A7A5A` | Body text |
| Dried Wheat | `#C4A96A` | Accent text nhẹ |
| Pale Bone | `#E8D8B4` | Text chính |

### ✦ UI Gold — Khung JRPG Cổ Điển

| Tên | Hex | Dùng cho |
|---|---|---|
| Gold Dim | `#7A5820` | Viền mờ |
| UI Gold | `#C8943C` | Viền HUD chính, heading accent |
| Gold Bright | `#E8C060` | Hover state, highlight |
| Focus Glow | `#D4A050` | Hikaru skill Focus glow |

### ❄ Hyou — Băng Thuật & Linh Hồn

| Tên | Hex | Dùng cho |
|---|---|---|
| Deep Navy | `#1C3A5E` | Bóng tối của Hyou |
| Ice Blue | `#3ABCF5` | Màu nhân vật chính của Hyou |
| Hyou Glow | `#00C8FF` | Ice Bolt skill glow |
| Frost | `#A8DCFF` | Frost effect, frozen aura |
| Ice Clear | `#D4EEFF` | Điểm sáng băng |
| Memory Mist | `#8B5CF6` | Ký ức bị phong ấn (quest Hyou) |

### ⬛ Resource Bars

| Resource | Dark | Bright |
|---|---|---|
| HP (máu) | `#C42B2B` | `#E84040` |
| MP (mana) | `#2B6AC4` | `#4090E8` |
| STA (thể lực) | `#2B9A3A` | `#40C850` |

### ⚡ Status Effects (Hyou)

| Effect | Màu |
|---|---|
| Chill | `#60C0E0` (pulse animation) |
| Slowed | `#A060C0` |
| Frozen | `#20D0F0` (override Chill+Slow) |

---

## 03 · Hệ thống Typography

| Font | Vai trò | Dùng khi nào |
|---|---|---|
| **Cinzel** | Display/Heading | Tên game, chapter title, tên nhân vật HUD, heading UI quan trọng |
| **IM Fell English Italic** | Lore/Narrative | Flavor text, mô tả quest, lời thoại nhân vật |
| **Be Vietnam Pro** | UI/Body | Mọi thứ còn lại: tooltip, stat, objective, settings |

### Scale chữ

- **Display**: Cinzel 900 · ~56px · Tên game
- **Heading**: Cinzel 600 · ~28px · Tên section/quest
- **Body lore**: IM Fell Italic · 16-20px
- **UI body**: Be Vietnam Pro 400 · 14-15px
- **Label**: Be Vietnam Pro 600 · 11px · Letter-spacing 3px · UPPERCASE
- **Micro**: 9-10px · Cooldown number, stack count

---

## 04 · Ngôn ngữ nhân vật

### Hikaru — Nhân vật chính

![Hikaru icon](file:///C:/Users/Gtvkun/.gemini/antigravity-ide/brain/ea4771a2-1985-427d-90bb-170f5a82c44a/hikaru_icon.png)

| Thuộc tính | Giá trị |
|---|---|
| **ID** | `001` |
| **Race** | Human |
| **ThemeColor** | `#000000` (base, earth tones) |
| **Element** | Physical |
| **Palette** | Olive/earth · Nâu đất · Vàng mờ |
| **Skills** | Tập trung (Deep Flow) · Heavy Slash · Quick Slash · Relentless Cut |
| **Thiết kế** | Trang phục bình dị · olive shirt · tóc đen lộn xộn |
| **Ý nghĩa màu** | Không nổi bật — con người bình thường với ý chí phi thường |

**Skill đặc trưng — Tập trung:**
- 18 giây Deep Flow: +8% tốc độ, +15% DEX
- Auto-evasion dựa trên tương quan level/DEX/INT
- Baseline 20% tại ngang trình, scale đến 95% khi vượt trội
- Cooldown: 50 giây

### Hyou — Đồng hành

![Hyou icon](file:///C:/Users/Gtvkun/.gemini/antigravity-ide/brain/ea4771a2-1985-427d-90bb-170f5a82c44a/hyou_icon.png)

| Thuộc tính | Giá trị |
|---|---|
| **ID** | `villain_001` |
| **Race** | Spirit (Ice) |
| **ThemeColor** | `#38BDED` (ice blue) |
| **Element** | Ice (Cryomancy) |
| **Palette** | Ice blue · Deep navy · Frost white · Purple mist |
| **Skills** | Ice Bolt · Frost Ward |
| **Skill tree** | Băng thuật (Cryomancy branch) |
| **Thiết kế** | Tóc xanh twintail · Crystal accessories · Bodysuit băng |
| **Ý nghĩa màu** | Lạnh lùng bên ngoài, ký ức ấm bên trong |

**Stats Spirit Race (cao INT/SPI, thấp STR):**
- STR: 3 · DEX: 15 · INT: 18 · VIT: 8 · **SPI: 20** · LCK: 3

---

## 05 · Hệ thống nguyên tố

| Element | Màu | Emoji |
|---|---|---|
| Physical | `#C8943C` | ⚔ |
| Ice | `#3ABCF5` | ❄ |
| Fire | `#F05030` | 🔥 |
| Lightning | `#E8D030` | ⚡ |
| Wind | `#50C890` | 🌬 |
| Earth | `#B48C50` | 🪨 |
| Light | `#F0E8A0` | ✨ |
| Dark | `#8050C8` | 🌑 |
| Arcane | `#C060D0` | 🔮 |
| None | `#888888` | ○ |

---

## 06 · HUD System

![HUD Preview](file:///C:/Users/Gtvkun/.gemini/antigravity-ide/brain/ea4771a2-1985-427d-90bb-170f5a82c44a/hud_preview.png)

### Thông số
- Kích thước: **300 × 97 px** per unit HUD
- Frame: nâu tối + viền vàng JRPG (`#C8943C`)

### Layer order (z-index)
```
unit_hud_frame.png          ← nền + viền vàng + nhãn HP/MP/STA
├─ Portrait                 ← icon nhân vật (dynamic)
├─ PortraitFrame            ← viền portrait overlay
├─ NameLabel                ← Cinzel font, ZIndex=40
└─ BarsContainer
   ├─ HPBar                 ← đỏ gradient, texture fill
   ├─ MPBar                 ← xanh dương gradient
   └─ StaminaBar            ← xanh lá gradient
```

### Skill Cooldown Strip (cùng hàng tên)
- Badge **24×24px**, icon skill
- Overlay tối dâng từ dưới lên (còn nhiều = cooldown lâu)
- Viền vàng mỏng tại cạnh overlay
- Số hồi chiêu chỉ hiện khi đang cooldown

### Combat Status Strip (dưới HUD)
- Badge **28×28px**: Chill / Slow / Frozen
- Frozen override Chill + Slow về mặt visual
- Stack number nhỏ góc dưới phải (chỉ Chill)

---

## 07 · Thế giới & Environment

### Địa danh đã đặt tên
- **Đồng Cỏ Thì Thầm** — khu vực chính, field_01
- Bìa rừng phía tây · Khu rừng phía bắc · Bờ sông
- Ngôi đền băng (phía đông bắc) · Trại nghỉ

### Shader system — `assets/shaders/world/`
| Shader | Tác dụng |
|---|---|
| `atmosphere_sunbeam` | Tia nắng xuyên tán cây |
| `atmosphere_fog` | Sương mù |
| `atmosphere_rain` | Mưa |
| `world_cloud_shadow` | Bóng mây di chuyển |
| `pond_water` | Nước ao shimmer/ripple |
| `foliage_wind` | Cây lắc theo gió |
| `world_color_grade` | Tông màu toàn cảnh |
| `ground_lighting` | Ánh sáng mặt đất |

### Dynamic globals
```
env_time01        = 0.5   (chu kỳ ngày đêm)
env_wind          = 0.2   (gió nhẹ)
env_shadow_strength = 0.86
env_water_shimmer = 0.72
```

### Layer system Field_01 (456×474 px, scale 4x)
`00_ground_base` → `01_ground_variation` → `02_canopy_shadow` → `03_dirt_path` → `04_cliff_top/05_cliff_wall` → `07_ground_details` → `08_wet_ground` → Props

**Hoa trong thế giới:** 🟣 purple_flower · 🔴 red_flower · 🔵 blue_flower

---

## 08 · Quest System

### 3 loại quest

| Type | ID | Icon | Phần thưởng điển hình |
|---|---|---|---|
| Main Quest | 0 | 📖 | XP + Vàng |
| Side Quest | 1 | 🌸 | Item |
| Companion Quest | 2 | 💙 | Skill Point |

### Ví dụ thực tế
- **"Dấu vết trong gió"** (Main) — Ansel → điều tra dấu chân bí ẩn → 500 XP + 120 Vàng
- **"Hoa trên tàn tro"** (Side) — Mira → thu thập 6 hoa tím → 2 Thuốc hồi phục
- **"Lời hứa của Hyou"** (Companion) → 3 mảnh ký ức → 1 Skill Point

### Ngôn ngữ mô tả
Tiếng Việt thơ mộng, gợi hình. Địa danh có cá tính: *"Các gốc cây cháy ở phía tây Đồng Cỏ Thì Thầm"*, *"Chúng thường mọc cạnh gốc cây cháy và những phiến đá giữ được hơi ấm sau hoàng hôn."*

---

## 09 · Visual Motifs & Gradients

| Motif | Gradient | Dùng cho |
|---|---|---|
| Ash World | `#2C1F10 → #1A110A → #0D0A07` | Nền chính toàn game |
| UI Gold Frame | `#3D2800 → #C8943C → #E8C060` | Khung HUD, panel |
| Hyou Ice | `#0A1C30 → #3ABCF5 → #A8DCFF` | Skill băng, aura Hyou |
| Dusk Sky | `#0A0D18 → #8B3A20 → #C87030` | Menu chính, cinematic |
| Dawn Sky | `#0D0A07 → #C89040 → #E8D090` | Quest reward, save point |
| Fire/Danger | `#5C1A00 → #F08020` | Boss, critical HP |

### Micro-animations
- **Pulse** — buff đang hiệu lực (scale 1→1.15)
- **Float** — floating text, ambient motes, collectibles
- **Shimmer** — gold accent hover (glow 8→32px)
- **Glow Ice** — Hyou skill charged / Frozen active

---

## 10 · Nguyên tắc thiết kế

1. **Màu = Nhận diện nhân vật** — ThemeColor xuyên suốt UI, người chơi nhận ra ngay không cần đọc tên
2. **World colors không nổi bật** — Earth tones mờ nhạt. Tương phản chỉ đến từ ánh sáng và hoa (biểu tượng hy vọng)
3. **Pixel-perfect, nearest-filter** — Scale 4x, không anti-alias sprite, nearest texture filter
4. **Tiếng Việt là ngôn ngữ chính** — Mọi text thơ mộng, địa danh có cá tính riêng
5. **HUD không che gameplay** — 300×97px góc trái, skill badge nhỏ cùng hàng tên
6. **Thế giới sống — Dynamic lighting** — Shader real-time cho thời tiết, ánh sáng, gió, nước
7. **Data tách khỏi State** — SkillData dùng chung, PlayerSkillState chứa progress riêng per character
8. **Narrative qua Visual** — Hoa tím = hy vọng · Đền băng = ký ức bị giam · Purple mist = memory Hyou

---

> **File HTML tương tác** (click swatch để copy hex, animation, dark mode):
> [visual_language.html](file:///C:/Users/Gtvkun/.gemini/antigravity-ide/brain/ea4771a2-1985-427d-90bb-170f5a82c44a/visual_language.html)
> 
> Hoặc mở qua HTTP server: `http://localhost:7890/visual_language.html`
