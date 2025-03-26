using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AgingBarrels
{
    class BlockEntityAgingBarrel : BlockEntityLiquidContainer
    {
        private BlockAgingBarrel block;
        private float CureRate { get; set; } = 1f;
        private int CapacityLitres { get; set; } = 50;
        // Stole this from Food Shelves
        public override string InventoryClassName => Block?.Attributes?["inventoryClassName"].AsString();

        public BlockEntityAgingBarrel()
        {
            this.inventory = new InventoryGeneric(1, InventoryClassName + "-0", Api, (_, inv) => new ItemSlotLiquidOnly(inv, CapacityLitres));
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            block = api.World.BlockAccessor.GetBlock(Pos) as BlockAgingBarrel;

            if (block?.Attributes?["capacityLitres"].Exists == true)
            {
                CapacityLitres = block.Attributes["capacityLitres"].AsInt(50);
                (this.inventory[0] as ItemSlotLiquidOnly).CapacityLitres = CapacityLitres;
            }
            if (block?.Attributes?["cureRate"].Exists == true)
            {
                CureRate = block.Attributes["cureRate"].AsFloat(50);
            }

            this.inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed;
        }

        private float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float mulByConfig)
        {
            if (transType == EnumTransitionType.Cure) return CureRate;
            else return 1f;
        }
    }
}
