using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    // Based off of BlockAngledGears
    public class BlockSpurGear : BlockMPBase
    {
        public string Orientation;
        public BlockFacing Facing;

        public override void OnLoaded(ICoreAPI api)
        {
            Orientation = Variant["orientation"];
            Facing = BlockFacing.FromFirstLetter(Orientation[0]);
            base.OnLoaded(api);
        }

        public BlockFacing[] Facings
        {
            get
            {
                string dirs = Orientation;
                BlockFacing[] facings = new BlockFacing[dirs.Length];
                for (int i = 0; i < dirs.Length; i++)
                {
                    facings[i] = BlockFacing.FromFirstLetter(dirs[i]);
                }

                return facings;
            }
        }

        public bool IsSoloGear() => Orientation.Length == 1;
        public bool HasInsideAxle() => Orientation.Length == 2 && Orientation[1] == Facing.Opposite.Code[0];
        public bool HasExtraGear() => Orientation.Length == 2 && !HasInsideAxle();
        public bool IsOrientedTo(BlockFacing facing) => Orientation.Contains(facing.Code[0]);

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock) => IsOrientedTo(face);

        /// <summary>
        /// Only wooden axles can be placed inside spur gears.
        /// </summary>
        public static bool WoodenAxleCheck(Block block) => block is BlockAxle && block.FirstCodePart() == "woodenaxle";

        public override bool IsReplacableBy(Block block)
        {
            // Either an extra spur or an axle can be potentially placed inside 
            if (block is BlockSpurGear || WoodenAxleCheck(block))
                return true;

            return base.IsReplacableBy(block);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos) => new(world.GetBlock(CodeWithVariant("orientation", "s")));

        public Block getGearBlock(IWorldAccessor world, BlockFacing facing, BlockFacing extraGearFacing = null)
        {
            if (extraGearFacing == null)
                return world.GetBlock(new AssetLocation(FirstCodePart() + "-" + facing.Code[0]));

            AssetLocation loc = new AssetLocation(FirstCodePart() + "-" + facing.Code[0] + extraGearFacing.Code[0]);
            Block toPlaceBlock = world.GetBlock(loc);

            if (toPlaceBlock is not BlockSpurGear)
            {
                loc = new AssetLocation(FirstCodePart() + "-" + extraGearFacing.Code[0] + facing.Code[0]);
                toPlaceBlock = world.GetBlock(loc);
            }

            if (toPlaceBlock is not BlockSpurGear)
                return null;
            else
                return toPlaceBlock;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }

        protected bool TryAddExtraGear(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlock(blockSel.Position) is not BlockSpurGear existingGear)
                return false;

            BlockPos gearPos = blockSel.Position;
            BlockFacing extraGearWillBeFacing;
            if (blockSel.DidOffset)
                extraGearWillBeFacing = blockSel.Face.Opposite;
            else
                extraGearWillBeFacing = blockSel.Face;

            if (!existingGear.IsSoloGear())
                return false;
            if (!extraGearWillBeFacing.IsAdjacent(existingGear.Facing))
                return false;

            BlockPos axlePos = gearPos.AddCopy(extraGearWillBeFacing);
            BlockEntity be = world.BlockAccessor.GetBlockEntity(axlePos);
            BEBehaviorMPAxle bempaxle = be?.GetBehavior<BEBehaviorMPAxle>();
            if (bempaxle == null || !(bempaxle.Block as BlockMPBase).HasMechPowerConnectorAt(world, axlePos, extraGearWillBeFacing.Opposite, this))
                return false;

            Block toPlaceBlock = getGearBlock(world, existingGear.Facing, extraGearWillBeFacing);
            if (toPlaceBlock == null)
                return false;

            // The server will update the client with the new gear block's state
            if (world.Side == EnumAppSide.Client)
                return true;

            (toPlaceBlock as BlockMPBase).ExchangeBlockAt(world, gearPos);

            // Attach the new extra gear to its axle
            IMechanicalPowerBlock neighbour = be?.Block as IMechanicalPowerBlock;
            neighbour?.DidConnectAt(world, axlePos, extraGearWillBeFacing.Opposite);
            WasPlaced(world, gearPos, extraGearWillBeFacing);

            return true;
        }

        public bool TryAddInsideAxle(IWorldAccessor world, BlockPos pos)
        {
            if (!IsSoloGear())
                return false;

            Block toPlaceBlock = getGearBlock(world, Facing, Facing.Opposite);
            if (toPlaceBlock == null)
                return false;

            // The server will update the client with the new gear block's state
            if (world.Side == EnumAppSide.Client)
                return true;

            (toPlaceBlock as BlockMPBase).ExchangeBlockAt(world, pos);

            // Attach the new inside axle to the opposite axle
            BlockPos oppositeAxlePos = pos.AddCopy(Facing.Opposite);
            IMechanicalPowerBlock neighbour = world.BlockAccessor.GetBlock(oppositeAxlePos) as IMechanicalPowerBlock;
            if (neighbour != null && neighbour.HasMechPowerConnectorAt(world, oppositeAxlePos, Facing, this))
            {
                neighbour.DidConnectAt(world, oppositeAxlePos, Facing);
                WasPlaced(world, pos, Facing.Opposite);
            }

            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Right clicking/placing a solo spur gear with a wooden axle puts it inside the spur gear' block
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (WoodenAxleCheck(slot?.Itemstack?.Block) && TryAddInsideAxle(world, blockSel.Position))
            {
                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }

                world.PlaySoundAt(Sounds.Place, blockSel.Position, 0.5f, byPlayer);
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // Right clicking/placing an adjacent axle to a solo spur gear with another spur gear puts it on that adjacent axle inside the spur gear' block
            if (TryAddExtraGear(world, byPlayer, blockSel))
            {
                return true;
            }
            else if (world.BlockAccessor.GetBlock(blockSel.Position) is BlockSpurGear)
            {
                // "Another block is in the way"
                failureCode = "notreplaceable";
                return false;
            }

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing targetFace = null;
            bool axleFoundButUnsupported = false;
            // Try put the gear on the adjacent axle thats being looked at by prepending it
            foreach (BlockFacing face in BlockFacing.ALLFACES.Prepend(blockSel.Face.Opposite))
            {
                BlockPos pos = blockSel.Position.AddCopy(face);
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                IMechanicalPowerBlock neighbour = be?.Block as IMechanicalPowerBlock;

                BEBehaviorMPAxle bempaxle = be?.GetBehavior<BEBehaviorMPAxle>();
                if (bempaxle == null || !(bempaxle.Block as BlockMPBase).HasMechPowerConnectorAt(world, pos, face.Opposite, this))
                    continue;

                // axlemusthavesupport condition, but pretty sure this is impossible right now
                if (!BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, neighbour as Block, pos))
                {
                    axleFoundButUnsupported = true;
                    continue;
                }

                targetFace = face;
                break;
            }

            if (targetFace == null)
            {
                if (axleFoundButUnsupported)
                    failureCode = "axlemusthavesupport";
                else
                    failureCode = "requiresaxle";

                return false;
            }

            Block toPlaceBlock = getGearBlock(world, targetFace);
            world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);

            BEBehaviorMPBase beSpurGear = GetBEBehavior<BEBehaviorMPBase>(blockSel.Position);
            if (beSpurGear == null)
                return true; // From BlockAngledGears: "fixes CTD when trying to place above block limit without an error message, to match other blocks"
            MechPowerPath[] mechPowerExits = beSpurGear.GetMechPowerExits(new MechPowerPath() { OutFacing = targetFace });

            foreach (MechPowerPath mechPowerExit in mechPowerExits)
            {
                BlockPos neighbourPos = blockSel.Position.AddCopy(mechPowerExit.OutFacing);
                var neighbourBlock = world.BlockAccessor.GetBlock(neighbourPos) as IMechanicalPowerBlock;
                neighbourBlock?.DidConnectAt(world, neighbourPos, mechPowerExit.OutFacing.Opposite);
                if (neighbourBlock != null)
                {
                    beSpurGear.tryConnect(mechPowerExit.OutFacing);
                }
            }

            return true;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            bool isSupported = false;
            foreach (BlockFacing facing in Facings)
            {
                Block neighbourBlock = world.BlockAccessor.GetBlock(pos.AddCopy(facing));
                if (neighbourBlock is BlockMPBase && !neighbourBlock.SideIsSolid(world.BlockAccessor, pos, facing.Opposite.Index))
                {
                    isSupported = true;
                    break;
                }
            }

            if (!isSupported)
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            var last = LastCodePart();
            string first;
            string second = string.Empty;
            if (last.Length > 1)
            {
                first = last.Substring(0, 1);
                second = last.Substring(1);
            }
            else
            {
                first = last;
            }

            if (first != "u" && first != "d")
            {
                var beforeFacing = BlockFacing.FromFirstLetter(first);
                int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
                var nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];
                first = nowFacing.Code.Substring(0, 1);
            }

            if (second != string.Empty && second != "u" && second != "d")
            {
                var beforeFacing = BlockFacing.FromFirstLetter(second);
                int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
                var nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];
                second = nowFacing.Code.Substring(0, 1);
            }
            var newOrientation = CodeWithParts(first+second);

            if (api.World.GetBlock(newOrientation) is not BlockSpurGear)
            {
                return CodeWithParts(second+first);
            }

            return newOrientation;
        }
    }
}
