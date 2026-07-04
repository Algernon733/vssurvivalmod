using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    // Largely similar to BEBehaviorMPAngledGears
    public class BEBehaviorMPSpurGear : BEBehaviorMPBase
    {
        public BlockFacing Facing => BlockFacing.FromFirstLetter(Block.Variant["orientation"]);

        public BlockFacing turnDir1 = null;
        public BlockFacing turnDir2 = null;
        public BlockFacing gearOneFacing = null;
        public BlockFacing gearTwoFacing = null;

        float angleOffset;

        static readonly Dictionary<string, (BlockFacing turnDir1, BlockFacing turnDir2, BlockFacing gearOne, BlockFacing gearTwo)> pairOrientationTable = new()
        {
            ["en"] = (BlockFacing.EAST,  BlockFacing.NORTH, BlockFacing.NORTH, BlockFacing.EAST),
            ["nw"] = (BlockFacing.NORTH, BlockFacing.WEST,  BlockFacing.WEST,  BlockFacing.NORTH),
            ["ws"] = (BlockFacing.WEST,  BlockFacing.SOUTH, BlockFacing.SOUTH, BlockFacing.WEST),
            ["es"] = (BlockFacing.EAST,  BlockFacing.SOUTH, BlockFacing.EAST,  BlockFacing.SOUTH),
            ["nu"] = (BlockFacing.NORTH, BlockFacing.UP,    BlockFacing.UP,    BlockFacing.NORTH),
            ["eu"] = (BlockFacing.EAST,  BlockFacing.UP,    BlockFacing.UP,    BlockFacing.EAST),
            ["su"] = (BlockFacing.SOUTH, BlockFacing.UP,    BlockFacing.UP,    BlockFacing.SOUTH),
            ["wu"] = (BlockFacing.WEST,  BlockFacing.UP,    BlockFacing.UP,    BlockFacing.WEST),
            ["nd"] = (BlockFacing.NORTH, BlockFacing.DOWN,  BlockFacing.NORTH, BlockFacing.DOWN),
            ["ed"] = (BlockFacing.EAST,  BlockFacing.DOWN,  BlockFacing.DOWN,  BlockFacing.EAST),
            ["sd"] = (BlockFacing.SOUTH, BlockFacing.DOWN,  BlockFacing.SOUTH, BlockFacing.DOWN),
            ["wd"] = (BlockFacing.WEST,  BlockFacing.DOWN,  BlockFacing.WEST,  BlockFacing.DOWN),
        };

        public override float AngleRad
        {
            get
            {
                if (turnDir1 == null)
                    return base.AngleRad + angleOffset;

                if (network != null)
                    lastKnownAngleRad = network.AngleRad * GearedRatio % GameMath.TWOPI;

                return lastKnownAngleRad;
            }
        }

        public BEBehaviorMPSpurGear(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            // Each spur gear is rotated a quarter of a tooth,
            // so when two gears meet their teeth are offset by half a tooth, resulting in their teeth interlacing
            float quarter16ToothRotationDeg = -5.625f;
            Vec3f gearToothRotation = Facing.Normalf * (quarter16ToothRotationDeg * GameMath.DEG2RAD);
            Vec3f rotationAxis = Facing.Axis switch
            {
                EnumAxis.X => new Vec3f(-1f, 0f, 0f),
                EnumAxis.Y => new Vec3f(0f, 1f, 0f),
                _ => new Vec3f(0f, 0f, -1f),
            };

            angleOffset = gearToothRotation.Dot(rotationAxis);
        }

        public override void SetOrientations()
        {
            string orientations = (Block as BlockSpurGear).Orientation;

            // This applies only when the BE is being updated when the gear orientations change
            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) propagationDir = turnDir2.Opposite;
                else if (propagationDir == turnDir2) propagationDir = turnDir1.Opposite;
                else if (propagationDir == turnDir2.Opposite) propagationDir = turnDir1;
                else if (propagationDir == turnDir1.Opposite) propagationDir = turnDir2;
                this.turnDir1 = null;
                this.turnDir2 = null;
            }

            gearOneFacing = null;
            gearTwoFacing = null;

            bool isGearPair = pairOrientationTable.TryGetValue(orientations, out var pair);
            turnDir1 = pair.turnDir1;
            turnDir2 = pair.turnDir2;
            gearOneFacing = pair.gearOne;
            gearTwoFacing = pair.gearTwo;

            if (isGearPair)
            {
                AxisSign = new int[6]; // Defer to SpurGearPairRenderer to spin each gear individually
            }
            else
            {
                // Solo gears and gears with axles spin around their mounting axle's axis
                AxisSign = Facing.Axis switch
                {
                    EnumAxis.X => [-1, 0, 0],
                    EnumAxis.Y => [0, 1, 0],
                    _ => [0, 0, -1],
                };
            }
        }

        public override void SetPropagationDirection(MechPowerPath path)
        {
            BlockFacing turnDir = path.NetworkDir();
            if (this.turnDir1 != null)
            {
                // Rotate the input turn direction if it has an extra gear (this helps later blocks to know which sense the network is turning)
                if (turnDir == turnDir1) turnDir = turnDir2.Opposite;
                else if (turnDir == turnDir2) turnDir = turnDir1.Opposite;
                else if (turnDir == turnDir2.Opposite) turnDir = turnDir1;
                else if (turnDir == turnDir1.Opposite) turnDir = turnDir2;
                path = new MechPowerPath(turnDir, path.gearingRatio, null, false);
            }

            base.SetPropagationDirection(path);
        }

        public override BlockFacing GetPropagationDirectionInput()
        {
            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) return turnDir2.Opposite;
                if (propagationDir == turnDir2) return turnDir1.Opposite;
                if (propagationDir == turnDir1.Opposite) return turnDir2;
                if (propagationDir == turnDir2.Opposite) return turnDir1;
            }

            return propagationDir;
        }

        public override bool IsPropagationDirection(BlockPos fromPos, BlockFacing test)
        {
            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) return propagationDir == test || test == turnDir2.Opposite;
                if (propagationDir == turnDir2) return propagationDir == test || test == turnDir1.Opposite;
                if (propagationDir == turnDir1.Opposite) return propagationDir == test || test == turnDir2;
                if (propagationDir == turnDir2.Opposite) return propagationDir == test || test == turnDir1;
            }

            return propagationDir == test;
        }

        public BlockFacing GearTurnDir(BlockFacing gearFacing)
        {
            if (propagationDir.Axis == gearFacing.Axis)
                return propagationDir;

            return GetPropagationDirectionInput();
        }

        public override BlockFacing GetPropagatingTurnDir(BlockFacing toFacing)
        {
            BlockSpurGear blockSpurGear = Block as BlockSpurGear;
            BlockFacing meshedGear;
            if (Api == null)
                meshedGear = null;
            else
                meshedGear = blockSpurGear.GetInterlacingGearsFace(Api.World.BlockAccessor.GetBlock(Position.AddCopy(toFacing)), toFacing);

            if (meshedGear != null)
                return GearTurnDir(meshedGear).Opposite; // Side by side. Turn the interlaced spur the opposite rotation

            return base.GetPropagatingTurnDir(toFacing);
        }

        public override float GetResistance()
        {
            return 0.0005f;
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath fromExitTurnDir)
        {
            // This method could be called from another (earlier in the loading chunk) block's Initialise() method, i.e. before this itself is initialised.
            if (this.AxisSign == null)
                this.SetOrientations();

            BlockSpurGear blockSpurGear = Block as BlockSpurGear;
            List<MechPowerPath> paths = [];

            if (blockSpurGear.HasExtraGear())
            {
                BlockFacing[] connectors = blockSpurGear.Facings;
                BlockFacing inputSide = fromExitTurnDir.OutFacing;
                bool invert = fromExitTurnDir.invert;

                if (connectors.Contains(inputSide) || connectors.Contains(inputSide.Opposite)) // Power came in through an axle
                {
                    if (!connectors.Contains(inputSide))
                    {
                        inputSide = inputSide.Opposite;
                        invert = !invert;
                    }

                    foreach (BlockFacing pathFacing in connectors)
                    {
                        if (GetInterlacingGearFace(pathFacing) != null)
                            continue;

                        paths.Add(new MechPowerPath(pathFacing, this.GearedRatio, null, pathFacing == inputSide ? invert : !invert));
                    }
                }
                else // Power came in through an interlaced gear
                {
                    foreach (BlockFacing pathFacing in connectors)
                    {
                        if (GetInterlacingGearFace(pathFacing) != null)
                            continue;

                        BlockFacing gearTurnDir = GearTurnDir(pathFacing);
                        paths.Add(fromExitTurnDir.PropagatedClone(pathFacing, gearTurnDir != pathFacing, gearTurnDir));
                    }
                }
            }
            else
            {
                MechPowerPath mechPower;
                MechPowerPath oppositeMechPower = null;
                if (blockSpurGear.HasInsideAxle()) // Power the mounting and opposite axle
                {
                    if (fromExitTurnDir.OutFacing.Opposite == Facing)
                        mechPower = fromExitTurnDir;
                    else
                        mechPower = fromExitTurnDir.PropagatedClone(Facing, fromExitTurnDir.invert, propagationDir);
                    oppositeMechPower = new MechPowerPath(mechPower.OutFacing.Opposite, mechPower.gearingRatio, Position, !mechPower.invert);
                }
                else // Just power the mounting axle
                {
                    if (fromExitTurnDir.OutFacing == Facing)
                        mechPower = fromExitTurnDir;
                    else if (fromExitTurnDir.OutFacing.Axis != Facing.Axis)
                        mechPower = fromExitTurnDir.PropagatedClone(Facing, fromExitTurnDir.invert, propagationDir);
                    else
                        mechPower = new MechPowerPath(Facing, fromExitTurnDir.gearingRatio, Position, !fromExitTurnDir.invert);
                }

                paths.Add(mechPower);
                if (oppositeMechPower != null)
                    paths.Add(oppositeMechPower);
            }

            // Spin the adjaceent+interlaced spur gears in the opposite sense
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                BlockFacing meshedGear = GetInterlacingGearFace(face);
                if (meshedGear != null)
                    paths.Add(fromExitTurnDir.PropagatedClone(face, !fromExitTurnDir.invert, GearTurnDir(meshedGear).Opposite));
            }

            return [.. paths];
        }

        /// <summary>
        /// Null if not interlacing
        /// </summary>
        private BlockFacing GetInterlacingGearFace(BlockFacing face)
        {
            if (Api == null)
                return null;

            return (Block as BlockSpurGear).GetInterlacingGearsFace(Api.World.BlockAccessor.GetBlock(Pos.AddCopy(face)), face);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode)
            {
                sb.AppendLine(string.Format(Lang.Get("Orientation: {0}", Block.Variant["orientation"])));
                if (gearOneFacing != null)
                {
                    sb.AppendLine(string.Format("Gear one: {0} turning {1}, gear two: {2} turning {3}", gearOneFacing, GearTurnDir(gearOneFacing), gearTwoFacing, GearTurnDir(gearTwoFacing)));
                }
            }
        }
    }
}
