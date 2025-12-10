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
    public override Version Version { get; init; } = new("0.0.2");
    public override Range SptVersion { get; init; } = new("~4.0.0");
    
    
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string? License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader+10)]
public class DynamicFleaPriceOnLoad(
    ISptLogger<DynamicFleaPriceOnLoad> logger,
    DynamicFleaPrice dynamicFleaPrice
    ) : IOnLoad
{
    public Task OnLoad()
    {
        dynamicFleaPrice.LoadDynamicFleaData();
        dynamicFleaPrice.LoadDynamicFleaConfig();
        
        logger.Success("Dynamic Flea Price data and config applied!");

        if (dynamicFleaPrice.GetDecreaseOfPurchasePeriod() == null)
        {
            logger.Error("The counter update cycle has not started. Check your settings.");
            return Task.CompletedTask;
        }
        
        var updateCounterByElapsedTimeTask = new Task(() =>
        {
            while (true)
            {
                Thread.Sleep((int)(dynamicFleaPrice.GetDecreaseOfPurchasePeriod() * 1000)!);
                try
                {
                    dynamicFleaPrice.DecreaseCounters();
                }
                catch (Exception ex)
                {
                    logger.Warning("error occured while updating flea data: ", ex);
                }
                
            }
        });
        updateCounterByElapsedTimeTask.Start();
        
        return Task.CompletedTask;
    }
}
