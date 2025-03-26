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
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory;
using System.Net.Http.Headers;

namespace AgingBarrels
{
    class BlockViewableAgingBarrel : BlockAgingBarrel
    {
        #region Render
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<string, MultiTextureMeshRef> meshrefs;

            object obj;
            if (capi.ObjectCache.TryGetValue("barrelMeshRefs" + Code, out obj))
            {
                meshrefs = obj as Dictionary<string, MultiTextureMeshRef>;
            }
            else
            {
                capi.ObjectCache["barrelMeshRefs" + Code] = meshrefs = new Dictionary<string, MultiTextureMeshRef>();
            }

            ItemStack[] contentStacks = GetContents(capi.World, itemstack);
            if (contentStacks == null || contentStacks.Length == 0) return;

            string meshkey = GetBarrelMeshkey(contentStacks[0]);

            MultiTextureMeshRef meshRef;

            if (!meshrefs.TryGetValue(meshkey, out meshRef))
            {
                MeshData meshdata = GenMesh(contentStacks[0]);
                meshrefs[meshkey] = meshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }

            renderinfo.ModelRef = meshRef;
        }


        public string GetBarrelMeshkey(ItemStack liquidStack)
        {
            string s = liquidStack?.StackSize + "x" + liquidStack?.GetHashCode();
            return s;
        }


        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("barrelMeshRefs", out obj))
            {
                Dictionary<int, MultiTextureMeshRef> meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("barrelMeshRefs");
            }
        }
        #endregion

        #region Mesh generation

        public MeshData GenMesh(ItemStack liquidContentStack, BlockPos forBlockPos = null)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            AssetLocation emptyShape = AssetLocation.Create("shapes/block/" + CodeWithoutParts(1) + ".json", "agingbarrels");

            Shape shape = Vintagestory.API.Common.Shape.TryGet(capi, emptyShape);
            if (shape == null)
            {
                api.Logger.Warning(string.Format("Tank block '{0}': Content shape {1} not found. Will try to default to another one.", Code, shape));
                return null;
            }
            MeshData barrelMesh;
            capi.Tesselator.TesselateShape(this, shape, out barrelMesh);

            var containerProps = liquidContentStack?.ItemAttributes?["waterTightContainerProps"];

            MeshData contentMesh = getContentMeshLiquids(liquidContentStack, forBlockPos, containerProps);

            if (contentMesh != null)
            {
                barrelMesh.AddMeshData(contentMesh);
            }

            if (forBlockPos != null)
            {
                // Water flags
                barrelMesh.CustomInts = new CustomMeshDataPartInt(barrelMesh.FlagsCount);
                barrelMesh.CustomInts.Values.Fill(0x4000000); // light foam only
                barrelMesh.CustomInts.Count = barrelMesh.FlagsCount;

                barrelMesh.CustomFloats = new CustomMeshDataPartFloat(barrelMesh.FlagsCount * 2);
                barrelMesh.CustomFloats.Count = barrelMesh.FlagsCount * 2;
            }

            switch (LastCodePart())
            {
                case "west":
                    barrelMesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0.5f * (float)Math.PI, 0);
                    break;
                case "south":
                    barrelMesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 1f * (float)Math.PI, 0);
                    break;
                case "east":
                    barrelMesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 1.5f * (float)Math.PI, 0);
                    break;
            }

            return barrelMesh;
        }

        private MeshData getContentMeshLiquids(ItemStack liquidContentStack, BlockPos forBlockPos, JsonObject containerProps)
        {
            AssetLocation[] opaqueLiquidContentsShape = new AssetLocation[5];
            AssetLocation[] liquidContentsShape = new AssetLocation[5];
            opaqueLiquidContentsShape[0] = AssetLocation.Create("shapes/block/opaquecontents/liquidcontentsbase.json", "agingbarrels");
            liquidContentsShape[0] = AssetLocation.Create("shapes/block/liquidcontents/liquidcontentsbase.json", "agingbarrels");
            for(int i = 1; i < liquidContentsShape.Length; i++)
            {
                opaqueLiquidContentsShape[i] = AssetLocation.Create("shapes/block/opaquecontents/liquidcontentswindow" + i.ToString() + ".json", "agingbarrels");
                liquidContentsShape[i] = AssetLocation.Create("shapes/block/liquidcontents/liquidcontentswindow" + i.ToString() + ".json", "agingbarrels");
            }

            bool isopaque = containerProps?["isopaque"].AsBool(false) == true;

            isopaque = true; //This is necessary because for some reason having a mesh with renderPass: 4 crashes the game

            bool isliquid = containerProps?.Exists == true;
            if (liquidContentStack != null && isliquid)
            {
                AssetLocation[] contentsShapes = isopaque ? opaqueLiquidContentsShape : liquidContentsShape;
                AssetLocation[] shapefilepaths = contentsShapes;

                return getContentMesh(liquidContentStack, forBlockPos, shapefilepaths);
            }
            
            return null;
        }


        protected MeshData getContentMesh(ItemStack stack, BlockPos forBlockPos, AssetLocation[] shapefilepaths)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            WaterTightContainableProps props = GetContainableProps(stack);
            ITexPositionSource contentSource;
            float fillHeight;

            if (props != null)
            {
                if (props.Texture == null) return null;

                contentSource = new ContainerTextureSource(capi, stack, props.Texture);
                fillHeight = GameMath.Min(0.875f, stack.StackSize / props.ItemsPerLitre / 50) * 14f / 16f;
            }
            else
            {
                contentSource = getContentTexture(capi, stack, out fillHeight);
            }


            if (stack != null && contentSource != null)
            {
                Shape[] shapes = new Shape[5];
                for (int i = 0; i < shapefilepaths.Length; i++)
                {
                    shapes[i] = Vintagestory.API.Common.Shape.TryGet(capi, shapefilepaths[i]);
                    if (shapes[i] == null)
                    {
                        api.Logger.Warning(string.Format("Tank block '{0}': Content shape {1} not found. Will try to default to another one.", Code, shapefilepaths[0]));
                        return null;
                    }

                }
                MeshData contentMesh;
                capi.Tesselator.TesselateShape("agingtank", shapes[0], out contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ), props?.GlowLevel ?? 0);
                for (int i = 1; i < fillHeight / 0.1875; i++)
                {
                    MeshData addedMesh;
                    capi.Tesselator.TesselateShape("agingtankwindow", shapes[i], out addedMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ), props?.GlowLevel ?? 0);
                    contentMesh.AddMeshData(addedMesh);
                }

                contentMesh.Translate(0, fillHeight, 0);

                if (props?.ClimateColorMap != null)
                {
                    int col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, 196, 128, false);
                    if (forBlockPos != null)
                    {
                        col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }


                return contentMesh;
            }

            return null;
        }


        public static ITexPositionSource getContentTexture(ICoreClientAPI capi, ItemStack stack, out float fillHeight)
        {
            ITexPositionSource contentSource = null;
            fillHeight = 0;

            JsonObject obj = stack?.ItemAttributes?["inContainerTexture"];
            if (obj != null && obj.Exists)
            {
                contentSource = new ContainerTextureSource(capi, stack, obj.AsObject<CompositeTexture>());
                fillHeight = GameMath.Min(12 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
            }
            else
            {
                if (stack?.Block != null && (stack.Block.DrawType == EnumDrawType.Cube || stack.Block.Shape.Base.Path.Contains("basic/cube")) && capi.BlockTextureAtlas.GetPosition(stack.Block, "up", true) != null)
                {
                    contentSource = new BlockTopTextureSource(capi, stack.Block);
                    fillHeight = GameMath.Min(12 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
                }
                else if (stack != null)
                {

                    if (stack.Class == EnumItemClass.Block)
                    {
                        if (stack.Block.Textures.Count > 1) return null;

                        contentSource = new ContainerTextureSource(capi, stack, stack.Block.Textures.FirstOrDefault().Value);
                    }
                    else
                    {
                        if (stack.Item.Textures.Count > 1) return null;

                        contentSource = new ContainerTextureSource(capi, stack, stack.Item.FirstTexture);
                    }


                    fillHeight = GameMath.Min(12 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
                }
            }

            return contentSource;
        }

        #endregion

    }
}
