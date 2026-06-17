using System;
using System.IO;
using AdaptiveCompanionAI.Common.Configs;
using AdaptiveCompanionAI.Common.Data;
using AdaptiveCompanionAI.Common.Players;
using AdaptiveCompanionAI.Common.Systems;
using AdaptiveCompanionAI.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AdaptiveCompanionAI.Content.NPCs
{
    public class AdaptiveCompanionNPC : ModNPC
    {
        private const int DuelCountdownDuration = 120;
        private const int DuelResultDisplayTicks = 600;
        private const float CompanionOpenDistance = 170f;

        private enum CompanionAttackMode
        {
            Adaptive = 0,
            Melee = 1,
            Ranged = 2,
            Magic = 3,
            Summon = 4,
        }

        private struct CompanionAttackPlan
        {
            public string Name;
            public Item Weapon;
            public int WeaponSlotIndex;
            public Item Ammo;
            public int AmmoSlotIndex;
            public int AmmoItemId;
            public bool ConsumesAmmo;
            public int Damage;
            public int UseTime;
            public float Range;
            public float PreferredDistance;
            public float ProjectileSpeed;
            public float KnockBack;
            public int ProjectileType;
            public DamageClass WeaponDamageClass;
            public CompanionAttackMode Mode;

            public bool HasProjectile => ProjectileType > ProjectileID.None;
            public bool HasWeapon => Weapon != null && !Weapon.IsAir;
        }

        private int _attackCooldown;
        private int _currentTarget = -1;
        private bool _duelActive;
        private int _duelTicks;
        private int _duelCountdownTicks;
        private int _duelResultTicks;
        private int _duelDamageDealtToPlayer;
        private int _duelDamageTaken;
        private int _lastDuelOwnerLife;
        private bool _interfaceLeashActive;
        private Item _visualHeldItem;
        private int _visualItemAnimation;
        private int _visualItemAnimationMax;
        private double _drawBodyFrameCounter;
        private double _drawLegFrameCounter;
        private int _drawBodyFrameY = -1;
        private int _drawLegFrameY = -1;
        private string _duelStatusText = "Готов к дуэли.";

        public override string Texture => $"Terraria/Images/NPC_{NPCID.Guide}";
        public int OwnerPlayerIndex => (int)NPC.ai[0];
        public bool DuelActive => _duelActive;
        public string DuelStatusText => _duelStatusText;
        public float DuelElapsedSeconds => _duelTicks / 60f;
        public int DuelDamageDealtToPlayer => _duelDamageDealtToPlayer;
        public int DuelDamageTaken => _duelDamageTaken;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Guide];
            NPCID.Sets.ActsLikeTownNPC[Type] = false;
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 20;
            NPC.height = 42;
            NPC.damage = 20;
            NPC.defense = 10;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
            NPC.friendly = true;
            NPC.townNPC = false;
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            AnimationType = NPCID.Guide;
        }

        public override bool CheckActive()
        {
            return false;
        }

        public override bool NeedSaving()
        {
            return true;
        }

        public override bool CanChat()
        {
            return false;
        }

        public override bool? CanBeHitByItem(Player player, Item item)
        {
            if (!_duelActive || _duelCountdownTicks > 0 || player == null || player.whoAmI != OwnerPlayerIndex || item == null || item.IsAir || item.noMelee)
            {
                return false;
            }

            // Do not force a hit from CanBeHitByItem. Terraria will ask CanCollideWithPlayerMeleeAttack
            // for the actual swing hitbox, which prevents distant pickaxe/sword hits during duels.
            return null;
        }

        public override bool? CanCollideWithPlayerMeleeAttack(Player player, Item item, Rectangle meleeAttackHitbox)
        {
            return IsValidDuelMeleeAttempt(player, item, meleeAttackHitbox);
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (projectile == null)
            {
                return false;
            }

            if (projectile.hostile)
            {
                return _duelActive ? false : null;
            }

            return _duelActive && _duelCountdownTicks <= 0 && projectile.friendly && projectile.owner == OwnerPlayerIndex && !CompanionProjectileGlobal.IsFromCompanion(projectile);
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            // Duel damage is applied manually so the UI progress, balancing and result tracking stay deterministic.
            return false;
        }

        public override bool CheckDead()
        {
            if (_duelActive)
            {
                FinishDuel(companionWon: false, companionWasDefeated: true);
                return false;
            }

            return true;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!TryGetOwnerData(out Player owner, out AdaptiveDifficultyPlayer ownerData))
            {
                return true;
            }

            Player drawPlayer = BuildDrawPlayer(owner, ownerData);
            if (drawPlayer == null)
            {
                return true;
            }

            Main.PlayerRenderer.DrawPlayer(Main.Camera, drawPlayer, drawPlayer.position, 0f, Vector2.Zero, 0f, 1f);
            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (_duelActive || _duelResultTicks > 0)
            {
                DrawDuelHealthBar(spriteBatch, screenPos);
            }
        }

        public override void AI()
        {
            if (!TryGetOwnerData(out Player owner, out AdaptiveDifficultyPlayer ownerData))
            {
                NPC.active = false;
                return;
            }

            NPC.timeLeft = 10;
            TryOpenInterfaceByRightClick(owner);
            ApplyDuelInteractionFlags();
            ApplyScaledStats(owner, ownerData);

            if (_attackCooldown > 0)
            {
                _attackCooldown--;
            }

            if (_visualItemAnimation > 0)
            {
                _visualItemAnimation--;
                if (_visualItemAnimation <= 0)
                {
                    _visualHeldItem = null;
                    _visualItemAnimationMax = 0;
                }
            }

            NPC target = FindTarget(owner);
            if (_duelActive)
            {
                _interfaceLeashActive = false;
                HandleDuel(owner, ownerData);
            }
            else
            {
                UpdateDuelResultTimer();
                bool interfaceLeashed = UpdateInterfaceLeashState(owner, ownerData);
                if (interfaceLeashed)
                {
                    target = FindTarget(owner, interfaceLeashed: true);
                    UpdateInterfaceLeashedMovement(owner, ownerData, target);
                }
                else
                {
                    UpdateAdaptiveMovement(owner, ownerData, target);
                }

                UpdateNormalCombat(target, owner, ownerData);
            }

            float distanceToOwner = Vector2.Distance(NPC.Center, owner.Center);
            ownerData.UpdateCompanionRuntimeMetrics(NPC, _duelActive, distanceToOwner);
            ownerData.CompanionMetrics.Set("interface_leash_active", _interfaceLeashActive ? 1d : 0d);
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            RegisterDamageTaken(damageDone);
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            RegisterDamageTaken(damageDone);
        }

        public override void SaveData(TagCompound tag)
        {
            tag.Add("owner", OwnerPlayerIndex);
        }

        public override void LoadData(TagCompound tag)
        {
            BindToOwner(tag.GetInt("owner"));
            ResetDuelRuntime("Готов к дуэли.");
            ApplyDuelInteractionFlags();
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(_duelActive);
            writer.Write(_duelTicks);
            writer.Write(_duelCountdownTicks);
            writer.Write(_duelResultTicks);
            writer.Write(_duelDamageDealtToPlayer);
            writer.Write(_duelDamageTaken);
            writer.Write(_lastDuelOwnerLife);
            writer.Write(_duelStatusText ?? string.Empty);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            _duelActive = reader.ReadBoolean();
            _duelTicks = reader.ReadInt32();
            _duelCountdownTicks = reader.ReadInt32();
            _duelResultTicks = reader.ReadInt32();
            _duelDamageDealtToPlayer = reader.ReadInt32();
            _duelDamageTaken = reader.ReadInt32();
            _lastDuelOwnerLife = reader.ReadInt32();
            _duelStatusText = reader.ReadString();
            ApplyDuelInteractionFlags();
        }

        public void BindToOwner(int ownerPlayerIndex)
        {
            NPC.ai[0] = ownerPlayerIndex;
            NPC.netUpdate = true;
        }

        public void SetDuelState(bool enabled)
        {
            if (enabled)
            {
                StartDuel();
            }
            else
            {
                StopDuelManually();
            }
        }

        private void TryOpenInterfaceByRightClick(Player owner)
        {
            if (Main.dedServ || owner.whoAmI != Main.myPlayer || !Main.mouseRight || !Main.mouseRightRelease)
            {
                return;
            }

            if (Main.LocalPlayer.mouseInterface)
            {
                return;
            }

            Rectangle hitbox = NPC.Hitbox;
            hitbox.Inflate(12, 12);
            if (!hitbox.Contains(Main.MouseWorld.ToPoint()) || Vector2.Distance(owner.Center, NPC.Center) > CompanionOpenDistance)
            {
                return;
            }

            Main.mouseRightRelease = false;
            Main.playerInventory = true;
            AdaptiveCompanionSystem.OpenInterface(NPC.whoAmI);
        }

        private void UpdateNormalCombat(NPC target, Player owner, AdaptiveDifficultyPlayer ownerData)
        {
            if (target != null)
            {
                if (_currentTarget != target.whoAmI)
                {
                    ownerData.CompanionSwitchedTarget();
                    _currentTarget = target.whoAmI;
                }

                TryAttackTarget(target, owner, ownerData);
            }
            else
            {
                _currentTarget = -1;
                ownerData.UpdateCompanionTacticalMetrics(0, 0, 0f, 0, 0f, 0, 0f, 0f);
            }
        }

        private void ApplyScaledStats(Player owner, AdaptiveDifficultyPlayer ownerData)
        {
            float effective = _duelActive ? ownerData.CurrentPowerSnapshot.DuelCoefficient : ownerData.CurrentPowerSnapshot.EffectiveCoefficient;
            effective = MathHelper.Clamp(effective, 0f, 10f);

            int equipmentDefense = GetCompanionEquipmentDefense(ownerData);
            int weaponDamage = GetBestStoredWeaponDamage(ownerData);
            float duelLifeMultiplier = _duelActive ? 0.78f : 1.12f;
            float duelDamageMultiplier = _duelActive ? 0.72f : 1f;

            int calculatedDamage = (int)((10f + Math.Max(ownerData.CurrentPowerSnapshot.Progress.CurrentWeaponDamage, weaponDamage) * 0.82f + owner.statDefense * 0.18f) * effective * duelDamageMultiplier);
            int calculatedDefense = (int)((4f + owner.statDefense * 0.72f) * effective) + equipmentDefense;
            int calculatedLifeMax = (int)(80f + owner.statLifeMax2 * duelLifeMultiplier * Math.Max(0.25f, effective) + equipmentDefense * 2f);

            float previousRatio = NPC.lifeMax > 0 ? NPC.life / (float)NPC.lifeMax : 1f;
            NPC.damage = Math.Max(1, calculatedDamage);
            NPC.defense = Math.Max(0, calculatedDefense);

            if (NPC.lifeMax != calculatedLifeMax)
            {
                NPC.lifeMax = Math.Max(40, calculatedLifeMax);
                NPC.life = (int)(NPC.lifeMax * MathHelper.Clamp(previousRatio, 0.1f, 1f));
                if (NPC.life <= 0)
                {
                    NPC.life = NPC.lifeMax;
                }
            }
        }

        private void ApplyDuelInteractionFlags()
        {
            NPC.friendly = !_duelActive;
            NPC.townNPC = false;
            NPC.dontTakeDamage = false;
            NPC.lifeRegen = 0;
        }

        private void StartDuel()
        {
            _duelActive = true;
            _duelTicks = 0;
            _duelCountdownTicks = DuelCountdownDuration;
            _duelResultTicks = 0;
            _duelDamageDealtToPlayer = 0;
            _duelDamageTaken = 0;
            _attackCooldown = 0;
            _currentTarget = -1;

            if (TryGetOwnerData(out Player owner, out AdaptiveDifficultyPlayer ownerData))
            {
                PositionDuelists(owner);
                ApplyScaledStats(owner, ownerData);
                owner.statLife = owner.statLifeMax2;
                owner.statMana = owner.statManaMax2;
                NPC.life = NPC.lifeMax;
                _lastDuelOwnerLife = owner.statLife;
            }
            else
            {
                _lastDuelOwnerLife = 0;
            }

            _duelStatusText = "Подготовка: 2 сек.";
            ApplyDuelInteractionFlags();
            NPC.netUpdate = true;
        }

        private void PositionDuelists(Player owner)
        {
            int side = owner.direction == 0 ? 1 : owner.direction;
            Vector2 center = owner.Center;
            Vector2 playerCenter = center + new Vector2(-side * 108f, 0f);
            Vector2 companionCenter = center + new Vector2(side * 108f, 0f);

            owner.Center = playerCenter;
            owner.velocity = Vector2.Zero;
            owner.fallStart = (int)(owner.position.Y / 16f);
            NPC.Center = companionCenter;
            NPC.velocity = Vector2.Zero;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, owner.whoAmI);
                NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, owner.whoAmI);
            }

            NPC.netUpdate = true;
        }

        private void StopDuelManually()
        {
            if (_duelActive)
            {
                _duelStatusText = "Дуэль остановлена.";
                _duelResultTicks = DuelResultDisplayTicks / 2;
            }
            else if (_duelResultTicks <= 0)
            {
                _duelStatusText = "Готов к дуэли.";
            }

            _duelActive = false;
            _duelCountdownTicks = 0;
            _attackCooldown = 0;
            ApplyDuelInteractionFlags();
            NPC.netUpdate = true;
        }

        private void FinishDuel(bool companionWon, bool companionWasDefeated)
        {
            if (!_duelActive)
            {
                return;
            }

            if (TryGetOwnerData(out _, out AdaptiveDifficultyPlayer ownerData))
            {
                if (companionWon)
                {
                    ownerData.CompanionWonDuel();
                }
                else
                {
                    ownerData.CompanionLostDuel();
                }

                int duelDurationSeconds = Math.Max(0, (_duelTicks - DuelCountdownDuration) / 60);
                ownerData.RecordDuelResult(duelDurationSeconds, _duelDamageTaken, _duelDamageDealtToPlayer, companionWon);
            }

            _duelActive = false;
            _duelCountdownTicks = 0;
            _duelResultTicks = DuelResultDisplayTicks;
            _attackCooldown = 30;
            _duelStatusText = companionWon ? "Победа компаньона." : "Победа игрока.";

            if (companionWasDefeated)
            {
                NPC.life = Math.Max(1, NPC.lifeMax / 2);
            }

            ApplyDuelInteractionFlags();
            NPC.netUpdate = true;
        }

        private void ResetDuelRuntime(string status)
        {
            _duelActive = false;
            _duelTicks = 0;
            _duelCountdownTicks = 0;
            _duelResultTicks = 0;
            _duelDamageDealtToPlayer = 0;
            _duelDamageTaken = 0;
            _lastDuelOwnerLife = 0;
            _duelStatusText = status;
            _interfaceLeashActive = false;
        }

        private void UpdateDuelResultTimer()
        {
            if (_duelResultTicks > 0)
            {
                _duelResultTicks--;
                if (_duelResultTicks == 0)
                {
                    _duelStatusText = "Готов к дуэли.";
                }
            }
        }

        private void HandleDuel(Player owner, AdaptiveDifficultyPlayer ownerData)
        {
            _duelTicks++;
            ApplyDuelInteractionFlags();
            UpdateDuelDamageCounter(owner);

            if (owner.dead || owner.statLife <= 0)
            {
                FinishDuel(companionWon: true, companionWasDefeated: false);
                return;
            }

            if (_duelCountdownTicks > 0)
            {
                _duelCountdownTicks--;
                int secondsLeft = Math.Max(1, (_duelCountdownTicks + 59) / 60);
                _duelStatusText = $"Подготовка: {secondsLeft} сек.";
                NPC.velocity *= 0.82f;
                return;
            }

            _duelStatusText = "Бой идет.";
            float distance = UpdateDuelMovement(owner, ownerData);
            CompanionAttackPlan plan = SelectBestAttackPlan(owner, ownerData, distance, owner.statLife, false, true);

            if (_attackCooldown > 0 || distance > plan.Range || Main.netMode == NetmodeID.MultiplayerClient || ownerData.CurrentPowerSnapshot.DuelCoefficient <= 0.01f || plan.Damage <= 0)
            {
                return;
            }

            TryDuelAttack(owner, ownerData, plan, distance);
        }

        private void TryDuelAttack(Player owner, AdaptiveDifficultyPlayer ownerData, CompanionAttackPlan plan, float distance)
        {
            bool meleeContact = plan.Mode == CompanionAttackMode.Melee && distance <= 96f;
            if (meleeContact && TryDuelMeleeStrike(owner, ownerData, plan))
            {
                return;
            }

            if (plan.HasProjectile && (plan.Mode != CompanionAttackMode.Melee || distance > 86f))
            {
                FireDuelProjectile(owner, ownerData, plan);
            }
        }

        private bool TryDuelMeleeStrike(Player owner, AdaptiveDifficultyPlayer ownerData, CompanionAttackPlan plan)
        {
            Rectangle strikeBox = NPC.Hitbox;
            strikeBox.Inflate(42, 18);
            if (!strikeBox.Intersects(owner.Hitbox))
            {
                return false;
            }

            int direction = owner.Center.X >= NPC.Center.X ? 1 : -1;
            float lowLifeGuard = owner.statLifeMax2 > 0 && owner.statLife / (float)owner.statLifeMax2 < 0.32f ? 0.72f : 1f;
            int damage = Math.Max(1, (int)((NPC.damage * 0.46f + plan.Damage * 0.08f) * lowLifeGuard));
            int lifeBeforeAttack = owner.statLife;
            owner.Hurt(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral($"{owner.name} пал в дуэли со своим компаньоном.")), damage, direction);
            _duelDamageDealtToPlayer += Math.Max(0, lifeBeforeAttack - owner.statLife);
            _lastDuelOwnerLife = owner.statLife;
            ownerData.CompanionDidMeleeAction();
            _attackCooldown = Math.Max(18, (int)(plan.UseTime * MathHelper.Lerp(1.24f, 0.84f, ownerData.CurrentPowerSnapshot.PlayerSkillScore)));
            return true;
        }

        private void FireDuelProjectile(Player owner, AdaptiveDifficultyPlayer ownerData, CompanionAttackPlan plan)
        {
            FireWeaponProjectile(owner, ownerData, null, plan, owner.Center, true);
            _attackCooldown = Math.Max(20, (int)(plan.UseTime * MathHelper.Lerp(1.35f, 0.82f, ownerData.CurrentPowerSnapshot.PlayerSkillScore)));
        }

        private void FireWeaponProjectile(Player owner, AdaptiveDifficultyPlayer ownerData, NPC target, CompanionAttackPlan plan, Vector2 targetPoint, bool duel)
        {
            if (!plan.HasWeapon || !plan.HasProjectile || plan.ProjectileType <= ProjectileID.None)
            {
                return;
            }

            Vector2 aim = targetPoint - NPC.Center;
            if (!NormalizeOrDefault(ref aim, new Vector2(NPC.direction == 0 ? 1f : NPC.direction, 0f)))
            {
                aim = Vector2.UnitX;
            }

            float coefficient = duel ? ownerData.CurrentPowerSnapshot.DuelCoefficient : ownerData.CurrentPowerSnapshot.EffectiveCoefficient;
            coefficient = MathHelper.Clamp(coefficient, 0.1f, 10f);
            float lowLifeGuard = duel && owner.statLifeMax2 > 0 && owner.statLife / (float)owner.statLifeMax2 < 0.32f ? 0.72f : 1f;
            float bossBonus = !duel && target != null && target.boss ? 1.08f : 1f;

            Vector2 position = NPC.Center + aim * Math.Max(18f, NPC.width * 0.75f);
            Vector2 velocity = aim * MathHelper.Clamp(plan.ProjectileSpeed, 4f, 30f);
            int type = plan.ProjectileType;
            int damage = Math.Max(1, (int)(plan.Damage * coefficient * lowLifeGuard * bossBonus));
            float knockBack = Math.Max(0f, plan.KnockBack);
            string context = $"AdaptiveCompanionAI|npc={NPC.whoAmI}|mode={(int)plan.Mode}|duel={(duel ? 1 : 0)}";
            EntitySource_ItemUse_WithAmmo source = new EntitySource_ItemUse_WithAmmo(owner, plan.Weapon, plan.AmmoItemId, context);
            bool spawnDefaultProjectile = true;

            RunWithOwnerItemProxy(owner, plan, () =>
            {
                ItemLoader.ModifyShootStats(plan.Weapon, owner, ref position, ref velocity, ref type, ref damage, ref knockBack);
                position = NPC.Center + aim * Math.Max(18f, NPC.width * 0.75f);
                if (velocity.LengthSquared() < 0.001f)
                {
                    velocity = aim * MathHelper.Clamp(plan.ProjectileSpeed, 4f, 30f);
                }

                spawnDefaultProjectile = ItemLoader.Shoot(plan.Weapon, owner, source, position, velocity, type, damage, knockBack, true);
            });

            if (spawnDefaultProjectile)
            {
                float targetIndex = duel ? owner.whoAmI : target != null ? target.whoAmI : -1f;
                int projectileIndex = Projectile.NewProjectile(source, position, velocity, type, damage, knockBack, owner.whoAmI, targetIndex, NPC.whoAmI, (float)plan.Mode);
                if (projectileIndex >= 0 && projectileIndex < Main.maxProjectiles)
                {
                    MarkCompanionProjectile(Main.projectile[projectileIndex], duel, plan);
                }
            }

            ConsumeAmmoIfNeeded(ownerData, owner, plan);
            ownerData.CompanionFiredProjectile();
            PlayWeaponUseFeedback(plan);
        }

        private void RunWithOwnerItemProxy(Player owner, CompanionAttackPlan plan, Action action)
        {
            if (owner == null || plan.Weapon == null || plan.Weapon.IsAir)
            {
                action();
                return;
            }

            int selectedItem = owner.selectedItem;
            if (selectedItem < 0 || selectedItem >= owner.inventory.Length)
            {
                selectedItem = 0;
            }

            Item savedSelectedItem = owner.inventory[selectedItem]?.Clone() ?? CreateAirItem();
            Vector2 savedPosition = owner.position;
            Vector2 savedVelocity = owner.velocity;
            int savedDirection = owner.direction;
            int savedItemAnimation = owner.itemAnimation;
            int savedItemAnimationMax = owner.itemAnimationMax;
            int savedItemTime = owner.itemTime;
            int savedItemTimeMax = owner.itemTimeMax;
            int savedSelectedItemIndex = owner.selectedItem;

            try
            {
                owner.inventory[selectedItem] = plan.Weapon.Clone();
                owner.selectedItem = selectedItem;
                owner.Center = NPC.Center;
                owner.velocity = NPC.velocity;
                owner.direction = NPC.direction == 0 ? 1 : NPC.direction;
                owner.itemAnimation = Math.Max(1, plan.UseTime);
                owner.itemAnimationMax = Math.Max(1, plan.UseTime);
                owner.itemTime = Math.Max(1, plan.UseTime);
                owner.itemTimeMax = Math.Max(1, plan.UseTime);
                action();
            }
            finally
            {
                owner.inventory[selectedItem] = savedSelectedItem;
                owner.selectedItem = savedSelectedItemIndex;
                owner.position = savedPosition;
                owner.velocity = savedVelocity;
                owner.direction = savedDirection;
                owner.itemAnimation = savedItemAnimation;
                owner.itemAnimationMax = savedItemAnimationMax;
                owner.itemTime = savedItemTime;
                owner.itemTimeMax = savedItemTimeMax;
            }
        }

        private void MarkCompanionProjectile(Projectile projectile, bool duel, CompanionAttackPlan plan)
        {
            if (projectile == null || !projectile.active)
            {
                return;
            }

            CompanionProjectileGlobal global = projectile.GetGlobalProjectile<CompanionProjectileGlobal>();
            global.FromCompanion = true;
            global.CompanionNpcIndex = NPC.whoAmI;
            global.AttackMode = (int)plan.Mode;
            global.DuelProjectile = duel;
            projectile.friendly = !duel;
            projectile.hostile = duel;
            projectile.DamageType = plan.WeaponDamageClass ?? DamageClass.Generic;
            projectile.netUpdate = true;
        }

        private void ConsumeAmmoIfNeeded(AdaptiveDifficultyPlayer ownerData, Player owner, CompanionAttackPlan plan)
        {
            if (!plan.ConsumesAmmo || plan.AmmoSlotIndex < 0 || plan.AmmoSlotIndex >= ownerData.CompanionInventory.Storage.Length)
            {
                return;
            }

            Item ammo = ownerData.CompanionInventory.Storage[plan.AmmoSlotIndex];
            if (ammo == null || ammo.IsAir || ammo.stack <= 0 || !ammo.consumable)
            {
                return;
            }

            if (!ItemLoader.CanConsumeAmmo(plan.Weapon, ammo, owner))
            {
                return;
            }

            ItemLoader.OnConsumeAmmo(plan.Weapon, ammo, owner);
            ammo.stack--;
            if (ammo.stack <= 0)
            {
                ammo.TurnToAir();
            }
        }

        private void PlayWeaponUseFeedback(CompanionAttackPlan plan)
        {
            SetVisualHeldItem(plan);
            if (plan.Weapon != null && !plan.Weapon.IsAir && plan.Weapon.UseSound.HasValue)
            {
                SoundEngine.PlaySound(plan.Weapon.UseSound.Value, NPC.Center);
            }
            else
            {
                SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
            }
        }

        private void SetVisualHeldItem(CompanionAttackPlan plan)
        {
            if (plan.Weapon == null || plan.Weapon.IsAir)
            {
                return;
            }

            _visualHeldItem = plan.Weapon.Clone();
            _visualItemAnimationMax = Math.Max(8, Math.Min(60, plan.UseTime));
            _visualItemAnimation = _visualItemAnimationMax;
        }

        private void UpdateDuelDamageCounter(Player owner)
        {
            if (_lastDuelOwnerLife <= 0)
            {
                _lastDuelOwnerLife = owner.statLife;
                return;
            }

            if (owner.statLife < _lastDuelOwnerLife)
            {
                _duelDamageDealtToPlayer += _lastDuelOwnerLife - owner.statLife;
            }

            _lastDuelOwnerLife = owner.statLife;
        }

        private float UpdateDuelMovement(Player owner, AdaptiveDifficultyPlayer ownerData)
        {
            Vector2 toOwner = owner.Center - NPC.Center;
            float distance = toOwner.Length();

            if (distance > 1500f)
            {
                NPC.Center = owner.Center + new Vector2(owner.direction * 108f, -8f);
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
                ownerData.CompanionTeleported();
                return Vector2.Distance(owner.Center, NPC.Center);
            }

            CompanionAttackPlan plan = SelectBestAttackPlan(owner, ownerData, distance, owner.statLife, false, true);
            Vector2 fromPlayer = NPC.Center - owner.Center;
            if (!NormalizeOrDefault(ref fromPlayer, new Vector2(owner.direction == 0 ? 1f : owner.direction, 0f)))
            {
                fromPlayer = new Vector2(1f, 0f);
            }

            Vector2 desired = owner.Center + fromPlayer * MathHelper.Clamp(plan.PreferredDistance, 92f, 380f);
            desired.Y -= HasWingAccessory(ownerData) && owner.Center.Y < NPC.Center.Y - 48f ? 24f : 0f;
            MoveTowardPosition(ownerData, desired, combatMovement: true);
            return distance;
        }

        private void RegisterDamageTaken(int damageDone)
        {
            if (TryGetOwnerData(out _, out AdaptiveDifficultyPlayer ownerData))
            {
                ownerData.CompanionTookDamage(damageDone);
            }

            if (_duelActive)
            {
                _duelDamageTaken += Math.Max(0, damageDone);
                NPC.netUpdate = true;
            }
        }

        private bool IsValidDuelMeleeAttempt(Player player, Item item, Rectangle? meleeAttackHitbox)
        {
            if (!_duelActive || _duelCountdownTicks > 0 || player == null || player.whoAmI != OwnerPlayerIndex || item == null || item.IsAir || item.noMelee)
            {
                return false;
            }

            float itemReach = Math.Max(item.width, item.height);
            float maxReach = 56f + itemReach * 2.1f;
            if (item.pick > 0 || item.axe > 0 || item.hammer > 0)
            {
                maxReach = Math.Min(maxReach + 18f, 118f);
            }
            else
            {
                maxReach = MathHelper.Clamp(maxReach, 82f, 190f);
            }

            if (Vector2.Distance(player.Center, NPC.Center) > maxReach)
            {
                return false;
            }

            if (meleeAttackHitbox.HasValue)
            {
                Rectangle hitbox = meleeAttackHitbox.Value;
                hitbox.Inflate(4, 4);
                Rectangle companionHitbox = NPC.Hitbox;
                companionHitbox.Inflate(8, 8);
                return hitbox.Intersects(companionHitbox);
            }

            return true;
        }

        private bool UpdateInterfaceLeashState(Player owner, AdaptiveDifficultyPlayer ownerData)
        {
            if (!AdaptiveCompanionSystem.IsInterfaceOpenFor(NPC.whoAmI))
            {
                _interfaceLeashActive = false;
                return false;
            }

            float distance = Vector2.Distance(owner.Center, NPC.Center);
            if (distance <= AdaptiveCompanionSystem.FullInterfaceDistance)
            {
                _interfaceLeashActive = true;
            }

            if (!_interfaceLeashActive)
            {
                return false;
            }

            ClampToInterfaceLeash(owner, ownerData);
            return true;
        }

        private void UpdateInterfaceLeashedMovement(Player owner, AdaptiveDifficultyPlayer ownerData, NPC target)
        {
            Vector2 desired;
            if (target != null && Vector2.Distance(owner.Center, target.Center) <= AdaptiveCompanionSystem.FullInterfaceDistance + 96f)
            {
                float targetDistance = Vector2.Distance(NPC.Center, target.Center);
                CompanionAttackPlan plan = SelectBestAttackPlan(owner, ownerData, targetDistance, target.life, target.boss, false);
                Vector2 targetToOwner = owner.Center - target.Center;
                if (!NormalizeOrDefault(ref targetToOwner, NPC.Center - target.Center))
                {
                    targetToOwner = new Vector2(owner.direction == 0 ? -1f : -owner.direction, 0f);
                }

                desired = target.Center + targetToOwner * MathHelper.Clamp(plan.PreferredDistance, 84f, 240f);
                desired.Y = MathHelper.Lerp(desired.Y, owner.Center.Y, 0.55f);
            }
            else
            {
                float phase = ((int)(Main.GameUpdateCount % 3600) / 38f) + NPC.whoAmI * 0.61f;
                desired = owner.Center + new Vector2((float)Math.Sin(phase) * 78f, 2f);
            }

            Vector2 fromOwner = desired - owner.Center;
            float maxDistance = AdaptiveCompanionSystem.FullInterfaceDistance - 38f;
            if (fromOwner.Length() > maxDistance && NormalizeOrDefault(ref fromOwner, new Vector2(owner.direction == 0 ? 1f : owner.direction, 0f)))
            {
                desired = owner.Center + fromOwner * maxDistance;
            }

            MoveTowardPosition(ownerData, desired, combatMovement: target != null);
            ClampToInterfaceLeash(owner, ownerData);
        }

        private void ClampToInterfaceLeash(Player owner, AdaptiveDifficultyPlayer ownerData)
        {
            Vector2 fromOwner = NPC.Center - owner.Center;
            float distance = fromOwner.Length();
            float hardLimit = AdaptiveCompanionSystem.FullInterfaceDistance - 10f;
            if (distance <= hardLimit)
            {
                return;
            }

            if (!NormalizeOrDefault(ref fromOwner, new Vector2(owner.direction == 0 ? 1f : owner.direction, 0f)))
            {
                fromOwner = new Vector2(1f, 0f);
            }

            Vector2 clampedCenter = owner.Center + fromOwner * hardLimit;
            NPC.Center = clampedCenter;
            Vector2 inward = owner.Center - NPC.Center;
            if (NormalizeOrDefault(ref inward, Vector2.Zero))
            {
                NPC.velocity = Vector2.Lerp(NPC.velocity, inward * 4.8f, 0.45f);
            }
            else
            {
                NPC.velocity *= 0.5f;
            }

            NPC.netUpdate = true;
            ownerData.CompanionMetrics.Set("interface_leash_active", 1d);
        }

        private void UpdateAdaptiveMovement(Player owner, AdaptiveDifficultyPlayer ownerData, NPC target)
        {
            float ownerDistance = Vector2.Distance(owner.Center, NPC.Center);
            if (ownerDistance > 1500f)
            {
                NPC.Center = owner.Center + new Vector2(owner.direction * 48f, -12f);
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
                ownerData.CompanionTeleported();
                return;
            }

            Vector2 desired;
            if (target != null)
            {
                float targetDistance = Vector2.Distance(NPC.Center, target.Center);
                CompanionAttackPlan plan = SelectBestAttackPlan(owner, ownerData, targetDistance, target.life, target.boss, false);
                Vector2 targetToOwner = owner.Center - target.Center;
                if (!NormalizeOrDefault(ref targetToOwner, NPC.Center - target.Center))
                {
                    targetToOwner = new Vector2(owner.direction == 0 ? -1f : -owner.direction, 0f);
                }

                desired = target.Center + targetToOwner * plan.PreferredDistance;
                desired.Y = MathHelper.Lerp(desired.Y, owner.Center.Y, 0.35f);
                if (HasWingAccessory(ownerData) && target.Center.Y < NPC.Center.Y - 56f)
                {
                    desired.Y -= 36f;
                }

                Vector2 fromOwner = desired - owner.Center;
                if (fromOwner.Length() > 540f)
                {
                    fromOwner.Normalize();
                    desired = owner.Center + fromOwner * 540f;
                }

                MoveTowardPosition(ownerData, desired, combatMovement: true);
                return;
            }

            float phase = ((int)(Main.GameUpdateCount % 3600) / 44f) + NPC.whoAmI * 0.71f;
            float horizontal = (float)Math.Sin(phase) * 92f;
            if (Math.Abs(owner.velocity.X) > 1.1f)
            {
                horizontal += -Math.Sign(owner.velocity.X) * 42f;
            }

            desired = owner.Center + new Vector2(horizontal, 4f);
            MoveTowardPosition(ownerData, desired, combatMovement: false);
        }

        private void MoveTowardPosition(AdaptiveDifficultyPlayer ownerData, Vector2 desired, bool combatMovement)
        {
            Vector2 offset = desired - NPC.Center;
            float distance = offset.Length();
            NPC.direction = desired.X >= NPC.Center.X ? 1 : -1;
            NPC.spriteDirection = NPC.direction;

            bool hasWings = HasWingAccessory(ownerData);
            bool shouldFly = hasWings && (distance > 240f || desired.Y < NPC.Center.Y - 48f || (combatMovement && Math.Abs(desired.Y - NPC.Center.Y) > 38f));
            if (shouldFly)
            {
                UpdateFlyingMovement(offset, distance, combatMovement);
                return;
            }

            UpdateGroundedMovement(offset, distance);
        }

        private void UpdateFlyingMovement(Vector2 offset, float distance, bool combatMovement)
        {
            NPC.noGravity = true;
            NPC.noTileCollide = distance > 150f;

            Vector2 desiredVelocity = Vector2.Zero;
            if (distance > 8f && NormalizeOrDefault(ref offset, Vector2.Zero))
            {
                float speed = combatMovement ? 8.8f : 7.2f;
                if (distance > 340f)
                {
                    speed += 2.8f;
                }

                desiredVelocity = offset * speed;
            }

            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, combatMovement ? 0.17f : 0.13f);
            if (distance < 16f)
            {
                NPC.velocity *= 0.82f;
            }
        }

        private void UpdateGroundedMovement(Vector2 offset, float distance)
        {
            NPC.noGravity = false;
            NPC.noTileCollide = false;

            float targetSpeed = MathHelper.Clamp(offset.X * 0.075f, -5.2f, 5.2f);
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, targetSpeed, 0.20f);
            if (Math.Abs(offset.X) < 12f)
            {
                NPC.velocity.X *= 0.78f;
            }

            bool grounded = NPC.velocity.Y == 0f || NPC.collideY;
            bool shouldJump = grounded && (NPC.collideX || offset.Y < -30f || (distance > 150f && offset.Y < -12f));
            if (shouldJump)
            {
                NPC.velocity.Y = -7.4f;
            }
        }

        private NPC FindTarget(Player owner, bool interfaceLeashed = false)
        {
            AdaptiveCompanionConfig config = ModContent.GetInstance<AdaptiveCompanionConfig>();
            NPC bestTarget = null;
            float bestScore = config.EngagementRadius;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy())
                {
                    continue;
                }

                float distanceToCompanion = Vector2.Distance(NPC.Center, npc.Center);
                float distanceToOwner = Vector2.Distance(owner.Center, npc.Center);
                if (interfaceLeashed && distanceToOwner > AdaptiveCompanionSystem.FullInterfaceDistance + 96f)
                {
                    continue;
                }

                float weightedDistance = distanceToCompanion * 0.72f + distanceToOwner * 0.28f - (npc.boss ? 180f : 0f);
                if (weightedDistance < bestScore)
                {
                    bestScore = weightedDistance;
                    bestTarget = npc;
                }
            }

            return bestTarget;
        }

        private void TryAttackTarget(NPC target, Player owner, AdaptiveDifficultyPlayer ownerData)
        {
            if (_attackCooldown > 0 || ownerData.CurrentPowerSnapshot.EffectiveCoefficient <= 0.01f)
            {
                ownerData.UpdateCompanionTacticalMetrics(0, 0, 0f, 0, target != null ? Vector2.Distance(NPC.Center, target.Center) : 0f, target != null ? target.life : 0, 0f, 0f);
                return;
            }

            Vector2 delta = target.Center - NPC.Center;
            float distance = delta.Length();
            CompanionAttackPlan plan = SelectBestAttackPlan(owner, ownerData, distance, target.life, target.boss, false);
            if (plan.Damage <= 0 || distance > plan.Range)
            {
                return;
            }

            if (plan.Mode == CompanionAttackMode.Melee && !plan.HasProjectile)
            {
                if (TryMeleeStrikeNpc(target, owner, ownerData, plan))
                {
                    float power = MathHelper.Clamp(ownerData.CurrentPowerSnapshot.EffectiveCoefficient, 0.1f, 10f);
                    _attackCooldown = Math.Max(12, (int)(plan.UseTime / MathHelper.Clamp(0.75f + power * 0.15f, 0.75f, 1.8f)));
                }
                return;
            }

            if (!plan.HasProjectile)
            {
                return;
            }

            FireWeaponProjectile(owner, ownerData, target, plan, target.Center, false);
            float currentPower = MathHelper.Clamp(ownerData.CurrentPowerSnapshot.EffectiveCoefficient, 0.1f, 10f);
            _attackCooldown = Math.Max(12, (int)(plan.UseTime / MathHelper.Clamp(0.75f + currentPower * 0.15f, 0.75f, 1.8f)));
        }

        private bool TryMeleeStrikeNpc(NPC target, Player owner, AdaptiveDifficultyPlayer ownerData, CompanionAttackPlan plan)
        {
            Rectangle strikeBox = NPC.Hitbox;
            int inflateX = Math.Max(34, Math.Min(86, (plan.Weapon?.width ?? 30) + 24));
            int inflateY = Math.Max(16, Math.Min(44, (plan.Weapon?.height ?? 24) / 2 + 12));
            strikeBox.Inflate(inflateX, inflateY);
            if (!strikeBox.Intersects(target.Hitbox))
            {
                return false;
            }

            if (ItemLoader.CanHitNPC(plan.Weapon, owner, target) == false)
            {
                return false;
            }

            float power = MathHelper.Clamp(ownerData.CurrentPowerSnapshot.EffectiveCoefficient, 0.1f, 10f);
            int damage = Math.Max(1, (int)(plan.Damage * power * (target.boss ? 1.08f : 1f)));
            int direction = target.Center.X >= NPC.Center.X ? 1 : -1;
            bool crit = Main.rand.NextFloat() < MathHelper.Clamp(0.04f + ownerData.CurrentPowerSnapshot.PlayerSkillScore * 0.08f, 0.04f, 0.16f);
            int damageDone = target.SimpleStrikeNPC(damage, direction, crit, plan.KnockBack, plan.WeaponDamageClass, false, 0f, false);

            ownerData.CompanionDidMeleeAction();
            ownerData.CompanionDealtDamage(Math.Max(0, damageDone), target.boss);
            if (target.life <= 0)
            {
                ownerData.CompanionKilledTarget();
            }

            PlayWeaponUseFeedback(plan);
            return true;
        }

        private CompanionAttackPlan SelectBestAttackPlan(Player owner, AdaptiveDifficultyPlayer ownerData, float distance, int targetLife, bool targetBoss, bool duel)
        {
            CompanionAttackPlan best = CreateDisabledAttackPlan();
            float bestScore = float.MinValue;
            bool forceStyle = ownerData.ManualCombatStyleEnabled && ownerData.ManualCombatStyle != CompanionCombatStyle.Balanced;
            CompanionAttackMode forcedMode = forceStyle ? (CompanionAttackMode)(int)ownerData.ManualCombatStyle : CompanionAttackMode.Adaptive;

            Item[] storage = ownerData.CompanionInventory.Storage;
            for (int pass = 0; pass < (forceStyle ? 2 : 1); pass++)
            {
                bool requireForcedMode = forceStyle && pass == 0;
                for (int i = 0; i < storage.Length; i++)
                {
                    Item item = storage[i];
                    if (!TryCreatePlanFromItem(owner, ownerData, item, i, out CompanionAttackPlan candidate))
                    {
                        continue;
                    }

                    if (requireForcedMode && candidate.Mode != forcedMode)
                    {
                        continue;
                    }

                    float score = ScoreAttackPlan(candidate, ownerData, distance, targetBoss);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }

                if (best.HasWeapon || !forceStyle)
                {
                    break;
                }
            }

            float threat = targetBoss ? 100f : MathHelper.Clamp(targetLife / 10f, 0f, 100f);
            ownerData.UpdateCompanionTacticalMetrics(best.Damage, best.UseTime, best.Range, (int)best.Mode, distance, targetLife, threat, best.PreferredDistance);
            return best;
        }

        private static CompanionAttackPlan CreateDisabledAttackPlan()
        {
            return new CompanionAttackPlan
            {
                Name = "Оружие не выбрано",
                Weapon = null,
                WeaponSlotIndex = -1,
                Ammo = null,
                AmmoSlotIndex = -1,
                AmmoItemId = 0,
                ConsumesAmmo = false,
                Damage = 0,
                UseTime = 40,
                Range = 0f,
                PreferredDistance = 120f,
                ProjectileSpeed = 0f,
                KnockBack = 0f,
                ProjectileType = ProjectileID.None,
                WeaponDamageClass = Terraria.ModLoader.DamageClass.Generic,
                Mode = CompanionAttackMode.Adaptive,
            };
        }

        private bool TryCreatePlanFromItem(Player owner, AdaptiveDifficultyPlayer ownerData, Item item, int slotIndex, out CompanionAttackPlan plan)
        {
            plan = CreateDisabledAttackPlan();
            if (!IsWeaponCandidate(item))
            {
                return false;
            }

            CompanionAttackMode mode = ClassifyWeapon(item);
            bool needsAmmo = item.useAmmo > 0 && ItemLoader.NeedsAmmo(item, owner);
            Item ammo = null;
            int ammoSlotIndex = -1;
            int ammoItemId = 0;
            int projectileType = item.shoot;
            float shootSpeed = item.shootSpeed > 0f ? item.shootSpeed : 10f;
            float knockBack = Math.Max(0f, item.knockBack);
            int damage = Math.Max(1, item.damage);

            if (needsAmmo)
            {
                if (!TryFindAmmo(ownerData, owner, item, out ammo, out ammoSlotIndex))
                {
                    return false;
                }

                ammoItemId = ammo.type;
                if (ammo.shoot > ProjectileID.None)
                {
                    projectileType = ammo.shoot;
                }

                if (ammo.shootSpeed > 0f)
                {
                    shootSpeed += ammo.shootSpeed;
                }

                knockBack += ammo.knockBack;
                StatModifier ammoDamageModifier = StatModifier.Default;
                ItemLoader.PickAmmo(item, ammo, owner, ref projectileType, ref shootSpeed, ref ammoDamageModifier, ref knockBack);
                damage = Math.Max(1, (int)ammoDamageModifier.ApplyTo(item.damage + Math.Max(0, ammo.damage)));
            }

            if (projectileType <= ProjectileID.None && mode != CompanionAttackMode.Melee)
            {
                return false;
            }

            int useTime = item.useTime > 0 ? item.useTime : item.useAnimation > 0 ? item.useAnimation : 30;
            shootSpeed = MathHelper.Clamp(shootSpeed, 6f, 24f);
            ResolveWeaponDistances(item, mode, projectileType > ProjectileID.None, out float range, out float preferred);

            plan = new CompanionAttackPlan
            {
                Name = item.Name,
                Weapon = item,
                WeaponSlotIndex = slotIndex,
                Ammo = ammo,
                AmmoSlotIndex = ammoSlotIndex,
                AmmoItemId = ammoItemId,
                ConsumesAmmo = needsAmmo,
                Damage = damage,
                UseTime = Math.Max(8, useTime),
                Range = range,
                PreferredDistance = preferred,
                ProjectileSpeed = shootSpeed,
                KnockBack = Math.Max(1f, knockBack),
                ProjectileType = projectileType,
                WeaponDamageClass = item.DamageType ?? Terraria.ModLoader.DamageClass.Generic,
                Mode = mode,
            };

            return true;
        }

        private static bool IsWeaponCandidate(Item item)
        {
            if (item == null || item.IsAir || item.damage <= 0)
            {
                return false;
            }

            if (item.ammo > 0 && item.useAmmo <= 0)
            {
                return false;
            }

            return item.useStyle > 0 || item.shoot > ProjectileID.None || item.pick > 0 || item.axe > 0 || item.hammer > 0;
        }

        private bool TryFindAmmo(AdaptiveDifficultyPlayer ownerData, Player owner, Item weapon, out Item ammo, out int slotIndex)
        {
            Item[] storage = ownerData.CompanionInventory.Storage;
            for (int i = 0; i < storage.Length; i++)
            {
                Item candidate = storage[i];
                if (candidate == null || candidate.IsAir || candidate.stack <= 0)
                {
                    continue;
                }

                if (ItemLoader.CanChooseAmmo(weapon, candidate, owner))
                {
                    ammo = candidate;
                    slotIndex = i;
                    return true;
                }
            }

            ammo = null;
            slotIndex = -1;
            return false;
        }

        private static void ResolveWeaponDistances(Item item, CompanionAttackMode mode, bool hasProjectile, out float range, out float preferred)
        {
            switch (mode)
            {
                case CompanionAttackMode.Melee:
                    range = hasProjectile ? 420f : MathHelper.Clamp(70f + Math.Max(item.width, item.height) * 1.3f, 84f, 156f);
                    preferred = hasProjectile ? 170f : 64f;
                    break;
                case CompanionAttackMode.Ranged:
                    range = 720f;
                    preferred = 455f;
                    break;
                case CompanionAttackMode.Magic:
                    range = 620f;
                    preferred = 360f;
                    break;
                case CompanionAttackMode.Summon:
                    range = 560f;
                    preferred = 320f;
                    break;
                default:
                    range = hasProjectile ? 520f : 130f;
                    preferred = hasProjectile ? 280f : 76f;
                    break;
            }
        }

        private float ScoreAttackPlan(CompanionAttackPlan plan, AdaptiveDifficultyPlayer ownerData, float distance, bool targetBoss)
        {
            if (plan.Damage <= 0 || !plan.HasWeapon)
            {
                return float.MinValue;
            }

            float dps = plan.Damage * 60f / Math.Max(10, plan.UseTime);
            float distancePenalty = MathHelper.Clamp(Math.Abs(distance - plan.PreferredDistance) / Math.Max(80f, plan.Range), 0f, 1f);
            float distanceScore = 1f - distancePenalty * 0.62f;
            if (distance > plan.Range)
            {
                distanceScore *= 0.20f;
            }

            float styleBonus = GetStyleBonus(ownerData.CurrentPowerSnapshot.PreferredStyleCode, plan.Mode);
            if (ownerData.ManualCombatStyleEnabled && ownerData.ManualCombatStyle != CompanionCombatStyle.Balanced)
            {
                CompanionAttackMode forcedMode = (CompanionAttackMode)(int)ownerData.ManualCombatStyle;
                styleBonus += plan.Mode == forcedMode ? 0.65f : -0.55f;
            }

            float safetyBonus = ownerData.PlayerMetrics.Get("life_ratio_current") < 0.38d && plan.Mode != CompanionAttackMode.Melee ? 0.22f : 0f;
            float bossBonus = targetBoss && plan.Range > 350f ? 0.16f : 0f;
            return dps * distanceScore * MathHelper.Clamp(1f + styleBonus + safetyBonus + bossBonus, 0.25f, 2.1f);
        }

        private static float GetStyleBonus(int preferredStyleCode, CompanionAttackMode mode)
        {
            if ((int)mode == preferredStyleCode)
            {
                return 0.20f;
            }

            if (preferredStyleCode == 0)
            {
                return 0.08f;
            }

            return 0f;
        }

        private static CompanionAttackMode ClassifyWeapon(Item item)
        {
            if (item.DamageType == DamageClass.Magic || item.mana > 0)
            {
                return CompanionAttackMode.Magic;
            }

            if (item.DamageType == DamageClass.Ranged || item.useAmmo > 0)
            {
                return CompanionAttackMode.Ranged;
            }

            if (item.DamageType == DamageClass.Summon || item.DamageType == DamageClass.SummonMeleeSpeed)
            {
                return CompanionAttackMode.Summon;
            }

            if (item.DamageType == DamageClass.Melee || item.DamageType == DamageClass.MeleeNoSpeed || !item.noMelee)
            {
                return CompanionAttackMode.Melee;
            }

            return CompanionAttackMode.Adaptive;
        }

        private static bool NormalizeOrDefault(ref Vector2 vector, Vector2 fallback)
        {
            if (vector.LengthSquared() > 0.0001f)
            {
                vector.Normalize();
                return true;
            }

            vector = fallback;
            if (vector.LengthSquared() > 0.0001f)
            {
                vector.Normalize();
                return true;
            }

            return false;
        }

        private void PrepareDrawPlayerAnimation(Player drawPlayer, AdaptiveDifficultyPlayer ownerData)
        {
            bool movingHorizontally = Math.Abs(NPC.velocity.X) > 0.18f;
            bool flying = IsActivelyFlying(ownerData);
            bool airborne = flying || Math.Abs(NPC.velocity.Y) > 0.18f || !NPC.collideY;

            drawPlayer.controlLeft = movingHorizontally && NPC.velocity.X < 0f;
            drawPlayer.controlRight = movingHorizontally && NPC.velocity.X > 0f;
            drawPlayer.controlJump = airborne || flying;
            drawPlayer.controlDown = false;
            drawPlayer.controlUp = false;
            drawPlayer.controlUseItem = _visualHeldItem != null && !_visualHeldItem.IsAir && _visualItemAnimation > 0;

            if (_drawBodyFrameY >= 0 && drawPlayer.bodyFrame.Height > 0)
            {
                drawPlayer.bodyFrame.Y = Math.Min(_drawBodyFrameY, drawPlayer.bodyFrame.Height * 19);
            }

            if (_drawLegFrameY >= 0 && drawPlayer.legFrame.Height > 0)
            {
                drawPlayer.legFrame.Y = Math.Min(_drawLegFrameY, drawPlayer.legFrame.Height * 19);
            }

            drawPlayer.bodyFrameCounter = _drawBodyFrameCounter;
            drawPlayer.legFrameCounter = _drawLegFrameCounter;
        }

        private void CaptureDrawPlayerAnimation(Player drawPlayer)
        {
            _drawBodyFrameCounter = drawPlayer.bodyFrameCounter;
            _drawLegFrameCounter = drawPlayer.legFrameCounter;
            _drawBodyFrameY = drawPlayer.bodyFrame.Y;
            _drawLegFrameY = drawPlayer.legFrame.Y;
        }

        private Player BuildDrawPlayer(Player owner, AdaptiveDifficultyPlayer ownerData)
        {
            Player drawPlayer = owner.Clone() as Player;
            if (drawPlayer == null)
            {
                return null;
            }

            DetachDrawPlayerArrays(drawPlayer, owner);
            drawPlayer.active = true;
            drawPlayer.dead = false;
            drawPlayer.ghost = false;
            drawPlayer.fullRotation = 0f;
            drawPlayer.fullRotationOrigin = Vector2.Zero;
            drawPlayer.gravDir = 1f;
            drawPlayer.whoAmI = owner.whoAmI;
            drawPlayer.position = NPC.Bottom - new Vector2(drawPlayer.width * 0.5f, drawPlayer.height);
            drawPlayer.velocity = NPC.velocity;
            drawPlayer.itemAnimation = 0;
            drawPlayer.itemAnimationMax = 0;
            drawPlayer.itemTime = 0;
            drawPlayer.itemTimeMax = 0;
            drawPlayer.selectedItem = 0;
            drawPlayer.heldProj = -1;
            drawPlayer.mount = new Mount();
            drawPlayer.ResetEffects();
            drawPlayer.ResetVisibleAccessories();
            drawPlayer.ChangeDir(NPC.spriteDirection == 0 ? 1 : NPC.spriteDirection);
            PrepareDrawPlayerAnimation(drawPlayer, ownerData);

            ClearCompanionVisualEquipment(drawPlayer);
            ApplyCompanionEquipment(drawPlayer, ownerData);
            ApplyHeldWeaponVisual(drawPlayer);

            drawPlayer.RefreshItems();
            drawPlayer.RefreshMovementAbilities();
            drawPlayer.head = GetHeadSlot(ownerData);
            drawPlayer.body = GetBodySlot(ownerData);
            drawPlayer.legs = GetLegSlot(ownerData);
            drawPlayer.wingTime = IsActivelyFlying(ownerData) ? 1f : 0f;
            drawPlayer.PlayerFrame();
            CaptureDrawPlayerAnimation(drawPlayer);
            return drawPlayer;
        }

        private void ApplyHeldWeaponVisual(Player drawPlayer)
        {
            if (_visualHeldItem == null || _visualHeldItem.IsAir || _visualItemAnimation <= 0)
            {
                return;
            }

            drawPlayer.inventory[0] = _visualHeldItem.Clone();
            drawPlayer.selectedItem = 0;
            drawPlayer.itemAnimation = _visualItemAnimation;
            drawPlayer.itemAnimationMax = Math.Max(_visualItemAnimationMax, _visualItemAnimation);
            drawPlayer.itemTime = Math.Min(_visualItemAnimation, drawPlayer.itemAnimationMax);
            drawPlayer.itemTimeMax = drawPlayer.itemAnimationMax;
            drawPlayer.heldProj = -1;
            drawPlayer.ChangeDir(NPC.spriteDirection == 0 ? 1 : NPC.spriteDirection);
        }

        private static void DetachDrawPlayerArrays(Player drawPlayer, Player owner)
        {
            drawPlayer.inventory = CloneItemArray(owner.inventory);
            drawPlayer.armor = CreateAirArray(drawPlayer.armor.Length);
            drawPlayer.dye = CreateAirArray(drawPlayer.dye.Length);
            drawPlayer.miscEquips = CreateAirArray(drawPlayer.miscEquips.Length);
            drawPlayer.miscDyes = CreateAirArray(drawPlayer.miscDyes.Length);
            drawPlayer.hideVisibleAccessory = new bool[drawPlayer.hideVisibleAccessory.Length];
        }

        private static void ClearCompanionVisualEquipment(Player drawPlayer)
        {
            for (int i = 0; i < drawPlayer.armor.Length; i++)
            {
                drawPlayer.armor[i] = CreateAirItem();
            }

            for (int i = 0; i < drawPlayer.dye.Length; i++)
            {
                drawPlayer.dye[i] = CreateAirItem();
            }

            for (int i = 0; i < drawPlayer.miscEquips.Length; i++)
            {
                drawPlayer.miscEquips[i] = CreateAirItem();
            }

            for (int i = 0; i < drawPlayer.miscDyes.Length; i++)
            {
                drawPlayer.miscDyes[i] = CreateAirItem();
            }
        }

        private static void ApplyCompanionEquipment(Player drawPlayer, AdaptiveDifficultyPlayer ownerData)
        {
            for (int i = 0; i < ownerData.CompanionInventory.Armor.Length && i < 3; i++)
            {
                if (!ownerData.CompanionInventory.IsArmorHidden(i))
                {
                    drawPlayer.armor[i] = CloneEquippedItem(ownerData.CompanionInventory.Armor[i]);
                }
            }

            for (int i = 0; i < ownerData.CompanionInventory.Accessories.Length && i + 3 < drawPlayer.armor.Length; i++)
            {
                drawPlayer.armor[i + 3] = CloneEquippedItem(ownerData.CompanionInventory.Accessories[i]);
                if (i < drawPlayer.hideVisibleAccessory.Length)
                {
                    drawPlayer.hideVisibleAccessory[i] = ownerData.CompanionInventory.IsAccessoryHidden(i);
                }
            }
        }

        private static int GetCompanionEquipmentDefense(AdaptiveDifficultyPlayer ownerData)
        {
            int defense = 0;
            foreach (Item item in ownerData.CompanionInventory.Armor)
            {
                if (item != null && !item.IsAir)
                {
                    defense += item.defense;
                }
            }

            foreach (Item item in ownerData.CompanionInventory.Accessories)
            {
                if (item != null && !item.IsAir)
                {
                    defense += item.defense;
                }
            }

            return defense;
        }

        private static int GetBestStoredWeaponDamage(AdaptiveDifficultyPlayer ownerData)
        {
            int damage = 0;
            foreach (Item item in ownerData.CompanionInventory.EnumerateWeapons())
            {
                if (item.damage > damage)
                {
                    damage = item.damage;
                }
            }

            return damage;
        }

        private static Item CloneEquippedItem(Item source)
        {
            if (source == null || source.IsAir)
            {
                return CreateAirItem();
            }

            Item clone = source.Clone();
            clone.wornArmor = true;
            return clone;
        }

        private static Item[] CloneItemArray(Item[] source)
        {
            Item[] items = new Item[source.Length];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = source[i] != null && !source[i].IsAir ? source[i].Clone() : CreateAirItem();
            }

            return items;
        }

        private static Item[] CreateAirArray(int size)
        {
            Item[] items = new Item[size];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = CreateAirItem();
            }

            return items;
        }

        private static Item CreateAirItem()
        {
            Item item = new Item();
            item.TurnToAir();
            return item;
        }

        private bool IsActivelyFlying(AdaptiveDifficultyPlayer ownerData)
        {
            return HasWingAccessory(ownerData) && NPC.noGravity && (Math.Abs(NPC.velocity.Y) > 0.15f || Math.Abs(NPC.velocity.X) > 1.5f);
        }

        private static bool HasWingAccessory(AdaptiveDifficultyPlayer ownerData)
        {
            foreach (Item item in ownerData.CompanionInventory.Accessories)
            {
                if (item != null && !item.IsAir && item.wingSlot >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetHeadSlot(AdaptiveDifficultyPlayer ownerData)
        {
            if (ownerData.CompanionInventory.IsArmorHidden(0))
            {
                return -1;
            }

            Item item = ownerData.CompanionInventory.Armor[0];
            return item != null && !item.IsAir ? item.headSlot : -1;
        }

        private static int GetBodySlot(AdaptiveDifficultyPlayer ownerData)
        {
            if (ownerData.CompanionInventory.IsArmorHidden(1))
            {
                return -1;
            }

            Item item = ownerData.CompanionInventory.Armor[1];
            return item != null && !item.IsAir ? item.bodySlot : -1;
        }

        private static int GetLegSlot(AdaptiveDifficultyPlayer ownerData)
        {
            if (ownerData.CompanionInventory.IsArmorHidden(2))
            {
                return -1;
            }

            Item item = ownerData.CompanionInventory.Armor[2];
            return item != null && !item.IsAir ? item.legSlot : -1;
        }

        private void DrawDuelHealthBar(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            const int barWidth = 58;
            const int barHeight = 6;
            float ratio = NPC.lifeMax > 0 ? MathHelper.Clamp(NPC.life / (float)NPC.lifeMax, 0f, 1f) : 0f;
            Vector2 drawPosition = NPC.Top - screenPos + new Vector2(-barWidth * 0.5f, -16f);
            Rectangle background = new Rectangle((int)drawPosition.X, (int)drawPosition.Y, barWidth, barHeight);
            Rectangle fill = new Rectangle(background.X + 1, background.Y + 1, Math.Max(1, (int)((barWidth - 2) * ratio)), barHeight - 2);

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, background, new Color(18, 20, 34, 210));
            spriteBatch.Draw(pixel, fill, new Color(222, 78, 70, 235));
            spriteBatch.Draw(pixel, new Rectangle(background.X, background.Y, barWidth, 1), new Color(255, 255, 255, 160));
            spriteBatch.Draw(pixel, new Rectangle(background.X, background.Bottom - 1, barWidth, 1), new Color(0, 0, 0, 180));
        }

        private bool TryGetOwnerData(out Player owner, out AdaptiveDifficultyPlayer ownerData)
        {
            int index = OwnerPlayerIndex;
            if (index < 0 || index >= Main.maxPlayers)
            {
                owner = null;
                ownerData = null;
                return false;
            }

            owner = Main.player[index];
            if (owner == null || !owner.active)
            {
                ownerData = null;
                return false;
            }

            ownerData = owner.GetModPlayer<AdaptiveDifficultyPlayer>();
            return true;
        }
    }
}
