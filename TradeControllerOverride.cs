using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace DynamicFleaNamespace;

[Injectable]
public class TradeControllerOverride(
    ISptLogger<TradeController> logger,
    DatabaseService databaseService,
    EventOutputHolder eventOutputHolder,
    TradeHelper tradeHelper,
    TimeUtil timeUtil,
    RandomUtil randomUtil,
    ItemHelper itemHelper,
    RagfairOfferHelper ragfairOfferHelper,
    RagfairServer ragfairServer,
    HttpResponseUtil httpResponseUtil,
    ServerLocalisationService serverLocalisationService,
    MailSendService mailSendService,
    ConfigServer configServer,
    DynamicFleaPrice dynamicFleaPrice
    ) : TradeController(logger, databaseService, eventOutputHolder, tradeHelper, timeUtil, randomUtil, itemHelper, ragfairOfferHelper, ragfairServer, httpResponseUtil, serverLocalisationService, mailSendService, configServer){
    
    
    public override ItemEventRouterResponse ConfirmRagfairTrading(PmcData pmcData, ProcessRagfairTradeRequestData request,
        MongoId sessionID)
    {
        dynamicFleaPrice.UpdateLastPurchasedDate();
        
        foreach (var requestOffer in request.Offers)
        {
            if(requestOffer.Id == null) continue;
            
            var offerItem = ragfairServer.GetOffer(requestOffer.Id);
            
            if(offerItem == null || offerItem.Items == null) continue;
                
            foreach (var offerItemItem in offerItem.Items)
            {
                    dynamicFleaPrice.AddItemOrIncreaseCount(offerItemItem.Template, requestOffer.Count);
            }
        }
        
        var response = base.ConfirmRagfairTrading(pmcData, request, sessionID);
        dynamicFleaPrice.SaveDynamicFleaData();
        return response;
    }
}