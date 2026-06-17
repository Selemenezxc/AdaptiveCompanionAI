using AdaptiveCompanionAI.Common.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdaptiveCompanionAI.Content.Items
{
    public class CompanionBell : ModItem
    {
        public override string Texture => "Terraria/Images/Item_509";

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item4;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(silver: 50);
            Item.maxStack = 1;
            Item.consumable = false;
            Item.noMelee = true;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                AdaptiveCompanionSystem.OpenNearestCompanionInterface(player);
            }
            else
            {
                AdaptiveCompanionSystem.SpawnOrRecallCompanion(player, false);
            }

            return true;
        }
    }
}
