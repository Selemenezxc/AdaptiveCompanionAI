using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdaptiveCompanionAI.Content.Projectiles
{
    public class CompanionWeaponProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_14";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
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

            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            float intensity = Projectile.hostile ? 0.78f : 0.58f;
            Lighting.AddLight(Projectile.Center, 0.45f * intensity, 0.52f * intensity, 0.62f * intensity);

            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, -Projectile.velocity * 0.16f, 170, default, Projectile.hostile ? 0.85f : 0.68f);
                dust.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.Kill();
            return false;
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
            if (desiredVelocity.LengthSquared() > 25f)
            {
                desiredVelocity.Normalize();
                desiredVelocity *= MathHelper.Clamp(Projectile.velocity.Length(), 9f, 16f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.08f);
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
                desiredVelocity *= MathHelper.Clamp(Projectile.velocity.Length(), 8.5f, 16f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.045f);
            }
        }
    }
}
