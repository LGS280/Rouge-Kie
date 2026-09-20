# HƯỚNG DẪN CẤU HÌNH VÀ TÍCH HỢP BOSS BRAEAD (UNITY)

Tài liệu chi tiết về kiến trúc mã nguồn, cơ chế AI, hành vi vũ khí và các bước thiết lập trong Unity Editor cho Boss **Braead**.

---

## 1. Tổng quan kiến trúc & File mã nguồn

| File / Thành phần | Vị trí thư mục | Vai trò chính |
| :--- | :--- | :--- |
| **`BraeadBossAI.cs`** | `Assets/Script/Enemy/Braead/BraeadBossAI.cs` | Quản lý vòng lặp AI, cự ly chiến đấu (Kiting), di chuyển về tâm xả chiêu nộ, kích hoạt Phase 2 (Hóa nộ), tích hợp thanh máu `BossHealthBarUI`. |
| **`BraeadWeaponAim.cs`** | `Assets/Script/Enemy/Braead/BraeadWeaponAim.cs` | Điều khiển vũ khí xoay theo mục tiêu, bắn chùm 10 viên (Cone Spread ở Phase 1 / Tỏa tròn 360 ở Phase 2), và vận hành tia quét Laser 360 độ. |
| **`Braead_Weapon.prefab`** | `Assets/Prefab/Mobs_Weapons/Braead_Weapon.prefab` | Prefab vũ khí cầm tay của Braead (chứa Animator 4 frames, FirePoint). |
| **`Braead_Bullet.prefab`** | `Assets/Prefab/Mobs_Weapons/Bullets/Braead_Bullet.prefab` | Prefab đạn thường 10 viên (Bullet ID: `24`). |
| **`Braead_Laser.prefab`** | `Assets/Prefab/Mobs_Weapons/Bullets/Braead_Laser.prefab` | Prefab tia Laser LineRenderer (Bullet ID: `25`). |
| **`Braead_Laser_Beam.mat`**| `Assets/Weapons/Materials/Braead_Laser_Beam.mat` | Material hiển thị tia sáng năng lượng của Laser. |

---

## 2. Các cơ chế chiến đấu chi tiết

### 2.1. Trí tuệ nhân tạo (AI) & Di chuyển Kiting
- **Không đi tuần (No Wander)**: Khi sinh ra hoặc khi người chơi bước vào phòng, Boss lập tức khóa mục tiêu và quay mặt về phía người chơi.
- **Giữ cự ly chiến đấu (Combat Kiting)**:
  - Cự ly lý tưởng: `4.5m - 5.5m`.
  - Nếu Player tiến sát lại gần ($< 4.0m$): Boss bay lùi lại đồng thời lượn nhẹ quanh người chơi.
  - Nếu Player ở quá xa ($> 6.2m$): Boss bay áp sát lại.
  - Khi trong tầm tối ưu: Boss bay lượn vòng cung (Strafe/Orbit) đảo chiều ngẫu nhiên mỗi `2.5s` và có cơ chế Raycast né tường / vật cản.
- **Hướng nhìn (Facing)**: Cả thân Boss và vũ khí đều liên tục xoay nhắm vào Player (kể cả khi đứng xa hay gần).

### 2.2. Dạng tấn công 1: Bắn chùm 10 viên (Bullet ID 24)
- **Phase 1 (Máu > 50%)**:
  - Bắn 10 viên đạn cùng lúc theo hình nón (`Cone Spread`) góc `50°` hướng thẳng về phía Player.
  - Tốc độ bắn lấy theo cấu hình database của vũ khí ID 22 (`attackCooldown` mặc định `~3.0s`).
- **Phase 2 (Máu <= 50% sau khi bắn Laser)**:
  - Chuyển sang dạng bắn **Tỏa tròn 360 độ** (10 viên chia đều mỗi góc `36°`).
  - Tốc độ bắn và di chuyển tăng thêm **+10%** (Thời gian hồi chiêu giảm 10%).

