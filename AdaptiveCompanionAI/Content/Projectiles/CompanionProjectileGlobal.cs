using System;
using AdaptiveCompanionAI.Common.Players;
using AdaptiveCompanionAI.Content.NPCs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace AdaptiveCompanionAI.Content.Projectiles
{
    public class CompanionProjectileGlobal : GlobalProjectile
    {
        private const string SourceContextPrefix = "AdaptiveCompanionAI|";

        public override bool InstancePerEntity => true;

        public bool FromCompanion { get; set; }
        public bool DuelProjectile { get; set; }
        public int CompanionNpcIndex { get; set; } = -1;
        public int AttackMode { get; set; }

        private bool _ownerProxyActive;
        private int _proxiedOwnerIndex = -1;
        private Vector2 _savedOwnerPosition;
        private Vector2 _savedOwnerVelocity;
        private int _savedOwnerDirection;
        private int _savedOwnerItemAnimation;
        private int _savedOwnerItemAnimationMax;
        private int _savedOwnerItemTime;
        private int _savedOwnerItemTimeMax;

        public static bool IsFromCompanion(Projectile projectile)
        {
            return projectile != null && projectile.active && projectile.GetGlobalProjectile<CompanionProjectileGlobal>().FromCompanion;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            string context = source?.Context;
            if (string.IsNullOrEmpty(context) || !context.StartsWith(SourceContextPrefix, StringComparison.Ordinal))
            {
                return;
            }

            FromCompanion = true;
            CompanionNpcIndex = ReadContextInt(context, "npc", -1);
            AttackMode = ReadContextInt(context, "mode", 0);
            DuelProjectile = ReadContextInt(context, "duel", 0) == 1;
            ApplyDamageFlags(projectile);
        }

        public override bool PreAI(Projectile projectile)
        {
            ApplyDamageFlags(projectile);
            BeginOwnerProxy(projectile);
            return true;
        }

        public override void PostAI(Projectile projectile)
        {
            EndOwnerProxy();
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            BeginOwnerProxy(projectile);
            return true;
        }

        public override void PostDraw(Projectile projectile, Color lightColor)
        {
            EndOwnerProxy();
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if (!FromCompanion)
            {
                return null;
            }

            if (DuelProjectile || target?.ModNPC is AdaptiveCompanionNPC)
            {
                return false;
            }

            return null;
        }

        public override bool CanHitPlayer(Projectile projectile, Player target)
        {
            if (!FromCompanion)
            {
                return true;
            }

            if (!DuelProjectile || !TryGetOwnerIndex(out int ownerIndex))
            {
                return false;
            }

            return target != null && target.active && target.whoAmI == ownerIndex;
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!FromCompanion || DuelProjectile || !TryGetOwnerIndex(out int ownerIndex))
            {
                return;
            }

            Player owner = Main.player[ownerIndex];
            if (!owner.active)
            {
                return;
            }

            AdaptiveDifficultyPlayer ownerData = owner.GetModPlayer<AdaptiveDifficultyPlayer>();
            ownerData.CompanionDealtDamage(damageDone, target.boss);
            if (target.life <= 0)
            {
                ownerData.CompanionKilledTarget();
            }
        }

        private void ApplyDamageFlags(Projectile projectile)
        {
            if (projectile == null || !projectile.active || !FromCompanion)
            {
                return;
            }

            bool shouldBeFriendly = !DuelProjectile;
            bool shouldBeHostile = DuelProjectile;
            if (projectile.friendly != shouldBeFriendly || projectile.hostile != shouldBeHostile)
            {
                projectile.friendly = shouldBeFriendly;
                projectile.hostile = shouldBeHostile;
                projectile.netUpdate = true;
            }
        }

        private void BeginOwnerProxy(Projectile projectile)
        {
            if (_ownerProxyActive || !FromCompanion || !TryGetCompanion(out NPC companion, out AdaptiveCompanionNPC companionNpc) || !TryGetOwnerIndex(out int ownerIndex))
            {
                return;
            }

            Player owner = Main.player[ownerIndex];
            if (!owner.active)
            {
                return;
            }

            _proxiedOwnerIndex = ownerIndex;
            _savedOwnerPosition = owner.position;
            _savedOwnerVelocity = owner.velocity;
            _savedOwnerDirection = owner.direction;
            _savedOwnerItemAnimation = owner.itemAnimation;
            _savedOwnerItemAnimationMax = owner.itemAnimationMax;
            _savedOwnerItemTime = owner.itemTime;
            _savedOwnerItemTimeMax = owner.itemTimeMax;

            owner.Center = companion.Center;
            owner.velocity = companion.velocity;
            owner.direction = companion.direction == 0 ? 1 : companion.direction;
            owner.itemAnimation = Math.Max(owner.itemAnimation, 2);
            owner.itemAnimationMax = Math.Max(owner.itemAnimationMax, owner.itemAnimation);
            owner.itemTime = Math.Max(owner.itemTime, 2);
            owner.itemTimeMax = Math.Max(owner.itemTimeMax, owner.itemTime);
            _ownerProxyActive = true;
        }

        private void EndOwnerProxy()
        {
            if (!_ownerProxyActive || _proxiedOwnerIndex < 0 || _proxiedOwnerIndex >= Main.maxPlayers)
            {
                _ownerProxyActive = false;
                _proxiedOwnerIndex = -1;
                return;
            }

            Player owner = Main.player[_proxiedOwnerIndex];
            owner.position = _savedOwnerPosition;
            owner.velocity = _savedOwnerVelocity;
            owner.direction = _savedOwnerDirection;
            owner.itemAnimation = _savedOwnerItemAnimation;
            owner.itemAnimationMax = _savedOwnerItemAnimationMax;
            owner.itemTime = _savedOwnerItemTime;
            owner.itemTimeMax = _savedOwnerItemTimeMax;
            _ownerProxyActive = false;
            _proxiedOwnerIndex = -1;
        }

        private bool TryGetOwnerIndex(out int ownerIndex)
        {
            ownerIndex = -1;
            if (!TryGetCompanion(out _, out AdaptiveCompanionNPC companionNpc))
            {
                return false;
            }

            int candidate = companionNpc.OwnerPlayerIndex;
            if (candidate < 0 || candidate >= Main.maxPlayers)
            {
                return false;
            }

            ownerIndex = candidate;
            return true;
        }

        private bool TryGetCompanion(out NPC companion, out AdaptiveCompanionNPC companionNpc)
        {
            companion = null;
            companionNpc = null;
            if (CompanionNpcIndex < 0 || CompanionNpcIndex >= Main.maxNPCs)
            {
                return false;
            }

            NPC npc = Main.npc[CompanionNpcIndex];
            if (!npc.active || npc.ModNPC is not AdaptiveCompanionNPC adaptiveCompanion)
            {
                return false;
            }

            companion = npc;
            companionNpc = adaptiveCompanion;
            return true;
        }

        private static int ReadContextInt(string context, string key, int fallback)
        {
            string[] parts = context.Split('|');
            string prefix = key + "=";
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(parts[i].Substring(prefix.Length), out int value))
                {
                    return value;
                }
            }

            return fallback;
        }
    }
}
