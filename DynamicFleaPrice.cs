using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using Path = System.IO.Path;

namespace DynamicFleaNamespace;

[Injectable(InjectionType = InjectionType.Singleton)]
public class DynamicFleaPrice(
    ISptLogger<DynamicFleaPrice> logger,
    DatabaseService databaseService
)
{
    public static string modName = "DynamicFleaPrice";
    private static string _dataPath = @"user\mods\DynamicFleaPrice\Data\DynamicFleaPriceData.json";
    private DynamicFleaPriceData? _data;
    private DynamicFleaPriceConfig? _config;

    /**
     * Get item multiplier ITEM + CATEGORY multiplier
     */
    public double GetItemMultiplier(MongoId template)
    {
        double itemMultiplier = 1;
        double configItemMultiplier = 0;
        MongoId? category = databaseService.GetHandbook().Items
            .Where(handbookItem => handbookItem.Id.Equals(template))
            .Select(handbookItem => handbookItem.ParentId).FirstOrDefault();

        if (_config.IncreaseMultiplierPerItem.TryGetValue(template, out var multiplierPerItem))
        {
            configItemMultiplier = multiplierPerItem;
        }

        if (_data != null && _data.ItemPurchased.TryGetValue(template, out var itemCount))
        {
            itemMultiplier = configItemMultiplier * itemCount ?? 1;
        }

        if (itemMultiplier < 1)
        {
            itemMultiplier += 1;
        }

        var finalMulti = itemMultiplier + GetItemCategoryMultiplier(category);

        if (finalMulti > 1)
        {
            logger.Debug("template=" + template + " x" + finalMulti);
        }

        return finalMulti;
    }

    private double GetItemCategoryMultiplier(MongoId? category)
    {
        if (category == null)
        {
            return 0;
        }

        double categoryMultiplier = 0;
        double configMultiplierCategory = 0;
        if (_config.IncreaseMultiplierPerItemCategory.TryGetValue(category, out var _multiplierPerCategory))
        {
            configMultiplierCategory = _multiplierPerCategory;
        }

        if (_data != null && _data.ItemCategyPurchased.TryGetValue(category, out var _categoryCount))
        {
            categoryMultiplier = (_categoryCount ?? 0) * configMultiplierCategory;
            if (categoryMultiplier > 0)
            {
                logger.Debug("    category=" + category + " x" + categoryMultiplier);
            }
        }

        return categoryMultiplier;
    }

    /**
     * Increase counter for item (item and ctegory counters)
     */
    public void AddItemOrIncreaseCount(MongoId template, int? count)
    {
        AddItemOrIncreaseItemCount(template, count);
        MongoId category = databaseService.GetHandbook().Items
            .Where(handbookItem => handbookItem.Id.Equals(template))
            .Select(handbookItem => handbookItem.ParentId).FirstOrDefault();

        if (category != null!)
        {
            AddItemOrIncreaseItemCategoryCount(category, count);
        }
    }

    private void AddItemOrIncreaseItemCount(MongoId template, int? count)
    {
        if (_data == null)
        {
            return;
        }

        if (!_data.ItemPurchased.ContainsKey(template))
        {
            logger.Debug("added item" + template);
            _data.ItemPurchased.Add(template, count);
        }
        else
        {
            logger.Debug("increase item" + template);
            _data.ItemPurchased[template] += count;
        }
    }

    private void AddItemOrIncreaseItemCategoryCount(MongoId category, int? count)
    {
        if (_data == null)
        {
            return;
        }

        if (!_data.ItemCategyPurchased.ContainsKey(category))
        {
            logger.Debug("added category" + category);
            _data.ItemCategyPurchased.Add(category, count);
        }
        else
        {
            logger.Debug("increase category" + category);
            _data.ItemCategyPurchased[category] += count;
        }
    }

    /**
     * Regenerate/Decreases counters (items, categories) depending on the config
     */
    public void DecreaseCounters()
    {
        if (_data == null || _config == null) return;
        
        _data.ItemPurchased = _data.ItemPurchased.ToDictionary(
            kvp => kvp.Key,
            kvp => PerformCounterValue(kvp.Value ?? 0)
            );

        _data.ItemCategyPurchased = _data.ItemCategyPurchased.ToDictionary(
            kvp => kvp.Key,
            kvp => PerformCounterValue(kvp.Value ?? 0));

        SaveDynamicFleaData();
    }

    private int? PerformCounterValue(int counterValue)
    {
        double percent = 0d;
        if (counterValue > 0)
        {
            if(_config.DecreaseCountersPercentage == 0) return counterValue;
            percent = _config.DecreaseCountersPercentage * 0.01;
        }
        else if(counterValue < 0)
        {
            if(_config.RegenerateCountersPercentage == 0) return counterValue;
            percent = _config.RegenerateCountersPercentage * 0.01;
        }
        else
        {
            return counterValue;
        }

        var changeCounterBy = counterValue * percent; 
        
        // The subtracted value cannot be 0
        if (changeCounterBy is < 0 and > -1)
        {
            changeCounterBy = -1;
        }
        else if(changeCounterBy is > 0 and < 1)
        {
            changeCounterBy = 1;
        }

        return counterValue - (int)changeCounterBy;
    }
    
    /**
     * Save counters data to json file
     */
    public void SaveDynamicFleaData()
    {
        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _dataPath);
            var fileInfo = new FileInfo(dataPath);

            fileInfo.Directory?.Create();

            var jsonString = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dataPath, jsonString);
        }
        catch (Exception ex)
        {
            logger.Error($"{modName}: on save data", ex);
        }
    }

    /**
     * Load counters data from json
     */
    public void LoadDynamicFleaData()
    {
        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _dataPath);

            var jsonContent = File.ReadAllText(dataPath);
            var loadedData = JsonSerializer.Deserialize<DynamicFleaPriceData>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            _data = loadedData;
        }
        catch (Exception ex)
        {
            logger.Warning($"{modName}:  error on load data, USE DEFAULT. " + ex.Message);
            _data = new DynamicFleaPriceData()
            {
                ItemPurchased = new Dictionary<string, int?>(),
                ItemCategyPurchased = new Dictionary<string, int?>(),
            };
        }
    }
    
    /**
     * Load config or create new one with default value
     */
    public void LoadDynamicFleaConfig()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                @"user\mods\DynamicFleaPrice\Config\DynamicFleaPriceConfig.json5");

            if (File.Exists(configPath))
            {
                var jsonContent = File.ReadAllText(configPath);
                var loadedConfig = JsonSerializer.Deserialize<DynamicFleaPriceConfig>(jsonContent,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });

                _config = loadedConfig;
            }
            else
            {
                _config = new DynamicFleaPriceConfig()
                {
                    OnlyFoundInRaidForFleaOffers = true,
                    IncreaseMultiplierPerItem = new Dictionary<string, double>(),
                    IncreaseMultiplierPerItemCategory = new Dictionary<string, double>(),
                    DecreaseCountersPercentage = 1,
                    UpdateCountersPeriod = 600,
                    RegenerateCountersPercentage = 1,
                    IncreaseCounterBuyMultiplier = 1,
                    DecreaseCounterSellMultiplier = 5
                };


                var jsonString = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, jsonString);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"{modName}: error on load config", ex);
            throw;
        }
    }

    public int? GetDecreaseOfPurchasePeriod()
    {
        return _config?.UpdateCountersPeriod;
    }

    public bool GetOnlyFoundInRaidForFleaOffers()
    {
        return (bool)_config?.OnlyFoundInRaidForFleaOffers;
    }
    
    public int GetIncreaseCounterByPurchaseMultiplier()
    {
        return _config?.IncreaseCounterBuyMultiplier ?? 1;
    }
    
    public int GetDecreaseCounterBySellMultiplier()
    {
        return _config?.DecreaseCounterSellMultiplier ?? 1;
    }
    
    
}

