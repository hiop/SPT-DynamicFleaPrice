using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace DynamicFleaNamespace;

// There's no need to inject it, as it's used to generate sheets to make searching for identifiers easier
//[Injectable(TypePriority = OnLoadOrder.PostDBModLoader)]
public class DynamicFleaPriceCheatSheetGenerator(
    ISptLogger<DynamicFleaPriceCheatSheetGenerator> logger,
    DatabaseService databaseService,
    LocaleService localeService
    ) : IOnLoad
{
    private Dictionary<string, List<string>> templateDictionary = new();
    public Task OnLoad()
    {
        var locale = localeService.GetLocaleDb("en");
        
        var handBook = databaseService.GetHandbook();
        
        foreach (var pair in databaseService.GetPrices())
        {
            
            var templateId = pair.Key;
            
            HandbookItem? handbookItem = handBook.Items
                .FirstOrDefault(item => item != null && item.Id == templateId, null);

            
            if (handbookItem == null)
            {
                continue;
            }else if(!templateDictionary.ContainsKey(handbookItem.ParentId)){
                templateDictionary.Add(handbookItem.ParentId, new List<string>());
            }
        
            var itemName = locale[templateId + " Name"];
                
            templateDictionary[handbookItem.ParentId].Add(templateId + " | "+itemName);
        }
        
        logger.Success("Category only:");
        foreach (var pair in templateDictionary)
        {
            var categoryId = pair.Key;
            var categoryName = locale[categoryId];
            
            logger.Success(categoryId+" | "+categoryName);
        }
        
        logger.Success("\nCategory and items:");
        foreach (var pair in templateDictionary)
        {
            var categoryId = pair.Key;
            var items = pair.Value;
            
            var categoryName = locale[categoryId];
            
            logger.Success("\n"+categoryId+"\t | "+categoryName);
            
            foreach (var item in items)
            {
                logger.Success("\t"+item);
            }
        }

        return Task.CompletedTask;
    }
}