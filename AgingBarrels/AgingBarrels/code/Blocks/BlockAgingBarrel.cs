using Cairo.Freetype;
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

namespace AgingBarrelsMod
{
    public class BlockAgingBarrel: BlockLiquidContainerBase
    {
        public override bool AllowHeldLiquidTransfer => false;
        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        {
            return 0; // Stole this from Food Shelves, stops block affecting cellar ratings
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            PlacedPriorityInteract = true; // Needed to call OnBlockInteractStart when shifting with an item in hand
        }
        // This is also stolen from Food Shelves
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // First, check for behaviors preventing default, for example Reinforcement system
            bool preventDefault = false;
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;

            // Spawn liquid particles
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { new(this) };
                for (int j = 0; j < drops.Length; j++)
                {
                    world.SpawnItemEntity(drops[j], pos, null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, 0, byPlayer);
            }

            world.BlockAccessor.SetBlock(0, pos);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            dsc.AppendLine("");
            dsc.AppendLine(Lang.Get("agingbarrels:helddesc-" + inSlot.Itemstack.Block.FirstCodePart()));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            StringBuilder dsc = new();

            dsc.Append(base.GetPlacedBlockInfo(world, pos, forPlayer));
            BlockEntityContainer becontainer = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer != null)
            {
                if (GetCurrentLitres(pos) > 0)
                {
                    ItemSlot slot = becontainer.Inventory[GetContainerSlotId(pos)];
                    ItemStack contentStack = slot.Itemstack;
                    string curingInfo = CuringInfoCompact(api, slot, 0, false);
                    if (curingInfo.Length > 2) dsc.AppendLine(curingInfo.Substring(2));
                }
            }

            return dsc.ToString();
        }

        public static string CuringInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

            if (transitionStates != null)
            {
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    string comma = ", ";

                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {

                        case EnumTransitionType.Cure:

                            dsc.Append(comma);
                            if (transitionLevel > 0)
                            {
                                dsc.Append(Lang.Get("{0}% cured", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(Lang.Get("will cure in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(Lang.Get("will cure in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(Lang.Get("will cure in {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }

            }

            return dsc.ToString();
        }
    }
}
