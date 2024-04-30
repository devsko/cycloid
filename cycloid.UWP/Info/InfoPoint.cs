using System;
using System.Collections.Generic;
using System.Linq;

namespace cycloid.Info;

public record InfoPoint(MapPoint Location, string Name, InfoCategory Category, InfoType Type)
{
    public static readonly InfoPoint Invalid = new(MapPoint.Invalid, default, default, default);

    public static InfoPoint FromOverpassPoint(OverpassPoint overpass)
    {
        MapPoint location;
        if (overpass.Bounds is OverpassBounds bounds)
        {
            location = new MapPoint((bounds.Minlat + bounds.Maxlat) / 2, (bounds.Minlon + bounds.Maxlon) / 2);
        }
        else
        {
            location = new MapPoint(overpass.Lat, overpass.Lon);
        }

        string name = overpass.Tags.Name ?? "";
        if (!string.IsNullOrEmpty(overpass.Tags.Ele))
        {
            name = $"{name}{(name.Length > 0 ? " " : "")}({overpass.Tags.Ele})";
        }

        (InfoCategory category, InfoType type) = ((overpass.Tags.MountainPass, overpass.Tags.Amenity, overpass.Tags.Shop)) switch
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

    public bool IsValid => Location.IsValid;
}

public class InfoCategory
{
    public static readonly InfoCategory Section = new() { Hide = true, Name = "Section", Types = [InfoType.MountainPass, InfoType.Split, InfoType.Goal] };
    public static readonly InfoCategory Water = new() { Name = "Water", Types = [InfoType.Water, InfoType.Toilet] };
    public static readonly InfoCategory Food = new() { Name = "Food", Types = [InfoType.FastFood, InfoType.Bar, InfoType.Restaurant] };
    public static readonly InfoCategory Shop = new() { Name = "Shop", Types = [InfoType.Supermarket, InfoType.Bakery, InfoType.FuelStation] };
    public static readonly InfoCategory Sleep = new() { Name = "Sleep", Types = [InfoType.Bed, InfoType.Outdoor, InfoType.Roof] };

    public static readonly IEnumerable<InfoCategory> All = [Section, Water, Food, Shop, Sleep];

    public static InfoCategory Get(InfoType type) => All.First(category => category.Types.Contains(type));

    public bool Hide { get; init; }
    public string Name { get; init; }
    public InfoType[] Types { get; init; }
    
    private InfoCategory()
    { }
}

public enum InfoType
{
    // Section
    Goal,
    MountainPass,
    Split,
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
    // Sleep
    Bed,
    Outdoor,
    Roof,
}
