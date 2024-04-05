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
                  "Point": {
                    "scale": 0.5,
                    "fillColor": "#FF000000",
                    "strokeColor": "#FFFFFFFF",
                    "iconScale": 2.3
                  },
                  "SplitPoint": {
                    "scale": 0.5, 
                    "fillColor": "#FFFFFFFF", 
                    "strokeColor": "#FFFF0000",
                    "iconColor": "#FFFF0000", 
                    "iconScale": 2.3
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
                  "Base": {
                    "parent": "userPoint", 
                    "iconScale": 1.2, 
                    "scale": 0.9
                  },
                  "SectionBase": {
                    "parent": "Info.Base", 
                    "fillColor": "#80FFFFFF", 
                    "iconColor": "#FF004200"
                  },
                  "WaterBase": {
                    "parent": "Info.Base", 
                    "fillColor": "#80FFFFFF", 
                    "iconColor": "#FF0000BF"
                  },
                  "ShopBase": {
                    "parent": "Info.Base", 
                    "fillColor": "#80F0E036", 
                    "iconColor": "#FF871113"
                  },
                  "FoodBase": {
                    "parent": "Info.Base", 
                    "fillColor": "#8085F0EE", 
                    "iconColor": "#FF2E084A"
                  },
                  "Restaurant": {
                    "parent": "Info.FoodBase", 
                    "shape-icon": "knifeAndFork"
                  },
                  "FastFood": {
                    "parent": "Info.FoodBase", 
                    "shape-icon": "car"
                  },
                  "Bar": {
                    "parent": "Info.FoodBase", 
                    "shape-icon": "mug"
                  },
                  "FuelStation": {
                    "parent": "Info.ShopBase", 
                    "shape-icon": "gasPump"
                  },
                  "Bakery": {
                    "parent": "Info.ShopBase", 
                    "shape-icon": "cupcake"
                  },
                  "Supermarket": {
                    "parent": "Info.ShopBase", 
                    "shape-icon": "shoppingCart"
                  },
                  "MountainPass": {
                    "parent": "Info.SectionBase", 
                    "shape-icon": "naturalPlace"
                  },
                  "Toilet": {
                    "parent": "Info.WaterBase", 
                    "shape-icon": "toilet"
                  },
                  "Water": {
                    "parent": "Info.WaterBase", 
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