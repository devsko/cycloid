using Windows.UI.Xaml.Controls.Maps;

namespace cycloid;

partial class ViewModel
{
    private static class StyleSheet
    {
        public static MapStyleSheet Extension { get; } = MapStyleSheet.ParseFromJson("""
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
                  "NewPoint": {
                    "scale": 0.5,
                    "fillColor": "#80000000",
                    "strokeColor": "#FFFFFFFF",
                    "iconColor": "#40FFFFFF",
                    "iconScale": 2.3
                  },
                  "Line": {
                    "fillColor": "#FFFF00FF",
                    "strokeColor": "#FF400040",
                    "strokeWidthScale": 5
                  },
                  "HoveredLine": {
                    "fillColor": "#FFC000C0",
                    "strokeColor": "#FF808080",
                    "strokeWidthScale": 8
                  },
                  "NewLine": {
                    "fillColor": "#80808080",
                    "strokeColor": "#80404040",
                    "strokeWidthScale": 6
                  },
                  "hover": {
                    "scale": 0.6
                  }
                }
              }
            }
            """);

        public static MapStyleSheet Empty { get; } = MapStyleSheet.ParseFromJson("""
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

        public static MapStyleSheet InterestingPoints { get; } = MapStyleSheet.ParseFromJson("""
            {
              "version": "1.*",
              "settings": {
              },
              "elements": {
                "cemetery": {
                  "fillColor": "#59ED0AFF", 
                  "labelVisible": true, 
                  "visible": true
                },
                "bankPoint": {
                  "fontWeight": "semiBold", 
                  "iconColor": "#FFFF10E9", 
                  "iconScale": 2, 
                  "labelColor": "#FF000000", 
                  "labelOutlineColor": "#FFFFFFFF", 
                  "labelScale": 1.2, 
                  "labelVisible": true, 
                  "shadowVisible": false, 
                  "visible": true
                },
                "gasStationPoint": {
                  "fontWeight": "semiBold", 
                  "iconColor": "#FFFF10E9", 
                  "iconScale": 2, 
                  "labelColor": "#FF000000", 
                  "labelOutlineColor": "#FFFFFFFF", 
                  "labelScale": 1.2, 
                  "shadowVisible": false, 
                  "visible": true
                },
                "groceryPoint": {
                  "fontWeight": "semiBold", 
                  "iconColor": "#FFFF10E9", 
                  "iconScale": 2, 
                  "labelColor": "#FF000000", 
                  "labelOutlineColor": "#FFFFFFFF", 
                  "labelScale": 1.2, 
                  "labelVisible": true, 
                  "shadowVisible": false, 
                  "visible": true
                },
                "marketPoint": {
                  "fontWeight": "semiBold", 
                  "iconColor": "#FFFF10E9", 
                  "iconScale": 2, 
                  "labelColor": "#FF000000", 
                  "labelOutlineColor": "#FFFFFFFF", 
                  "labelScale": 1.2, 
                  "labelVisible": true, 
                  "shadowVisible": false, 
                  "visible": true
                },
                "toiletPoint": {
                  "fontWeight": "semiBold", 
                  "iconColor": "#FFFF10E9", 
                  "iconScale": 2, 
                  "labelColor": "#FF000000", 
                  "labelOutlineColor": "#FFFFFFFF", 
                  "labelScale": 1.2, 
                  "labelVisible": true, 
                  "shadowVisible": false, 
                  "visible": true
                },
                "foodPoint": {
                  "fontWeight": "semiBold", 
                  "iconColor": "#FFFF10E9", 
                  "iconScale": 2, 
                  "labelColor": "#FF000000", 
                  "labelOutlineColor": "#FFFFFFFF", 
                  "labelScale": 1.2, 
                  "labelVisible": true, 
                  "shadowVisible": false, 
                  "visible": true
                }
              }
            }
            """);
    }
}