### 2.3. Dạng tấn công 2: Chiêu nộ Laser 360 độ (Bullet ID 25)
- **Điều kiện kích hoạt**: Tự động kích hoạt duy nhất **1 lần** khi Boss tụt xuống $\le 50\%$ lượng máu tối đa.
- **Trình tự thực hiện (Ultimate Routine)**:
  1. **Tập trung về tâm phòng**: Boss kích hoạt tốc độ cao (`ultimateMoveSpeed = 4.0f`) lướt về tâm phòng boss (`roomCenter` / `RoomController`).
  2. **Báo hiệu (Telegraph / Wind-up)**: Đứng yên sạc năng lượng trong `1.2 giây` (tạo cơ hội để người chơi nhận biết và tìm chỗ nấp).
  3. **Quét Laser 360 độ**: 
     - Vũ khí quét tròn đều 360 độ trong vòng `3.5 giây`.
     - **Vật cản che chắn**: Dùng 2D Raycast với `obstacleLayerMask` (tường, cột, cửa chắn). Nếu người chơi đứng nấp sau vật thể sẽ không phải chịu sát thương.
     - **Sát thương**: Gây mất **50% tổng máu và giáp tối đa** của người chơi (`(maxHealth + maxArmor) * 0.5f`).
     - **Hit-once Tracking**: Sử dụng `HashSet<int>` để đảm bảo mỗi người chơi chỉ bị trừ máu **tối đa 1 lần duy nhất** trong suốt 1 vòng quét 360 độ.
  4. **Kích hoạt Hóa Nộ (Phase 2)**: Sau khi tia laser tắt, Boss lập tức bắn bồi 1 đợt 10 viên tỏa tròn 360 độ và chuyển vĩnh viễn sang trạng thái Enraged.

---

## 3. Hướng dẫn thiết lập trong Unity Editor

### Bước 1: Gán Component trên Prefab Boss Braead
1. Mở Prefab Boss **Braead** trong Unity Editor.
2. Thêm component `BraeadBossAI` vào GameObject gốc của Boss:
   - **Preferred Distance**: `5.0`
   - **Move Speed**: `2.2`
   - **Attack Cooldown**: `3.0`
   - **Ultimate Move Speed**: `4.0`
   - **Laser Sweep Duration**: `3.5`
3. Đảm bảo các component nền tảng đã có: `Rigidbody2D` (Dynamic, Gravity = 0, Freeze Rotation Z = Checked), `Collider2D`, `MobHealth`, `MobFlash`, `Animator`.

### Bước 2: Gán Component trên Prefab Braead_Weapon
1. Mở Prefab vũ khí `Assets/Prefab/Mobs_Weapons/Braead_Weapon.prefab`.
2. Thêm component `BraeadWeaponAim`:
   - **Fire Point**: Kéo Transform con `FirePoint` (nếu để trống script sẽ tự động tìm `FirePoint`).
   - **Bullet Prefab**: Kéo Prefab `Assets/Prefab/Mobs_Weapons/Bullets/Braead_Bullet.prefab`.
   - **Laser Prefab**: Kéo Prefab `Assets/Prefab/Mobs_Weapons/Bullets/Braead_Laser.prefab`.
   - **Laser Material**: Kéo Material `Assets/Weapons/Materials/Braead_Laser_Beam.mat`.
   - **Laser Max Distance**: `30`
   - **Laser Width**: `0.85`
   - **Obstacle Layer Mask**: Chọn các layer `Obstacle`, `Door`, `Wall`, `Default`.
   - **Player Layer Mask**: Chọn layer `Player`.

### Bước 3: Kiểm tra Animation vũ khí
- Đảm bảo `Braead_Weapon.controller` đã gán clip `Braead_Weapon.anim` (Loop Time = 1).
- Animation vũ khí sẽ tự động chạy liên tục trong suốt trận đấu mà không cần thêm trigger hay parameter phức tạp nào.

---

## 4. Cấu hình Cơ sở dữ liệu (Database)

Hệ thống sử dụng **1 dòng duy nhất** trong bảng Weapons để quản lý cả 2 dạng đạn:

```sql
-- Dòng cấu hình vũ khí Braead trong bảng Weapons:
-- ID: 22
-- WeaponName: 'Braead Weapon'
-- BulletId: 24 (Đạn thường 10 viên)
-- SecondBulletId: 25 (Tia Laser 360 độ)
-- BulletsPerShot: 10
-- FireRate: 3.0
```

---

## 5. Danh sách kiểm tra nghiệm thu (Checklist)

- [x] Code đã biên dịch hoàn tất (0 Error, `dotnet build` Pass).
- [x] Boss không đi tuần, vừa vào là lock thẳng mục tiêu người chơi.
- [x] Boss giữ cự ly 4.5 - 5.5m (Kiting), lượn tròn né chướng ngại vật, không bu sát người chơi.
- [x] Khi máu $> 50\%$: Bắn chùm 10 viên hình nón về phía người chơi.
- [x] Khi máu $\le 50\%$: Bay về tâm phòng, dừng 1.2s rồi quét laser 360 độ trong 3.5s.
- [x] Tia laser bị tường cản, trúng người chơi gây mất đúng 50% tổng máu và giáp, mỗi người chỉ trúng 1 lần.
- [x] Sau khi kết thúc laser: Bước vào Phase 2, tăng 10% tốc độ di chuyển và tốc độ bắn, đạn thường bắn tỏa tròn 360 độ.
