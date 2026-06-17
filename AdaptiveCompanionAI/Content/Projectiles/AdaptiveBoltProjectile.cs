using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdaptiveCompanionAI.Content.Projectiles
{
    public class AdaptiveBoltProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_466";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            if (Projectile.hostile)
            {
                UpdateDuelHoming();
            }
            else
            {
                UpdateNpcHoming();
            }

            Projectile.rotation += 0.35f;
            Projectile.scale = Projectile.hostile ? 1.18f : 1f;
            Lighting.AddLight(Projectile.Center, 0.2f, 0.45f, 0.8f);

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, -Projectile.velocity * 0.18f, 150, default, Projectile.hostile ? 1.05f : 0.85f);
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // CompanionProjectileGlobal records companion damage for all companion-owned projectiles.
        }

        private void UpdateNpcHoming()
        {
            int targetIndex = (int)Projectile.ai[0];
            if (targetIndex < 0 || targetIndex >= Main.maxNPCs)
            {
                return;
            }

            NPC target = Main.npc[targetIndex];
            if (!target.active || !target.CanBeChasedBy())
            {
                return;
            }

            Vector2 desiredVelocity = target.Center - Projectile.Center;
            if (desiredVelocity.LengthSquared() > 16f)
            {
                desiredVelocity.Normalize();
                desiredVelocity *= 12f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.12f);
            }
        }

        private void UpdateDuelHoming()
        {
            int playerIndex = (int)Projectile.ai[0];
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers)
            {
                return;
            }

            Player target = Main.player[playerIndex];
            if (target == null || !target.active || target.dead)
            {
                return;
            }

            Vector2 desiredVelocity = target.Center - Projectile.Center;
            if (desiredVelocity.LengthSquared() > 36f)
            {
                desiredVelocity.Normalize();
                desiredVelocity *= 11.5f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.055f);
            }
        }

    }
}
