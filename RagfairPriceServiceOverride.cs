using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace DynamicFleaNamespace;

[Injectable(InjectionType.Singleton)]
public class RagfairPriceServiceOverride(    
    ISptLogger<RagfairPriceService> logger,
    RandomUtil randomUtil,
    HandbookHelper handbookHelper,
    TraderHelper traderHelper,
    PresetHelper presetHelper,
    ItemHelper itemHelper,
    DatabaseService databaseService,
    DatabaseServer databaseServer,
    ServerLocalisationService serverLocalisationService,
    ConfigServer configServer,
    DynamicFleaPrice dynamicFleaPrice
    ): RagfairPriceService(
    logger, 
    randomUtil, 
    handbookHelper, 
    traderHelper, 
    presetHelper,
    itemHelper,
    databaseService,
    databaseServer, 
    serverLocalisationService, 
    configServer)
{
    public override double GetDynamicOfferPriceForOffer(IEnumerable<Item> offerItems, MongoId desiredCurrency, bool isPackOffer)
    {
        // Price to return.
        var price = 0d;

        // Iterate over each item in the offer.
        foreach (var item in offerItems)
        {
            // Skip over armor inserts as those are not factored into item prices.
            if (itemHelper.IsOfBaseclass(item.Template, BaseClasses.BUILT_IN_INSERTS))
            {
                continue;
            }
            
            double dynamicMulty = dynamicFleaPrice.GetItemMultiplier(item.Template);
            if (dynamicMulty < 1)
            {
                dynamicMulty = 1;
            }
            
            price += (GetDynamicItemPrice(item.Template, desiredCurrency, item, offerItems, isPackOffer) ?? 0) * dynamicMulty;

            // Check if the item is a weapon preset.
            if (item?.Upd?.SptPresetId is not null && presetHelper.IsPresetBaseClass(item.Upd.SptPresetId.Value, BaseClasses.WEAPON))
                // This is a weapon preset, which has its own price calculation that takes into account the mods in the
                // preset. Since we've already calculated the price for the preset entire preset in
                // `getDynamicItemPrice`, we can skip the rest of the items in the offer.
            {
                break;
            }
        }

        return Math.Round(price);
    }
}