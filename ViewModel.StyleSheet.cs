using Windows.UI.Xaml.Controls.Maps;

namespace cycloid;

partial class ViewModel
{
    private static class StyleSheet
    {
        public static MapStyleSheet Extension { get; } = MapStyleSheet.ParseFromJson(/*lang=json*/ """
            {
              "version": "1.*",
              "settings": {
              },
              "elements": {
                "userPoint": {
                  "stemHeightScale": 0
                }
              },
              "extensions": {
                "Routing": {
                  "BasePoint": {
                    "parent": "userPoint",
                    "scale": 0.5,
                    "iconScale": 2.3
                  },
                  "Point": {
                    "parent": "Routing.BasePoint",
                    "fillColor": "#FF000000",
                    "strokeColor": "#FFFFFFFF"
                  },
                  "SplitPoint": {
                    "parent": "Routing.BasePoint",
                    "fillColor": "#FFFFFFFF", 
                    "strokeColor": "#FFFF0000",
                    "iconColor": "#FFFF0000"
                  },
                  "Line": {
                    "fillColor": "#FFFF00FF",
                    "strokeColor": "#FF400040",
                    "strokeWidthScale": 4
                  },
                  "hovered": {
                    "strokeWidthScale": 8
                  },
                  "new": {
                    "fillColor": "#40808080",
                    "strokeColor": "#40404040"
                  },
                  "calculating": {
                    "fillColor": "#C0FF00FF",
                    "strokeColor": "#C0400040"
                  },
                  "retry": {
                    "fillColor": "#C0DA3B01",
                    "strokeColor": "#C06D1D01"
                  },
                  "error": {
                    "fillColor": "#C0800000",
                    "strokeColor": "#C0400000"
                  },
                  "diff": {
                    "fillColor": "#C0FF0000",
                    "strokeColor": "#C000FF00",
                    "strokeWidthScale": 6
                  },
                  "hover": {
                    "scale": 0.65
                  }
                },
                "Info": {
                  "BasePoint": {
                    "parent": "userPoint",
                    "fontWeight": "SemiBold",
                    "strokeColor": "#C0FFFFFF",
                    "iconScale": 1.2, 
                    "scale": 0.75
                  },
                  "SectionBasePoint": {
                    "parent": "Info.BasePoint", 
                    "fillColor": "#A0FFFFFF", 
                    "iconColor": "#FF004200"
                  },
                  "WaterBasePoint": {
                    "parent": "Info.BasePoint", 
                    "fillColor": "#A0FFFFFF", 
                    "iconColor": "#FF0000BF"
                  },
                  "ShopBasePoint": {
                    "parent": "Info.BasePoint", 
                    "fillColor": "#A0F0E036", 
                    "iconColor": "#FF871113"
                  },
                  "FoodBasePoint": {
                    "parent": "Info.BasePoint", 
                    "fillColor": "#A085F0EE", 
                    "iconColor": "#FF2E084A"
                  },
                  "Restaurant": {
                    "parent": "Info.FoodBasePoint", 
                    "shape-icon": "knifeAndFork"
                  },
                  "FastFood": {
                    "parent": "Info.FoodBasePoint", 
                    "shape-icon": "car"
                  },
                  "Bar": {
                    "parent": "Info.FoodBasePoint", 
                    "shape-icon": "mug"
                  },
                  "FuelStation": {
                    "parent": "Info.ShopBasePoint", 
                    "shape-icon": "gasPump"
                  },
                  "Bakery": {
                    "parent": "Info.ShopBasePoint", 
                    "shape-icon": "cupcake"
                  },
                  "Supermarket": {
                    "parent": "Info.ShopBasePoint", 
                    "shape-icon": "shoppingCart"
                  },
                  "MountainPass": {
                    "parent": "Info.SectionBasePoint", 
                    "shape-icon": "naturalPlace"
                  },
                  "Toilet": {
                    "parent": "Info.WaterBasePoint", 
                    "shape-icon": "toilet"
                  },
                  "Water": {
                    "parent": "Info.WaterBasePoint", 
                    "shape-icon": "brewery"
                  },
                  "hover": {
                    "scale": 1,
                    "strokeColor": "#FF000000"
                  }
                },
                "POI": {
                  "BasePoint": {
                    "parent": "userPoint",
                    "fontWeight": "Bold",
                    "strokeColor": "#FF000000",
                    "iconScale": 1.2
                  },
                  "SectionBasePoint": {
                    "parent": "POI.BasePoint", 
                    "fillColor": "#FFFFFFFF", 
                    "iconColor": "#FF004200"
                  },
                  "WaterBasePoint": {
                    "parent": "POI.BasePoint", 
                    "fillColor": "#FFFFFFFF", 
                    "iconColor": "#FF0000BF"
                  },
                  "ShopBasePoint": {
                    "parent": "POI.BasePoint", 
                    "fillColor": "#FFF0E036", 
                    "iconColor": "#FF871113"
                  },
                  "FoodBasePoint": {
                    "parent": "POI.BasePoint", 
                    "fillColor": "#FF85F0EE", 
                    "iconColor": "#FF2E084A"
                  },
                  "Restaurant": {
                    "parent": "POI.FoodBasePoint", 
                    "shape-icon": "knifeAndFork"
                  },
                  "FastFood": {
                    "parent": "POI.FoodBasePoint", 
                    "shape-icon": "car"
                  },
                  "Bar": {
                    "parent": "POI.FoodBasePoint", 
                    "shape-icon": "mug"
                  },
                  "FuelStation": {
                    "parent": "POI.ShopBasePoint", 
                    "shape-icon": "gasPump"
                  },
                  "Bakery": {
                    "parent": "POI.ShopBasePoint", 
                    "shape-icon": "cupcake"
                  },
                  "Supermarket": {
                    "parent": "POI.ShopBasePoint", 
                    "shape-icon": "shoppingCart"
                  },
                  "MountainPass": {
                    "parent": "POI.SectionBasePoint", 
                    "shape-icon": "naturalPlace"
                  },
                  "Split": {
                    "parent": "POI.SectionBasePoint", 
                    "shape-icon": "lightning"
                  },
                  "Goal": {
                    "parent": "POI.SectionBasePoint", 
                    "shape-icon": "beach"
                  },
                  "Toilet": {
                    "parent": "POI.WaterBasePoint", 
                    "shape-icon": "toilet"
                  },
                  "Water": {
                    "parent": "POI.WaterBasePoint", 
                    "shape-icon": "brewery"
                  }
                }
              }
            }
            """);

        public static MapStyleSheet Empty { get; } = MapStyleSheet.ParseFromJson(/*lang=json*/ """
            {
              "version": "1.*",
              "elements": {
                "baseMapElement": {
                  "labelVisible": false, 
                  "visible": false
                },
                "political": {
                  "borderVisible": false
                }
              }
            }
            """);
    }
}