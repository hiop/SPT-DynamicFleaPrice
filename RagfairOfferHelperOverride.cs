using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DynamicFleaNamespace;

[Injectable]
public class RagfairOfferHelperOverride(
    ISptLogger<RagfairOfferHelper> logger,
    TimeUtil timeUtil,
    BotHelper botHelper,
    RagfairSortHelper ragfairSortHelper,
    PresetHelper presetHelper,
    RagfairHelper ragfairHelper,
    PaymentHelper paymentHelper,
    TraderHelper traderHelper,
    QuestHelper questHelper,
    RagfairServerHelper ragfairServerHelper,
    ItemHelper itemHelper,
    DatabaseService databaseService,
    RagfairOfferService ragfairOfferService,
    LocaleService localeService,
    ServerLocalisationService serverLocalisationService,
    MailSendService mailSendService,
    RagfairRequiredItemsService ragfairRequiredItemsService,
    ProfileHelper profileHelper,
    EventOutputHolder eventOutputHolder,
    ConfigServer configServer,
    ICloner cloner,
    DynamicFleaPrice dynamicFleaPrice
) : RagfairOfferHelper(logger, timeUtil, botHelper, ragfairSortHelper, presetHelper, ragfairHelper, paymentHelper,
    traderHelper, questHelper, ragfairServerHelper, itemHelper, databaseService, ragfairOfferService, localeService,
    serverLocalisationService, mailSendService, ragfairRequiredItemsService, profileHelper, eventOutputHolder,
    configServer, cloner
)
{
    /**
     * After complete offer need to reduce multiplier
     */
    public override ItemEventRouterResponse CompleteOffer(MongoId offerOwnerSessionId, RagfairOffer offer, int boughtAmount)
    {
        var response = base.CompleteOffer(offerOwnerSessionId, offer, boughtAmount);
        
        foreach (var offerItem in offer?.Items ?? [])
        {
            var count = offerItem?.Upd?.StackObjectsCount ?? 1;
            var multiplier = dynamicFleaPrice.GetDecreaseCounterBySellMultiplier();
            
            dynamicFleaPrice.AddOrIncreaseMultiplier(
                offerItem?.Template ?? null!, 
                -(int)(count * multiplier)
            );
        }
        dynamicFleaPrice.SaveDynamicFleaData();
        
        return response;
    }

    public override bool ProcessOffersOnProfile(MongoId sessionId)
    {
        var currentTimestamp = timeUtil.GetTimeStamp();
        var profileOffers = GetProfileOffers(sessionId);

        // No offers, don't do anything
        if (!profileOffers.Any())
        {
            return true;
        }

        // Index backwards as CompleteOffer() can delete offer object
        for (var index = profileOffers.Count - 1; index >= 0; index--)
        {
            var offer = profileOffers[index];
            if (currentTimestamp > offer.EndTime)
            {
                // Offer has expired before selling, skip as it will be processed in RemoveExpiredOffers()
                continue;
            }

            if (offer.SellResults is null || !offer.SellResults.Any() || currentTimestamp < offer.SellResults.FirstOrDefault()?.SellTime)
            {
                // Not sold / too early to check
                continue;
            }

            var firstSellResult = offer.SellResults?.FirstOrDefault();
            if (firstSellResult is null)
            {
                continue;
            }

            // Checks first item, first is spliced out of array after being processed
            // Item sold
            var totalItemsCount = 1d;
            var boughtAmount = 1;

            // Does item need to be re-stacked
            if (!offer.SellInOnePiece.GetValueOrDefault(false))
            {
                // offer.items.reduce((sum, item) => sum + item.upd?.StackObjectsCount ?? 0, 0);
                totalItemsCount = GetTotalStackCountSize([offer.Items]);
                boughtAmount = firstSellResult.Amount ?? boughtAmount;
            }

            var ratingToAdd = offer.SummaryCost / totalItemsCount * boughtAmount;
            IncreaseProfileRagfairRating(profileHelper.GetFullProfile(sessionId), ratingToAdd.Value);

            // Remove the sell result object now it has been processed
            offer.SellResults.Remove(firstSellResult);

            // Can delete offer object, must run last
            CompleteOffer(sessionId, offer, boughtAmount);
        }

        return true;
    }
}