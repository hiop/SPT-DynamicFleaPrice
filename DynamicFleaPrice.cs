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
    private static long _elapsedTime;
    private DynamicFleaData? _data;
    private DynamicFleaConfig? _config;

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

    public void AddItemOrIncreaseItemCount(MongoId template, int? count)
    {
        if (_data == null)
        {
            logger.Error("flea dynamic data is not init");
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
            logger.Error("flea dynamic data is not init");
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
        if (!(_elapsedTime >= _config.DecreaseOfPurchaseInSeconds * 1000)) return;


        double degradationTimes =
            Math.Floor((double)_elapsedTime / (_config.DecreaseOfPurchaseInSeconds * 1000));
        logger.Debug("decrease time! x" + degradationTimes);

        _data.ItemPurchased = _data.ItemPurchased.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var result = kvp.Value -
                             (int)((kvp.Value ?? 0) *
                                   (_config.DecreaseOfPurchasePercentage * 0.01 * degradationTimes));

                return result < 0 ? 0 : result;
            });

        _data.ItemCategyPurchased = _data.ItemCategyPurchased.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var result = kvp.Value -
                             (int)((kvp.Value ?? 0) *
                                   (_config.DecreaseOfPurchasePercentage * 0.01 * degradationTimes));
                return result < 0 ? 0 : result;
            });

        _elapsedTime = 0L;
    }
    public void UpdateLastPurchasedDate()
    {
        if (_data == null) return;

        _elapsedTime += DateTimeOffset.Now.ToUnixTimeMilliseconds() - _data.LastFleaPurchasedIsMs;
        _data.LastFleaPurchasedIsMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        logger.Debug("UpdateLastPurchasedDate " + _data.LastFleaPurchasedIsMs);
        logger.Debug("_elapsedTime " + _elapsedTime);
    }

    public void SaveDynamicFleaData()
    {
        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                @"user\mods\DynamicFleaPrice\Data\DynamicFleaData.json");
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

    public void LoadDynamicFleaData()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                @"user\mods\DynamicFleaPrice\Data\DynamicFleaData.json");
            var jsonContent = File.ReadAllText(configPath);
            var loadedData = JsonSerializer.Deserialize<DynamicFleaData>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            _data = loadedData;
        }
        catch
        {
            logger.Warning("on load data, set default");
            _data = new DynamicFleaData()
            {
                LastFleaPurchasedIsMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                ItemPurchased = new Dictionary<string, int?>(),
                ItemCategyPurchased = new Dictionary<string, int?>(),
            };
        }
        
        _elapsedTime = _data == null 
            ? 0 
            : DateTimeOffset.Now.ToUnixTimeMilliseconds() - _data.LastFleaPurchasedIsMs;
    }

    public void LoadDynamicFleaConfig()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                @"user\mods\DynamicFleaPrice\Config\DynamicFleaConfig.json");
            var jsonContent = File.ReadAllText(configPath);
            var loadedConfig = JsonSerializer.Deserialize<DynamicFleaConfig>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            _config = loadedConfig;
        }
        catch (Exception ex)
        {
            logger.Error("on load config", ex);
            _config = new DynamicFleaConfig()
            {
                IncreaseMultiplierPerItem = new Dictionary<string, double>(),
                IncreaseMultiplierPerItemCategory = new Dictionary<string, double>()
            };
        }
    }
}

public class DynamicFleaData
{
    [JsonPropertyName("lastFleaPurchasedIsMs")]
    public long LastFleaPurchasedIsMs { get; set; }

    [JsonPropertyName("itemPurchased")] public required Dictionary<string, int?> ItemPurchased { get; set; }

    [JsonPropertyName("itemCategoryPurchased")]
    public required Dictionary<string, int?> ItemCategyPurchased { get; set; }
}

public class DynamicFleaConfig
{
    [JsonPropertyName("decreaseOfPurchasePercentage")]
    public int DecreaseOfPurchasePercentage { get; set; }

    [JsonPropertyName("decreaseOfPurchaseInSeconds")]
    public int DecreaseOfPurchaseInSeconds { get; set; }

    [JsonPropertyName("increaseMultiplierPerItem")]
    public Dictionary<string, double> IncreaseMultiplierPerItem { get; set; }

    [JsonPropertyName("increaseMultiplierPerItemCategory")]
    public required Dictionary<string, double> IncreaseMultiplierPerItemCategory { get; set; }
}