using System;
using System.Collections.Generic;

namespace cycloid.Info;

public record struct InfoPoint(MapPoint Location, string Name, InfoCategory Category, InfoType Type)
{
    public static InfoPoint FromOverpassPoint(OverpassPoint overpass)
    {
        MapPoint location;
        if (overpass.bounds is OverpassBounds bounds)
        {
            location = new MapPoint((bounds.minlat + bounds.maxlat) / 2, (bounds.minlon + bounds.maxlon) / 2);
        }
        else
        {
            location = new MapPoint(overpass.lat, overpass.lon);
        }

        string name = overpass.tags.name ?? "";
        if (!string.IsNullOrEmpty(overpass.tags.ele))
        {
            name = $"{name}{(name.Length > 0 ? " " : "")}({overpass.tags.ele})";
        }

        (InfoCategory category, InfoType type) = ((overpass.tags.mountain_pass, overpass.tags.amenity, overpass.tags.shop)) switch
        {
            (OverpassBool.yes, _, _) => (InfoCategory.Section, InfoType.MountainPass),
            (_, _, OverpassShops.bakery) => (InfoCategory.Shop, InfoType.Bakery),
            (_, _, OverpassShops.pastry) => (InfoCategory.Shop, InfoType.Bakery),
            (_, _, OverpassShops.food) => (InfoCategory.Shop, InfoType.Supermarket),
            (_, _, OverpassShops.greengrocer) => (InfoCategory.Shop, InfoType.Supermarket),
            (_, _, OverpassShops.health_food) => (InfoCategory.Shop, InfoType.Supermarket),
            (_, _, OverpassShops.supermarket) => (InfoCategory.Shop, InfoType.Supermarket),
            (_, OverpassAmenities.drinking_water, _) => (InfoCategory.Water, InfoType.Water),
            (_, OverpassAmenities.toilets, _) => (InfoCategory.Water, InfoType.Toilet),
            (_, OverpassAmenities.fuel, _) => (InfoCategory.Shop, InfoType.FuelStation),
            (_, OverpassAmenities.fast_food, _) => (InfoCategory.Food, InfoType.FastFood),
            (_, OverpassAmenities.ice_cream, _) => (InfoCategory.Food, InfoType.FastFood),
            (_, OverpassAmenities.cafe, _) => (InfoCategory.Food, InfoType.Bar),
            (_, OverpassAmenities.bar, _) => (InfoCategory.Food, InfoType.Bar),
            (_, OverpassAmenities.pub, _) => (InfoCategory.Food, InfoType.Bar),
            (_, OverpassAmenities.restaurant, _) => (InfoCategory.Food, InfoType.Restaurant),
            (_, _, _) => throw new InvalidOperationException()
        };

        return new InfoPoint(location, name, category, type);
    }
}

public class InfoCategory
{
    public static readonly InfoCategory Section = new() { Hide = true, Types = [InfoType.MountainPass, InfoType.Split, InfoType.Goal] };
    public static readonly InfoCategory Water = new() { Name = "Water", Types = [InfoType.Water, InfoType.Toilet] };
    public static readonly InfoCategory Food = new() { Name = "Food", Types = [InfoType.FastFood, InfoType.Bar, InfoType.Restaurant] };
    public static readonly InfoCategory Shop = new() { Name = "Shop", Types = [InfoType.Supermarket, InfoType.Bakery, InfoType.FuelStation] };
    public static readonly InfoCategory Sleep = new() { Name = "Sleep", Types = [] };

    public static readonly IEnumerable<InfoCategory> All = [Section, Water, Food, Shop, Sleep];

    public bool Hide { get; init; }
    public string Name { get; init; }
    public InfoType[] Types { get; init; }
    
    private InfoCategory()
    { }
}

public enum InfoType
{
    // Section
    MountainPass,
    Split,
    Goal,
    // Water
    Water,
    Toilet,
    // Food
    Restaurant,
    FastFood,
    Bar,
    // Shop
    Supermarket,
    Bakery,
    FuelStation,
}
