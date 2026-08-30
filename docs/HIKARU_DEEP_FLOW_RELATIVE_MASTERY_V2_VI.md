# Hikaru Deep Flow — Relative Mastery V2

## Mục tiêu

Không còn "20% né cố định".

`20%` chỉ là baseline khi Hikaru và attack đang ở kèo tương đối ngang nhau.
Runtime tính lại chance cho từng hit hợp lệ dựa trên:

- chênh lệch level;
- DEX hiện tại của Hikaru (bao gồm buff Focus) so với DEX attacker;
- INT của Hikaru so với INT attacker;
- tốc độ combat movement;
- attack speed của attacker;
- startup/telegraph của action;
- lunge speed hoặc projectile speed.

## Overmatch

Nếu Hikaru hơn ít nhất 25 level VÀ mastery score đủ cao hơn threat score (`>= 1.65x`),
Deep Flow coi attack đó là hoàn toàn đọc được:

`evade chance = 100%`

Vì vậy Lv99 Focus vs Slime Lv1 sẽ né sạch các melee/projectile hợp lệ.
Rule cần cả level gap + mastery ratio nên một boss cố tình để level thấp nhưng stat/tempo rất cao
không bị 100% dodge chỉ vì metadata level.

## Clamp bình thường

Nếu chưa Overmatch:
- min 2%
- max 95%
- ngang trình xoay quanh baseline 20%, sau đó tăng/giảm theo matchup.

## Không đổi các luật an toàn V1

- True Damage không auto-evade.
- Frozen/block/perfect-parry không bị Flow giành quyền.
- Chỉ melee hitbox / projectile.
- Multi-hit vẫn có cửa sổ invulnerability ngắn sau proc.
- Manual movement luôn override auto re-engage.
- Eye/VFX vẫn để phase sau.
