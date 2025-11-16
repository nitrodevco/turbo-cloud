using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;
using Turbo.Primitives.Rooms.Furniture;

namespace Turbo.Rooms.Furniture.Logic;

[FurnitureLogic("default_floor")]
public class FurnitureFloorLogic : FurnitureLogicBase, IFurnitureFloorLogic { }
