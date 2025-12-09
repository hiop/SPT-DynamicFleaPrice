using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace DynamicFleaNamespace;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "hiop.dynamic.flea.price";
    public override string Name { get; init; } = "DynamicFleaPrice";
    public override string Author { get; init; } = "HioP";
    public override List<string>? Contributors { get; init; }
    public override Version Version { get; init; } = new("0.0.1");
    public override Range SptVersion { get; init; } = new("~4.0.0");
    
    
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string? License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader)]
public class DynamicFleaPriceOnLoad(
    ISptLogger<DynamicFleaPriceOnLoad> logger,
    DynamicFleaPrice dynamicFleaPrice
    ) : IOnLoad
{
    public Task OnLoad()
    {
        dynamicFleaPrice.LoadDynamicFleaData();
        dynamicFleaPrice.LoadDynamicFleaConfig();
        
        logger.Success("Dynamic Flea data and config applied!");
        dynamicFleaPrice.UpdateCounterByElapsedTime();
        
        return Task.CompletedTask;
    }
}
