using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace DynamicFleaNamespace;

[Injectable]
public class RagfairControllerOverride(ISptLogger<RagfairController> logger,
    TimeUtil timeUtil,
    JsonUtil jsonUtil,
    HttpResponseUtil httpResponseUtil,
    EventOutputHolder eventOutputHolder,
    RagfairServer ragfairServer,
    ItemHelper itemHelper,
    InventoryHelper inventoryHelper,
    RagfairSellHelper ragfairSellHelper,
    HandbookHelper handbookHelper,
    ProfileHelper profileHelper,
    PaymentHelper paymentHelper,
    RagfairHelper ragfairHelper,
    RagfairSortHelper ragfairSortHelper,
    RagfairOfferHelper ragfairOfferHelper,
    TraderHelper traderHelper,
    DatabaseService databaseService,
    ServerLocalisationService localisationService,
    RagfairTaxService ragfairTaxService,
    RagfairOfferService ragfairOfferService,
    PaymentService paymentService,
    RagfairPriceService ragfairPriceService,
    RagfairOfferGenerator ragfairOfferGenerator,
    ConfigServer configServer
    ) : RagfairController(logger, timeUtil, jsonUtil, httpResponseUtil, eventOutputHolder, ragfairServer, itemHelper, inventoryHelper, ragfairSellHelper, handbookHelper, profileHelper, paymentHelper, ragfairHelper, ragfairSortHelper, ragfairOfferHelper, traderHelper, databaseService, localisationService, ragfairTaxService, ragfairOfferService, paymentService, ragfairPriceService, ragfairOfferGenerator, configServer)
{
    public override ItemEventRouterResponse AddPlayerOffer(PmcData pmcData, AddOfferRequestData offerRequest, MongoId sessionID)
    {
        
        var inventoryItemsToSell = GetItemsToListOnFleaFromInventory(pmcData, offerRequest.Items);
        foreach (var items in inventoryItemsToSell.Items)
        {
            foreach (var item in items)
            {
                if (item.Upd.SpawnedInSession.Equals(false))
                {
                    var output = eventOutputHolder.GetOutput(sessionID);
                    eventOutputHolder.GetOutput(sessionID);
                    
                    //return null;
                }
            }
        }
        
        return base.AddPlayerOffer(pmcData, offerRequest, sessionID);
    }
}