using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace DynamicFleaNamespace;

[Injectable]
public class RagfairControllerOverride(
    ISptLogger<RagfairController> logger,
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
    ConfigServer configServer,
    DynamicFleaPrice dynamicFleaPrice
    ) : RagfairController(logger, timeUtil, jsonUtil, httpResponseUtil, eventOutputHolder, ragfairServer, itemHelper, inventoryHelper, ragfairSellHelper, handbookHelper, profileHelper, paymentHelper, ragfairHelper, ragfairSortHelper, ragfairOfferHelper, traderHelper, databaseService, localisationService, ragfairTaxService, ragfairOfferService, paymentService, ragfairPriceService, ragfairOfferGenerator, configServer)
{
    /**
     * Override.
     * Check FIR offer to flea from player
     */
    public override ItemEventRouterResponse AddPlayerOffer(
        PmcData pmcData,
        AddOfferRequestData offerRequest,
        MongoId sessionID
    )
    {
        
        var inventoryItemsToSell = GetItemsToListOnFleaFromInventory(pmcData, offerRequest.Items);
        
        foreach (var items in inventoryItemsToSell.Items ?? [])
        {
            foreach (var item in items)
            {
                if (item.Upd == null || item.Upd.SpawnedInSession.Equals(null) || item.Upd.SpawnedInSession.Equals(false))
                {
                    if (dynamicFleaPrice.GetOnlyFoundInRaidForFleaOffers())
                    {
                        // This may not return any errors to the client, but at least I'm not returning null now.
                        var warning = new Warning
                        {
                            ErrorMessage = "Only FIR items available on flea",
                            Index = 0,
                            Code = BackendErrorCodes.RagfairUnavailable
                        };
                        return new ItemEventRouterResponse()
                        {
                            Warnings = [warning]
                        };
                    }
                }
            }
        }


        return base.AddPlayerOffer(pmcData, offerRequest, sessionID);
    }

    public override void Update()
    {
        foreach (var (sessionId, profile) in profileHelper.GetProfiles())
        {
            // Check profile is capable of creating offers
            var pmcProfile = profile?.CharacterData?.PmcData;
            if (
                pmcProfile?.RagfairInfo is not null
                && pmcProfile?.Info?.Level >= databaseService.GetGlobals().Configuration.RagFair.MinUserLevel
            )
            {
                ragfairOfferHelper.ProcessOffersOnProfile(sessionId);
            }
        };
    }
}