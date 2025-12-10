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

        UpdateCounterByElapsedTime();
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

    public void UpdateCounterByElapsedTime()
    {
        if (_data == null) return;
        
        _data.ItemPurchased = _data.ItemPurchased.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var decreasePercent = _config.DecreaseOfPurchasePercentage * 0.01;
                var decreaseCountBy = (int)((kvp.Value ?? 0) * decreasePercent);

                // if decreaseCountBy is lower by 1, then just subtract from result 1;
                if (decreaseCountBy <= 0)
                {
                    decreaseCountBy = 1;
                }

                var result = kvp.Value - decreaseCountBy;
                return result < 0 ? 0 : result;
            });

        _data.ItemCategyPurchased = _data.ItemCategyPurchased.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var decreasePercent = _config.DecreaseOfPurchasePercentage * 0.01;
                var decreaseCountBy = (int)((kvp.Value ?? 0) * decreasePercent);

                // if decreaseCountBy is lower by 1, then just subtract from result 1;
                if (decreaseCountBy <= 0)
                {
                    decreaseCountBy = 1;
                }

                var result = kvp.Value - decreaseCountBy;
                return result < 0 ? 0 : result;
            });

        SaveDynamicFleaData();
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
            logger.Error("on save data", ex);
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
            logger.Warning("error on load data, set default: " + ex.Message);
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
                    DecreaseOfPurchasePercentage = 1,
                    DecreaseOfPurchasePeriod = 600,
                };


                var jsonString = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, jsonString);
            }
        }
        catch (Exception ex)
        {
            logger.Error("on load config", ex);
            _config = new DynamicFleaPriceConfig()
            {
                IncreaseMultiplierPerItem = new Dictionary<string, double>(),
                IncreaseMultiplierPerItemCategory = new Dictionary<string, double>()
            };
        }
    }

    public int? GetDecreaseOfPurchasePeriod()
    {
        return _config?.DecreaseOfPurchasePeriod;
    }

    public bool GetOnlyFoundInRaidForFleaOffers()
    {
        return (bool)_config?.OnlyFoundInRaidForFleaOffers;
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

    [JsonPropertyName("decreaseOfPurchasePercentage")]
    public int DecreaseOfPurchasePercentage { get; set; }

    [JsonPropertyName("decreaseOfPurchasePeriod")]
    public int DecreaseOfPurchasePeriod { get; set; }

    [JsonPropertyName("increaseMultiplierPerItem")]
    public Dictionary<string, double> IncreaseMultiplierPerItem { get; set; }

    [JsonPropertyName("increaseMultiplierPerItemCategory")]
    public required Dictionary<string, double> IncreaseMultiplierPerItemCategory { get; set; }
}