public class DynamicFleaPriceData
{
    [JsonPropertyName("itemPurchased")] public required Dictionary<string, int?> ItemPurchased { get; set; }

    [JsonPropertyName("itemCategoryPurchased")]
    public required Dictionary<string, int?> ItemCategyPurchased { get; set; }
}

public class DynamicFleaPriceConfig
{
    [JsonPropertyName("onlyFoundInRaidForFleaOffers")]
    public bool OnlyFoundInRaidForFleaOffers { get; set; }

    [JsonPropertyName("decreaseCountersPercentage")]
    public int DecreaseCountersPercentage { get; set; }
    
    [JsonPropertyName("regenerateCountersPercentage")]
    public int RegenerateCountersPercentage { get; set; }

    [JsonPropertyName("increaseCounterBuyMultiplier")]
    public int IncreaseCounterBuyMultiplier { get; set; }
    
    [JsonPropertyName("decreaseCounterSellMultiplier")]
    public int DecreaseCounterSellMultiplier { get; set; }

    [JsonPropertyName("updateCountersPeriod")]
    public int UpdateCountersPeriod { get; set; }

    [JsonPropertyName("increaseMultiplierPerItem")]
    public Dictionary<string, double> IncreaseMultiplierPerItem { get; set; }

    [JsonPropertyName("increaseMultiplierPerItemCategory")]
    public required Dictionary<string, double> IncreaseMultiplierPerItemCategory { get; set; }
}