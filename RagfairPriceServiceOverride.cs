using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
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
    private readonly RagfairConfig RagfairConfig = configServer.GetConfig<RagfairConfig>();

    private double GetTraderPriceIfPriceBeingBelowTraderBuyPrice(Item item, MongoId currency, double itemFleaPrice)
    {
        MongoId template = item.Template;
        
        double newFleaPrice = itemFleaPrice;
        try
        {
            var config = RagfairConfig.Dynamic.GenerateBaseFleaPrices;

            if (config.PreventPriceBeingBelowTraderBuyPrice)
            {
                // Check if item can be sold to trader for a higher price than what we're going to set
                var highestSellToTraderPrice = traderHelper.GetHighestSellToTraderPrice(template);
                        
                // Convert to different currency if required.
                var itemPriceByCurrency = itemFleaPrice;
                if (currency != Money.ROUBLES)
                {
                    itemPriceByCurrency = handbookHelper.FromRoubles(itemFleaPrice, currency);
                }
                
                if (highestSellToTraderPrice > itemPriceByCurrency)
                {
                    // Trader has higher sell price, use that value
                    newFleaPrice = handbookHelper.FromRoubles(highestSellToTraderPrice, currency);
                    // little rondomize price
                    Random random = new Random();
                    double randomPercentage = random.Next(0, 4);
                    newFleaPrice += (newFleaPrice * randomPercentage / 100);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"Fail to check trader price template={template}, error={ex}");
        }
        
        return newFleaPrice;
    }
    
    /**
     * Override.
     * Adds a multiplier to items when generating an offer on the flea
     */
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
            
            double dynamicMulty = GetPriceMultiplier(item.Template);
            

            if (dynamicMulty > 0)
            {
                var itemPrice = (GetDynamicItemPrice(item.Template, desiredCurrency, item, offerItems, isPackOffer) ?? 0) * dynamicMulty;
                itemPrice = GetTraderPriceIfPriceBeingBelowTraderBuyPrice(item, desiredCurrency, itemPrice);
                price += itemPrice;
            }
            else
            {
                var itemPrice = (GetDynamicItemPrice(item.Template, desiredCurrency, item, offerItems, isPackOffer) ?? 0) / Math.Abs(dynamicMulty);
                itemPrice = GetTraderPriceIfPriceBeingBelowTraderBuyPrice(item, desiredCurrency, itemPrice);
                price += itemPrice;
            }
            
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

    private double GetPriceMultiplier(MongoId template)
    {
        double dynamicMulty = dynamicFleaPrice.GetItemMultiplier(template);
        
        if (dynamicMulty is >= 0 and < 1)
        {
            dynamicMulty = 1;
        }
            
        if (dynamicMulty is < 0 and > -1)
        {
            dynamicMulty = -1;
        }
        
        return dynamicMulty;
    }
}