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
    class BlockEntityViewableAgingBarrel : BlockEntityAgingBarrel
    {
        MeshData currentMesh;
        BlockViewableAgingBarrel ownBlock;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockViewableAgingBarrel;

            if (api.Side == EnumAppSide.Client && currentMesh == null)
            {
                    currentMesh = GenMesh();
                    MarkDirty(true);
            }

            this.inventory.SlotModified += Inventory_SlotModified1;
        }

        bool ignoreChange = false;

        private void Inventory_SlotModified1(int slotId)
        {
            if (ignoreChange) return;

            if (slotId == 0)
            {
                if (Api?.Side == EnumAppSide.Client)
                {
                    currentMesh = GenMesh();
                }

                MarkDirty(true);
            }

        }

        internal MeshData GenMesh()
        {
            if (ownBlock == null) return null;

            MeshData mesh = ownBlock.GenMesh(inventory[0].Itemstack);

            if (mesh.CustomInts != null)
            {
                for (int i = 0; i < mesh.CustomInts.Count; i++)
                {
                    mesh.CustomInts.Values[i] |= 1 << 27; // Enable weak water wavy
                    mesh.CustomInts.Values[i] |= 1 << 26; // Enabled weak foam
                }
            }

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return true;
        }
    }
}
