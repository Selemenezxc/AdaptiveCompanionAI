using Terraria;

namespace AdaptiveCompanionAI.Common.Data
{
    public sealed class PlayerProgressSnapshot
    {
        public int BossesDefeated { get; private set; }
        public double ProgressRatio { get; private set; }
        public int LifeMax { get; private set; }
        public int ManaMax { get; private set; }
        public int Defense { get; private set; }
        public int CurrentWeaponDamage { get; private set; }
        public double InventoryFillRatio { get; private set; }
        public int ArmorRating { get; private set; }

        public static PlayerProgressSnapshot Capture(Player player)
        {
            PlayerProgressSnapshot snapshot = new PlayerProgressSnapshot();
            int defeated = 0;
            int checkpoints = 13;

            if (NPC.downedSlimeKing) defeated++;
            if (NPC.downedBoss1) defeated++;
            if (NPC.downedBoss2) defeated++;
            if (NPC.downedBoss3) defeated++;
            if (NPC.downedQueenBee) defeated++;
            if (NPC.downedDeerclops) defeated++;
            if (Main.hardMode) defeated++;
            if (NPC.downedQueenSlime) defeated++;
            if (NPC.downedMechBossAny) defeated++;
            if (NPC.downedPlantBoss) defeated++;
            if (NPC.downedGolemBoss) defeated++;
            if (NPC.downedAncientCultist) defeated++;
            if (NPC.downedMoonlord) defeated++;

            snapshot.BossesDefeated = defeated;
            snapshot.ProgressRatio = CompanionMath.Clamp01(defeated / (double)checkpoints);
            snapshot.LifeMax = player.statLifeMax2;
            snapshot.ManaMax = player.statManaMax2;
            snapshot.Defense = player.statDefense;
            snapshot.CurrentWeaponDamage = player.HeldItem != null ? player.HeldItem.damage : 0;
            snapshot.InventoryFillRatio = CalculateInventoryFillRatio(player);
            snapshot.ArmorRating = CalculateArmorRating(player);
            return snapshot;
        }

        private static double CalculateInventoryFillRatio(Player player)
        {
            int slots = 40;
            int used = 0;
            for (int i = 0; i < slots && i < player.inventory.Length; i++)
            {
                if (player.inventory[i] != null && !player.inventory[i].IsAir)
                {
                    used++;
                }
            }

            return CompanionMath.SafeRate(used, slots);
        }

        private static int CalculateArmorRating(Player player)
        {
            int rating = 0;
            for (int i = 0; i < player.armor.Length; i++)
            {
                if (player.armor[i] != null && !player.armor[i].IsAir)
                {
                    rating += player.armor[i].defense;
                }
            }

            return rating;
        }
    }
}
