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
    public static readonly string ModName = "DynamicFleaPrice";
    private static readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user", "mods", "DynamicFleaPrice", "Data", "DynamicFleaPriceData.json");
    private static readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user", "mods", "DynamicFleaPrice", "Config", "DynamicFleaPriceConfig.json5");
    
    private DynamicFleaPriceData? _data;
    private DynamicFleaPriceConfig? _config;

    /**
     * Get item multiplier ITEM + CATEGORY
     */
    public double GetItemMultiplier(MongoId template)
    {
        double itemMultiplier = 1;
        MongoId? category = databaseService.GetHandbook().Items
            .Where(handbookItem => handbookItem.Id.Equals(template))
            .Select(handbookItem => handbookItem.ParentId).FirstOrDefault();

        if (_data != null && _data.ItemMultiplier.TryGetValue(template, out var dataItemMultiplier))
        {
            itemMultiplier = dataItemMultiplier;
        }
        var finalMulti = itemMultiplier + GetItemCategoryMultiplier(category);

        return finalMulti;
    }

    private double GetItemCategoryMultiplier(MongoId? category)
    {
        if (category == null)
        {
            return 0;
        }

        double categoryMultiplier = 0;
        
        if (_data != null && _data.ItemCategoryMultiplier.TryGetValue(category, out var dataCategoryMultiplier))
        {
            categoryMultiplier = dataCategoryMultiplier;
            if (categoryMultiplier > 0)
            {
                logger.Debug("    category=" + category + " x" + categoryMultiplier);
            }
        }

        return categoryMultiplier;
    }

    /**
     * Increase multiplier for item (item and category)
     */
    public void AddOrIncreaseMultiplier(MongoId template, int? count)
    {
        AddOrIncreaseItemMultiplier(template, count);
        MongoId category = databaseService.GetHandbook().Items
            .Where(handbookItem => handbookItem.Id.Equals(template))
            .Select(handbookItem => handbookItem.ParentId).FirstOrDefault();

        if (category != null!)
        {
            AddOrIncreaseItemCategoryMultiplier(category, count);
        }
    }

    private void AddOrIncreaseItemMultiplier(MongoId template, int? count)
    {
        if (_data == null || _config == null)
        {
            return;
        }
        
        _config.IncreaseMultiplierPerItem.TryGetValue(template, out var multiplierPerItem);
        if(multiplierPerItem <= 0) return;
        
        if (!_data.ItemMultiplier.ContainsKey(template))
        {
            logger.Debug("added item multiplier" + template);
            _data.ItemMultiplier.Add(template, count * multiplierPerItem ?? 0);
        }
        else
        {
            logger.Debug("increase item multiplier" + template);
            _data.ItemMultiplier[template] += count * multiplierPerItem ?? 0;
        }
    }

    private void AddOrIncreaseItemCategoryMultiplier(MongoId category, int? count)
    {
        if (_data == null || _config == null)
        {
            return;
        }
        
        _config.IncreaseMultiplierPerItemCategory.TryGetValue(category, out var multiplierPerCategory);
        if(multiplierPerCategory <= 0) return;

        if (!_data.ItemCategoryMultiplier.ContainsKey(category))
        {
            logger.Debug("added category multiplier" + category);
            _data.ItemCategoryMultiplier.Add(category, count * multiplierPerCategory ?? 0);
        }
        else
        {
            logger.Debug("increase category multiplier" + category);
            _data.ItemCategoryMultiplier[category] += count * multiplierPerCategory ?? 0;
        }
    }

    /**
     * Regenerate/Decreases multiplier (items, categories) depending on the config
     */
    public void UpdateMultiplier()
    {
        if (_data == null || _config == null) return;
        
        _data.ItemMultiplier = _data.ItemMultiplier.ToDictionary(
            kvp => kvp.Key,
            kvp => PerformMultiplierValue(kvp.Value) ?? 0
            );

        _data.ItemCategoryMultiplier = _data.ItemCategoryMultiplier.ToDictionary(
            kvp => kvp.Key,
            kvp => PerformMultiplierValue(kvp.Value) ?? 0);

        SaveDynamicFleaData();
    }
    
    private double? PerformMultiplierValue(double multiplier)
    {
        double percent = 0d;

        if (multiplier > 0)
        {
            if(_config.DecreaseMultiplierPercentage == 0) return multiplier;
            percent = _config.DecreaseMultiplierPercentage * 0.01;
        }
        else if(multiplier < 0)
        {
            if(_config.RegenerateMultiplierPercentage == 0) return multiplier;
            percent = _config.RegenerateMultiplierPercentage * 0.01;
        }
        else
        {
            return multiplier;
        }

        var changeCounterBy = multiplier * percent; 

        return multiplier - changeCounterBy;
    }
    
    /**
     * Save multiplier data to json file
     */
    public void SaveDynamicFleaData()
    {
        try
        {
            var fileInfo = new FileInfo(_dataPath);

            fileInfo.Directory?.Create();

            var jsonString = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataPath, jsonString);
        }
        catch (Exception ex)
        {
            logger.Error($"{ModName}: on save data", ex);
        }
    }

    /**
     * Load counters data from json
     */
    public void LoadDynamicFleaData()
    {
        try
        {
            var jsonContent = File.ReadAllText(_dataPath);
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
            if (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                logger.Warning($"{ModName}: data not found, generation of new");
                _data = new DynamicFleaPriceData()
                {
                    ItemCategoryMultiplier = new Dictionary<string, double>(),
                    ItemMultiplier = new Dictionary<string, double>(),
                };

                SaveDynamicFleaData();
            }
            else
            {
                logger.Error($"{ModName}:  error on load data," + ex.Message);
                throw;
            }
        }
    }
    
    /**
     * Load config or create new one with default value
     */
    public void LoadDynamicFleaConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var jsonContent = File.ReadAllText(_configPath);
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
                    OnlyFoundInRaidForFleaOffers = false,
                    IncreaseMultiplierPerItem = new Dictionary<string, double>(),
                    IncreaseMultiplierPerItemCategory = new Dictionary<string, double>(),
                    DecreaseMultiplierPercentage = 1,
                    UpdatePeriod = 600,
                    RegenerateMultiplierPercentage = 1,
                    MoreMultiplierPerBuying = 1,
                    MoreMultiplierPerSelling = 2
                };
                
                var fileInfo = new FileInfo(_configPath);
                fileInfo.Directory?.Create();

                var jsonString = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, jsonString);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"{ModName}: error on load config", ex);
            throw;
        }
    }

    public int? GetDecreaseOfPurchasePeriod()
    {
        return _config?.UpdatePeriod;
    }

    public int GetIncreaseCounterByPurchaseMultiplier()
    {
        return _config?.MoreMultiplierPerBuying ?? 1;
    }
    
    public int GetDecreaseCounterBySellMultiplier()
    {
        return _config?.MoreMultiplierPerSelling ?? 1;
    }
    
    public bool GetOnlyFoundInRaidForFleaOffers()
    {
        return (bool)_config?.OnlyFoundInRaidForFleaOffers;
    }
}

