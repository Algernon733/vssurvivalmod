using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    // Largely similar to BEBehaviorMPAngledGears
    public class BEBehaviorMPSpurGear : BEBehaviorMPBase
    {
        public BlockFacing Facing => BlockFacing.FromFirstLetter(Block.Variant["orientation"]);

        public BlockFacing axis1 = null;
        public BlockFacing axis2 = null;
        public BlockFacing turnDir1 = null;
        public BlockFacing turnDir2 = null;

        public override float AngleRad
        {
            get
            {
                if (turnDir1 == null)
                    return base.AngleRad;

                float angle = base.AngleRad;
                bool flip = propagationDir == BlockFacing.DOWN || propagationDir == BlockFacing.WEST;
                return flip ? GameMath.TWOPI - angle : angle;
            }
        }

        public BEBehaviorMPSpurGear(BlockEntity blockentity) : base(blockentity)
        {
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

            axis1 = null;
            axis2 = null;

            switch (orientations)
            {
                // Solo gears and gears with axles
                case "n":
                case "ns":
                case "s":
                case "sn":
                    AxisSign = new int[3] { 0, 0, -1 };
                    break;

                case "e":
                case "ew":
                case "w":
                case "we":
                    AxisSign = new int[3] { -1, 0, 0 };
                    break;

                case "u":
                case "ud":
                case "d":
                case "du":
                    AxisSign = new int[3] { 0, 1, 0 };
                    break;

                // Two gears
                case "es":
                    AxisSign = new int[6] { 1, 0, 0, 0, 0, -1 };
                    axis1 = BlockFacing.EAST;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.SOUTH;
                    break;

                case "ws":
                    AxisSign = new int[6] { 0, 0, -1, -1, 0, 0 };
                    axis1 = BlockFacing.WEST;
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.SOUTH;
                    break;

                case "nw":
                    AxisSign = new int[6] { 1, 0, 0, 0, 0, -1 };
                    axis2 = BlockFacing.EAST;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.WEST;
                    break;

                case "sd":
                    AxisSign = new int[6] { 0, 0, -1, 0, -1, 0 };
                    this.turnDir1 = BlockFacing.SOUTH;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "ed":
                    AxisSign = new int[6] { 0, 1, 0, 1, 0, 0 };
                    axis1 = BlockFacing.EAST;
                    axis2 = BlockFacing.DOWN;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "wd":
                    AxisSign = new int[6] { -1, 0, 0, 0, 1, 0 };
                    axis1 = BlockFacing.DOWN;
                    axis2 = BlockFacing.WEST;
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "nd":
                    AxisSign = new int[6] { 0, 0, -1, 0, 1, 0 };
                    axis1 = BlockFacing.DOWN;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "nu":
                    AxisSign = new int[6] { 0, -1, 0, 0, 0, -1 };
                    axis1 = BlockFacing.UP;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "eu":
                    AxisSign = new int[6] { 0, -1, 0, 1, 0, 0 };
                    axis1 = BlockFacing.UP;
                    axis2 = BlockFacing.EAST;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "su":
                    AxisSign = new int[6] { 0, 1, 0, 0, 0, -1 };
                    axis1 = BlockFacing.DOWN;
                    this.turnDir1 = BlockFacing.SOUTH;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "wu":
                    AxisSign = new int[6] { 0, -1, 0, -1, 0, 0 };
                    axis1 = BlockFacing.WEST;
                    axis2 = BlockFacing.UP;
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "en":
                    AxisSign = new int[6] { 0, 0, 1, 1, 0, 0 };
                    axis1 = BlockFacing.SOUTH;
                    axis2 = BlockFacing.NORTH;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.NORTH;
                    break;

                default:
                    AxisSign = new int[3] { 0, 0, -1 };
                    break;
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
        public override float GetResistance()
        {
            return 0.0005f;
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath fromExitTurnDir)
        {
            // This method could be called from another (earlier in the loading chunk) block's Initialise() method, i.e. before this itself is initialised.
            if (this.AxisSign == null) this.SetOrientations();

            BlockSpurGear blockSpurGear = Block as BlockSpurGear;

            if (blockSpurGear.HasExtraGear())
            {
                bool invert = fromExitTurnDir.invert;
                BlockFacing[] connectors = blockSpurGear.Facings;
                BlockFacing inputSide = fromExitTurnDir.OutFacing;
                if (!connectors.Contains(inputSide))
                {
                    inputSide = inputSide.Opposite;
                    invert = !invert;
                }

                MechPowerPath[] paths = new MechPowerPath[connectors.Length];
                for (int i = 0; i < paths.Length; i++)
                {
                    BlockFacing pathFacing = connectors[i];

                    // An spur gear's output side rotates in the opposite sense from the input side
                    paths[i] = new MechPowerPath(pathFacing, this.GearedRatio, null, pathFacing == inputSide ? invert : !invert);
                }
                return paths;
            }

            BlockFacing facing = Facing;

            if (blockSpurGear.HasInsideAxle()) // Power the mounting and opposite axle
            {
                MechPowerPath axial;
                if (fromExitTurnDir.OutFacing.Opposite == facing)
                    axial = fromExitTurnDir;
                else
                    axial = fromExitTurnDir.PropagatedClone(facing, fromExitTurnDir.invert, propagationDir);

                return [axial, new MechPowerPath(axial.OutFacing.Opposite, axial.gearingRatio, Position, !axial.invert)];
            }
            else // Just power the mounting axle
            {
                MechPowerPath toMount;
                if (fromExitTurnDir.OutFacing == facing)
                    toMount = fromExitTurnDir;
                else
                    toMount = new MechPowerPath(facing, fromExitTurnDir.gearingRatio, Position, !fromExitTurnDir.invert);

                return [toMount];
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode)
            {
                string orientations = Block.Variant["orientation"];
                bool rev = propagationDir == axis1 || propagationDir == axis2;
                sb.AppendLine(string.Format(Lang.Get("Orientation: {0} {1}", orientations, rev ? "-" : "")));
            }
        }
    }
}
