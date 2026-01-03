using System.Text;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace DynamicFleaNamespace;

// There's no need to inject it, as it's used to generate sheets to make searching for identifiers easier
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader)]
public class DynamicFleaPriceCheatSheetGenerator(
    ISptLogger<DynamicFleaPriceCheatSheetGenerator> logger,
    DatabaseService databaseService,
    LocaleService localeService,
    DynamicFleaPrice dynamicFleaPrice
    ) : IOnLoad
{
    private Dictionary<string, List<string>> templateDictionary = new();
    public Task OnLoad()
    {
        if (dynamicFleaPrice.GetItemsAndCategoriesExport() == false)
        {
            return Task.CompletedTask;
        }
        
        var locale = localeService.GetLocaleDb(dynamicFleaPrice.GetItemsAndCategoriesExportLocale());
        var handBook = databaseService.GetHandbook();
        StringBuilder itemStringBuilder = new();
        
        foreach (var pair in databaseService.GetPrices())
        {
            
            var templateId = pair.Key;
            
            HandbookItem? handbookItem = handBook.Items
                .FirstOrDefault(item => item != null && item.Id == templateId, null);

            
            if (handbookItem == null)
            {
                var errorItemName = "???";
                try
                {
                    errorItemName = locale[templateId + " Name"];
                }
                catch (Exception ex)
                {
                    //
                }
                logger.Error($"Item error: {templateId} | {errorItemName}");

                continue;
            } else if(!templateDictionary.ContainsKey(handbookItem.ParentId)){
                templateDictionary.Add(handbookItem.ParentId, new List<string>());
            }
        
            var itemName = locale[templateId + " Name"];
                
            templateDictionary[handbookItem.ParentId].Add("\""+templateId + "\": 0, // "+itemName);
        }
        
        //logger.Success("Category only:");
        itemStringBuilder.AppendLine("Category only:");
        foreach (var pair in templateDictionary)
        {
            var categoryId = pair.Key;
            var categoryName = locale[categoryId];
            
            //logger.Success(categoryId+" | "+categoryName);
            itemStringBuilder.AppendLine("\""+categoryId+"\": 0, // "+categoryName);
        }
        
        //logger.Success("\nCategory and items:");
        itemStringBuilder.AppendLine("\nCategory and items:");
        foreach (var pair in templateDictionary)
        {
            var categoryId = pair.Key;
            var items = pair.Value;
            
            var categoryName = locale[categoryId];
            
            //logger.Success("\n"+categoryId+"\t | "+categoryName);
            itemStringBuilder.AppendLine("\n//"+categoryId+"\t   "+categoryName);
            
            foreach (var item in items)
            {
                //logger.Success("\t"+item);
                itemStringBuilder.AppendLine("\t"+item);
            }
        }
        
        var fileInfo = new FileInfo(DynamicFleaPrice.exportPath);
        fileInfo.Directory?.Create();
        File.WriteAllText(DynamicFleaPrice.exportPath, itemStringBuilder.ToString(), Encoding.UTF8);
        
        logger.Success($"[{DynamicFleaPrice.ModName}] Items/Categories IDs exported: {fileInfo.Name}");

        return Task.CompletedTask;
    }
}