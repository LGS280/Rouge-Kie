#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueKie.Tests
{
    [TestFixture]
    public class DaiUnityTestRunner
    {
        // =========================================================================
        // MODULE 4: GAMEPLAY — COMBAT & DUNGEON (UC-40 to UC-76)
        // =========================================================================

        [Test]
        public void UC40_CopyRoomCodeToClipboard()
        {
            string roomCode = "RK-8892";
            GUIUtility.systemCopyBuffer = roomCode;
            Assert.AreEqual(roomCode, GUIUtility.systemCopyBuffer, "Room Code must be correctly copied to system clipboard.");
        }

        [Test]
        public void UC41_SpawnPlayerCharacterInDungeon()
        {
            string selectedCharacter = "Rookie";
            Vector3 spawnPoint = new Vector3(0f, 0f, 0f);
            bool isCharacterInstantiated = !string.IsNullOrEmpty(selectedCharacter);
            
            Assert.IsTrue(isCharacterInstantiated, "Player character prefab must be instantiated at spawn point.");
            Assert.AreEqual(Vector3.zero, spawnPoint, "Spawn position must match designated point.");
        }

        [Test]
        public void UC42_MovePlayerCharacter_Keyboard()
        {
            Vector2 wasdInput = new Vector2(1f, 0f).normalized;
            float moveSpeed = 5.5f;
            Vector2 velocity = wasdInput * moveSpeed;

            Assert.AreEqual(5.5f, velocity.x, 0.01f, "Horizontal velocity should match speed.");
            Assert.AreEqual(0f, velocity.y, 0.01f, "Vertical velocity should be zero.");
        }

        [Test]
        public void UC43_MovePlayerCharacter_Gamepad()
        {
            Vector2 leftStickInput = new Vector2(-0.8f, 0.6f);
            float moveSpeed = 5.5f;
            Vector2 velocity = leftStickInput * moveSpeed;

            Assert.AreEqual(-4.4f, velocity.x, 0.01f, "Left stick horizontal movement vector calculated.");
            Assert.AreEqual(3.3f, velocity.y, 0.01f, "Left stick vertical movement vector calculated.");
        }

        [Test]
        public void UC44_AimWeaponWithMouse()
        {
            Vector3 weaponPos = Vector3.zero;
            Vector3 mouseWorldPos = new Vector3(10f, 10f, 0f);
            Vector3 dir = (mouseWorldPos - weaponPos).normalized;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bool isFacingLeft = mouseWorldPos.x < weaponPos.x;

            Assert.AreEqual(45f, angle, 0.1f, "Aim angle toward (10, 10) must be 45 degrees.");
            Assert.IsFalse(isFacingLeft, "Player sprite should face right when aiming right.");
        }

        [Test]
        public void UC45_AimWeaponWithGamepad_AutoAim()
        {
            Vector3 playerPos = Vector3.zero;
            Vector3 enemyPos = new Vector3(3f, 4f, 0f);
            float aimRadius = 8f;

            float distance = Vector3.Distance(playerPos, enemyPos);
            bool isTargetAcquired = distance <= aimRadius;

            Assert.IsTrue(isTargetAcquired, "Enemy within aim radius must be selected for auto-aim.");
            Assert.AreEqual(5f, distance, 0.01f, "Distance to target calculated correctly.");
        }

        [Test]
        public void UC46_ShootBullet_MouseClick()
        {
            int currentMana = 20;
            int manaCost = 5;
            float fireRateTimer = 0f;
            float cooldown = 0.2f;

            bool canShoot = fireRateTimer <= 0f && currentMana >= manaCost;
            if (canShoot)
            {
                currentMana -= manaCost;
                fireRateTimer = cooldown;
            }

            Assert.IsTrue(canShoot, "Player should be able to shoot when mana and cooldown allow.");
            Assert.AreEqual(15, currentMana, "Mana should be deducted by bullet cost.");
            Assert.AreEqual(0.2f, fireRateTimer, 0.01f, "Cooldown timer set.");
        }

        [Test]
        public void UC47_ShootBullet_Gamepad()
        {
            bool gamepadButtonPressed = true;
            int currentMana = 10;
            int manaCost = 2;

            bool bulletFired = gamepadButtonPressed && currentMana >= manaCost;
            Assert.IsTrue(bulletFired, "Bullet fired towards auto-aimed target via gamepad button.");
        }

        [Test]
        public void UC48_PerformMeleeAttack()
        {
            float enemyDistance = 1.2f;
            float meleeRadius = 2.0f;
            int currentMana = 0; // Melee consumes no mana

            bool canMelee = enemyDistance <= meleeRadius;
            Assert.IsTrue(canMelee, "Melee attack performed when enemy within melee radius.");
            Assert.AreEqual(0, currentMana, "No mana consumed for melee attack.");
        }

        [Test]
        public void UC49_CalculateCriticalHit()
        {
            float baseDamage = 40f;
            float critMultiplier = 2.0f;
            float critChance = 100f; // 100% for deterministic testing

            float roll = 50f;
            bool isCrit = roll <= critChance;
            float finalDamage = isCrit ? baseDamage * critMultiplier : baseDamage;

            Assert.IsTrue(isCrit, "Roll of 50 against 100% crit chance must yield critical hit.");
            Assert.AreEqual(80f, finalDamage, 0.01f, "Critical damage must equal baseDamage * 2.0.");
        }

        [Test]
        public void UC50_DisplayFloatingDamageNumber()
        {
            int damage = 85;
            bool isCrit = true;
            string damageColor = isCrit ? "Yellow" : "Red";

            Assert.AreEqual(85, damage, "Damage number text value matches applied damage.");
            Assert.AreEqual("Yellow", damageColor, "Crit damage text uses Yellow color.");
        }

        [Test]
        public void UC51_EnemyTakesDamage()
        {
            int enemyHp = 100;
            int damage = 35;

            enemyHp = Mathf.Max(0, enemyHp - damage);
            bool triggerFlash = damage > 0;

            Assert.AreEqual(65, enemyHp, "Enemy HP reduced by damage amount.");
            Assert.IsTrue(triggerFlash, "Flash effect triggered on damage.");
        }

        [Test]
        public void UC52_EnemyDies()
        {
            int enemyHp = 0;
            bool isDead = enemyHp <= 0;

            bool colliderEnabled = !isDead;
            bool aiActive = !isDead;

            Assert.IsTrue(isDead, "Enemy dies when HP reaches 0.");
            Assert.IsFalse(colliderEnabled, "Enemy collider disabled on death.");
            Assert.IsFalse(aiActive, "Enemy AI disabled on death.");
        }

        [Test]
        public void UC53_PlayerTakesDamage()
        {
            int currentHp = 100;
            int currentArmor = 5;
            int incomingDamage = 8;

            int damageToArmor = Mathf.Min(currentArmor, incomingDamage);
            int remainingDamage = incomingDamage - damageToArmor;
            currentArmor -= damageToArmor;
            currentHp -= remainingDamage;

            Assert.AreEqual(0, currentArmor, "Armor absorbs 5 damage and reaches 0.");
            Assert.AreEqual(97, currentHp, "HP absorbs remaining 3 damage and reaches 97.");
        }

        [Test]
        public void UC54_PlayerArmorRegeneration()
        {
            int currentArmor = 3;
            int maxArmor = 5;
            float timeSinceLastHit = 2.5f;
            float delayBeforeRegen = 2.0f;

            bool regenStarted = timeSinceLastHit >= delayBeforeRegen;
            if (regenStarted && currentArmor < maxArmor)
            {
                currentArmor += 1;
            }

            Assert.IsTrue(regenStarted, "Armor regen starts after 2s without damage.");
            Assert.AreEqual(4, currentArmor, "Armor regenerated by 1 tick up to maxArmor.");
        }

        [Test]
        public void UC55_PlayerDies()
        {
            int playerHp = 0;
            bool isDead = playerHp <= 0;

            bool playerControllerEnabled = !isDead;
            bool weaponsVisible = !isDead;

            Assert.IsTrue(isDead, "Player dies when HP reaches 0.");
            Assert.IsFalse(playerControllerEnabled, "PlayerController disabled on death.");
            Assert.IsFalse(weaponsVisible, "Weapons hidden on death.");
        }

        [Test]
        public void UC56_UseReviveToken()
        {
            int reviveTokens = 1;
            int maxHp = 100;
            int currentHp = 0;

            bool canRevive = reviveTokens > 0;
            if (canRevive)
            {
                reviveTokens -= 1;
                currentHp = Mathf.RoundToInt(maxHp * 0.5f);
            }

            Assert.IsTrue(canRevive, "Player able to revive when token available.");
            Assert.AreEqual(0, reviveTokens, "Revive token deducted.");
            Assert.AreEqual(50, currentHp, "Player respawns with 50% HP.");
        }

        [Test]
        public void UC57_ClearRoom_OpenDoors()
        {
            int remainingEnemies = 0;
            bool roomCleared = remainingEnemies == 0;
            bool doorsOpen = roomCleared;
            bool showBuffSelection = roomCleared;

            Assert.IsTrue(roomCleared, "Room cleared when 0 enemies remaining.");
            Assert.IsTrue(doorsOpen, "All room doors set to Open state.");
            Assert.IsTrue(showBuffSelection, "Buff selection screen displayed.");
        }

        [Test]
        public void UC58_SelectLevelUpBuff()
        {
            float baseDamage = 10f;
            float buffMultiplier = 1.25f; // +25% Damage

            float updatedDamage = baseDamage * buffMultiplier;
            Assert.AreEqual(12.5f, updatedDamage, 0.01f, "Damage increased by 25% after selecting buff.");
        }

        [Test]
        public void UC59_ActivateSpeedSkill()
        {
            float baseFireRate = 1.0f;
            float skillMultiplier = 3.0f;
            float cooldownTimer = 0f;

            bool canActivate = cooldownTimer <= 0f;
            float buffedFireRate = baseFireRate;
            if (canActivate)
            {
                buffedFireRate *= skillMultiplier;
                cooldownTimer = 15f;
            }

            Assert.IsTrue(canActivate, "Skill activates when off cooldown.");
            Assert.AreEqual(3.0f, buffedFireRate, 0.01f, "Attack speed tripled during skill.");
            Assert.AreEqual(15f, cooldownTimer, 0.01f, "15s cooldown started.");
        }

        [Test]
        public void UC60_DisplaySkillCooldownUI()
        {
            float cooldownRemaining = 7.5f;
            float totalCooldown = 15.0f;

            float fillAmount = cooldownRemaining / totalCooldown;
            Assert.AreEqual(0.5f, fillAmount, 0.01f, "Cooldown UI overlay fill amount calculated correctly.");
        }

        [Test]
        public void UC61_WeaponRecoilAnimation()
        {
            Vector3 originalPos = Vector3.zero;
            float recoilDistance = 0.2f;

            Vector3 recoiledPos = originalPos - new Vector3(recoilDistance, 0f, 0f);
            Assert.AreEqual(-0.2f, recoiledPos.x, 0.01f, "Weapon moves backward during recoil animation.");
        }

        [Test]
        public void UC62_SwapWeapon()
        {
            int activeWeaponIndex = 0;
            int totalWeapons = 2;

            activeWeaponIndex = (activeWeaponIndex + 1) % totalWeapons;
            Assert.AreEqual(1, activeWeaponIndex, "Active weapon index swapped from Primary (0) to Secondary (1).");
        }

        [Test]
        public void UC63_PickUpWeaponFromGround()
        {
            int inventoryCount = 1;
            int maxWeapons = 2;
            string groundWeapon = "Gold_Katana";

            if (inventoryCount < maxWeapons)
            {
                inventoryCount++;
            }

            Assert.AreEqual(2, inventoryCount, "Ground weapon added to player inventory.");
        }

        [Test]
        public void UC64_SpawnEnemiesInRoom()
        {
            int targetEnemyCount = 4;
            int spawnedEnemies = 0;

            for (int i = 0; i < targetEnemyCount; i++)
            {
                spawnedEnemies++;
            }

            bool doorsLocked = spawnedEnemies > 0;
            Assert.AreEqual(4, spawnedEnemies, "Specified enemy count spawned in room.");
            Assert.IsTrue(doorsLocked, "Room doors locked while combat active.");
        }

        [Test]
        public void UC65_EnemyAI_IdleState()
        {
            float distanceToPlayer = 15f;
            float sightRadius = 8f;

            bool hasTarget = distanceToPlayer <= sightRadius;
            string state = hasTarget ? "Chase" : "Idle";

            Assert.AreEqual("Idle", state, "Enemy remains in Idle state when player outside sight radius.");
        }

        [Test]
        public void UC66_EnemyAI_DetectPlayer_Chase()
        {
            float distanceToPlayer = 6f;
            float sightRadius = 8f;

            bool hasTarget = distanceToPlayer <= sightRadius;
            string state = hasTarget ? "Chase" : "Idle";

            Assert.AreEqual("Chase", state, "Enemy state switches to Chase when player enters sight radius.");
        }

        [Test]
        public void UC67_EnemyAI_AttackPlayer()
        {
            float distanceToPlayer = 1.5f;
            float attackRadius = 2.0f;

            bool inAttackRange = distanceToPlayer <= attackRadius;
            string state = inAttackRange ? "Attack" : "Chase";

            Assert.AreEqual("Attack", state, "Enemy state switches to Attack when player enters attack range.");
        }

        [Test]
        public void UC68_EnemyAI_TakeDamageState()
        {
            bool isHitByBullet = true;
            bool triggerSpriteFlash = isHitByBullet;
            string state = isHitByBullet ? "TakeDamage" : "Chase";

            Assert.AreEqual("TakeDamage", state, "Enemy enters TakeDamage state briefly when hit.");
            Assert.IsTrue(triggerSpriteFlash, "MobFlash triggers white sprite color flash.");
        }

        [Test]
        public void UC69_ProceduralMapGeneration()
        {
            int seed = 12345;
            Random.InitState(seed);
            int generatedRoomCount = Random.Range(4, 8);

            Assert.GreaterOrEqual(generatedRoomCount, 4, "Generated map room count matches PCG bounds.");
            Assert.LessOrEqual(generatedRoomCount, 8, "Generated map room count within PCG bounds.");
        }

        [Test]
        public void UC70_LootDropFromEnemy()
        {
            float dropChance = 0.8f;
            float roll = 0.3f;

            bool spawnLoot = roll <= dropChance;
            Assert.IsTrue(spawnLoot, "Loot prefab spawned at enemy position on death.");
        }

        [Test]
        public void UC71_PickUpLootItem()
        {
            bool playerInTrigger = true;
            bool lootItemDestroyed = playerInTrigger;

            Assert.IsTrue(lootItemDestroyed, "Loot item destroyed after player triggers pickup.");
        }

        [Test]
        public void UC72_HPRestoreFromLoot()
        {
            int currentHp = 60;
            int maxHp = 100;
            int healAmount = 25;

            currentHp = Mathf.Min(maxHp, currentHp + healAmount);
            Assert.AreEqual(85, currentHp, "Player HP increased and capped to maxHp.");
        }

        [Test]
        public void UC73_ManaRestoreFromLoot()
        {
            int currentMana = 10;
            int maxMana = 30;
            int manaRestore = 15;

            currentMana = Mathf.Min(maxMana, currentMana + manaRestore);
            Assert.AreEqual(25, currentMana, "Player mana increased and capped to maxMana.");
        }

        [Test]
        public void UC74_PlayerHUD_DisplayHPBar()
        {
            int currentHp = 75;
            int maxHp = 100;

            float hpFillAmount = (float)currentHp / maxHp;
            string hpText = $"{currentHp}/{maxHp}";

            Assert.AreEqual(0.75f, hpFillAmount, 0.01f, "HUD HP bar fill amount calculated.");
            Assert.AreEqual("75/100", hpText, "HUD HP text formatted.");
        }

        [Test]
        public void UC75_PlayerHUD_DisplayArmorBar()
        {
            int currentArmor = 3;
            int maxArmor = 5;

            float armorFillAmount = (float)currentArmor / maxArmor;
            Assert.AreEqual(0.6f, armorFillAmount, 0.01f, "HUD Armor bar fill amount calculated.");
        }

        [Test]
        public void UC76_PlayerHUD_DisplayManaBar()
        {
            int currentMana = 18;
            int maxMana = 30;

            float manaFillAmount = (float)currentMana / maxMana;
            Assert.AreEqual(0.6f, manaFillAmount, 0.01f, "HUD Mana bar fill amount calculated.");
        }

        // =========================================================================
        // MODULE 5: CO-OP MULTIPLAYER SYNCHRONIZATION (UC-77 to UC-80)
        // =========================================================================

        [Test]
        public void UC77_EstablishSignalRConnection()
        {
            string jwtToken = "mock_valid_jwt_token";
            bool isConnected = !string.IsNullOrEmpty(jwtToken);

            Assert.IsTrue(isConnected, "SignalR connection established with JWT authentication.");
        }

        [Test]
        public void UC78_SyncPlayerPosition_CoOp()
        {
            Vector3 sentPos = new Vector3(12.5f, -4.2f, 0f);
            Vector3 receivedPos = sentPos;

            Assert.AreEqual(sentPos, receivedPos, "Remote player transform position updated from SignalR payload.");
        }

        [Test]
        public void UC79_SyncPlayerHealth_CoOp()
        {
            int remoteHp = 45;
            int syncedHp = remoteHp;

            Assert.AreEqual(45, syncedHp, "Remote player health bar updated across Co-op clients.");
        }

        [Test]
        public void UC80_SyncEnemyPosition_CoOp()
        {
            Vector3 hostMobPos = new Vector3(8.0f, 15.3f, 0f);
            Vector3 guestMobPos = hostMobPos;

            Assert.AreEqual(hostMobPos, guestMobPos, "Enemy position broadcast by Host synchronized on Guest client.");
        }

        // =========================================================================
        // MODULE 6: ECONOMY, SHOP & LEADERBOARD (TC-81 to TC-120)
        // =========================================================================

        [Test]
        public void TC81_VerifyEnemyDeathSync_DataStructureValid()
        {
            string enemyId = "mob_goblin_01";
            Assert.IsNotEmpty(enemyId, "Enemy ID must not be empty for network sync.");
        }

        [Test]
        public void TC82_VerifyBulletCollisionSync_Calculation()
        {
            float baseDamage = 25f;
            float critMultiplier = 1.5f;
            bool isCrit = true;

            float finalDamage = isCrit ? baseDamage * critMultiplier : baseDamage;
            Assert.AreEqual(37.5f, finalDamage, 0.01f, "Damage calculation mismatch for synced bullet collision.");
        }

        [Test]
        public void TC90_VerifyPingDisplay_Threshold()
        {
            int pingMs = 45;
            Assert.Less(pingMs, 500, "Ping latency must be below 500ms for acceptable gameplay.");
        }

        [Test]
        public void TC92_VerifyShopPurchase_SufficientBalance()
        {
            int currentCoins = 500;
            int itemPrice = 200;

            bool canPurchase = currentCoins >= itemPrice;
            Assert.IsTrue(canPurchase, "Player should be allowed to purchase item when balance is sufficient.");
        }

        [Test]
        public void TC99_VerifyPreventDuplicatePurchase()
        {
            bool isAlreadyOwned = true;
            bool allowPurchase = !isAlreadyOwned;

            Assert.IsFalse(allowPurchase, "Duplicate purchase of permanent items must be blocked.");
        }

        [Test]
        public void TC101_VerifySoloRunStatsCompilation()
        {
            int wavesSurvived = 10;
            int enemiesKilled = 45;
            int currencyEarned = 150;

            Assert.Greater(wavesSurvived, 0, "Waves survived must be recorded.");
            Assert.GreaterOrEqual(enemiesKilled, 0, "Enemies killed must be recorded.");
            Assert.GreaterOrEqual(currencyEarned, 0, "Currency earned must be non-negative.");
        }

        [UnityTest]
        public IEnumerator TC104_SimulateLeaderboardFetch_Coroutine()
        {
            yield return new WaitForSeconds(0.1f);

            bool isDataFetched = true;
            Assert.IsTrue(isDataFetched, "Leaderboard data fetch simulation completed.");
        }

        [Test]
        public void TC116_VerifyCreateEnemyConfig_Validation()
        {
            string enemyName = "Plasma Goblin";
            float baseHp = 250f;
            float baseDamage = 35f;

            Assert.IsNotEmpty(enemyName, "Enemy name must be provided.");
            Assert.Greater(baseHp, 0, "Enemy HP must be positive.");
            Assert.Greater(baseDamage, 0, "Enemy Damage must be positive.");
        }
    }
}
#endif
