using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    // Based off of AngledGearBlockRenderer
    public class SpurGearPairRenderer : MechBlockRenderer
    {
        MeshRef spurGearOne;
        MeshRef spurGearTwo;

        CustomMeshDataPartFloat floatsSpurGearOne;
        CustomMeshDataPartFloat floatsSpurGearTwo;

        static readonly Vec3f center = new(0.5f, 0.5f, 0.5f);

        public SpurGearPairRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSourceBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);
            Shape shape = Shape.TryGet(capi, "shapes/block/wood/mechanics/spurgear16.json");

            // Gear two is perpendicular to gear one
            capi.Tesselator.TesselateShape(textureSourceBlock, shape, out MeshData spurGearOneMesh, rot);
            capi.Tesselator.TesselateShape(textureSourceBlock, shape, out MeshData spurGearTwoMesh, new Vec3f(0, 270, 0));

            _ = spurGearTwoMesh.Rotate(center, rot.X * GameMath.DEG2RAD, rot.Y * GameMath.DEG2RAD, rot.Z * GameMath.DEG2RAD);

            // 16 floats matrix, 4 floats light rgbs
            spurGearOneMesh.CustomFloats = floatsSpurGearOne = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = [0, 16, 32, 48, 64],
                InterleaveSizes = [4, 4, 4, 4, 4],
                InterleaveStride = 16 + (4 * 16),
                StaticDraw = false,
            };
            spurGearOneMesh.CustomFloats.SetAllocationSize((16 + 4) * 10100);
            spurGearTwoMesh.CustomFloats = floatsSpurGearTwo = floatsSpurGearOne.Clone();

            this.spurGearOne = capi.Render.UploadMesh(spurGearOneMesh);
            this.spurGearTwo = capi.Render.UploadMesh(spurGearTwoMesh);
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotationRad, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPSpurGear gear = dev as BEBehaviorMPSpurGear;

            UpdateGearTransformMatrix(floatsSpurGearOne.Values, index, distToCamera, dev, gear, gear?.gearOneFacing, rotationRad);
            UpdateGearTransformMatrix(floatsSpurGearTwo.Values, index, distToCamera, dev, gear, gear?.gearTwoFacing, rotationRad);
        }

        private void UpdateGearTransformMatrix(float[] values, int index, Vec3f distToCamera, IMechanicalPowerRenderable dev, BEBehaviorMPSpurGear gear, BlockFacing gearFacing, float rotationRad)
        {
            float rotX = 0f, rotY = 0f, rotZ = 0f;

            if (gearFacing != null)
            {
                BlockFacing turnDir = gear.GearTurnDir(gearFacing);
                if (turnDir == BlockFacing.DOWN || turnDir == BlockFacing.EAST || turnDir == BlockFacing.SOUTH)
                {
                    rotationRad = -rotationRad;
                }

                // Each spur gear is rotated a quarter of a tooth,
                // so when two gears meet their teeth are offset by half a tooth, resulting in their teeth interlacing
                float gearToothPhase = -5.625f * GameMath.DEG2RAD * (gearFacing.Normali.X + gearFacing.Normali.Y + gearFacing.Normali.Z);

                switch (gearFacing.Axis)
                {
                    case EnumAxis.X: rotX = -rotationRad + gearToothPhase; break;
                    case EnumAxis.Y: rotY = rotationRad + gearToothPhase; break;
                    case EnumAxis.Z: rotZ = -rotationRad + gearToothPhase; break;
                }
            }

            UpdateLightAndTransformMatrix(values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                floatsSpurGearOne.Count = quantityBlocks * 20;
                floatsSpurGearTwo.Count = quantityBlocks * 20;

                updateMesh.CustomFloats = floatsSpurGearOne;
                capi.Render.UpdateMesh(spurGearOne, updateMesh);

                updateMesh.CustomFloats = floatsSpurGearTwo;
                capi.Render.UpdateMesh(spurGearTwo, updateMesh);

                capi.Render.RenderMeshInstanced(spurGearOne, quantityBlocks);
                capi.Render.RenderMeshInstanced(spurGearTwo, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            spurGearOne?.Dispose();
            spurGearTwo?.Dispose();
        }
    }
}
