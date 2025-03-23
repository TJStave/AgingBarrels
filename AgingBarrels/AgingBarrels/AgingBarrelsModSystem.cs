using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace AgingBarrelsMod;

public class AgingBarrelsModSystem : ModSystem
{

    // Called on server and client
    // Useful for registering block/entity classes on both sides
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterBlockClass("AgingBarrels.BlockAgingBarrel", typeof(BlockAgingBarrel));
        api.RegisterBlockEntityClass("AgingBarrels.BlockEntityAgingBarrel", typeof(BlockEntityAgingBarrel));
    }

}
