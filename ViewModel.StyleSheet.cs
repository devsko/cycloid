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
                  "HoveredLine": {
                    "fillColor": "#FFFF00FF",
                    "strokeColor": "#FF400040",
                    "strokeWidthScale": 8
                  },
                  "NewLine": {
                    "fillColor": "#40808080",
                    "strokeColor": "#40404040",
                    "strokeWidthScale": 4
                  },
                  "CalculatingLine": {
                    "fillColor": "#40202080",
                    "strokeColor": "#40202040",
                    "strokeWidthScale": 4
                  },
                  "RetryLine": {
                    "fillColor": "#80805200",
                    "strokeColor": "#80402900",
                    "strokeWidthScale": 4
                  },
                  "ErrorLine": {
                    "fillColor": "#80800000",
                    "strokeColor": "#80400000",
                    "strokeWidthScale": 4
                  },
                  "hover": {
                    "scale": 0.65
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

        public static MapStyleSheet InterestingPoints { get; } = MapStyleSheet.ParseFromJson(/*lang=json*/ """
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