public class DynamicFleaPriceData
{
    [JsonPropertyName("itemMultiplier")] 
    public Dictionary<string, double> ItemMultiplier { get; set; } = null!;

    [JsonPropertyName("itemCategoryMultiplier")]
    public Dictionary<string, double> ItemCategoryMultiplier { get; set; } = null!;
}

public class DynamicFleaPriceConfig
{
    [JsonPropertyName("onlyFoundInRaidForFleaOffers")]
    public bool OnlyFoundInRaidForFleaOffers { get; set; } = true;
    
    [JsonPropertyName("decreaseMultiplierPercentage")]
    public int DecreaseMultiplierPercentage { get; set; }
    
    [JsonPropertyName("regenerateMultiplierPercentage")]
    public int RegenerateMultiplierPercentage { get; set; }

    [JsonPropertyName("moreMultiplierPerBuying")]
    public int MoreMultiplierPerBuying { get; set; }
    
    [JsonPropertyName("moreMultiplierPerSelling")]
    public int MoreMultiplierPerSelling { get; set; }

    [JsonPropertyName("updatePeriod")]
    public int UpdatePeriod { get; set; }

    [JsonPropertyName("increaseMultiplierPerItem")]
    public Dictionary<string, double> IncreaseMultiplierPerItem { get; set; }

    [JsonPropertyName("increaseMultiplierPerItemCategory")]
    public required Dictionary<string, double> IncreaseMultiplierPerItemCategory { get; set; }
}