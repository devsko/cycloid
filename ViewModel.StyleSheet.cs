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
                    "fillColor": "#C0805200",
                    "strokeColor": "#C0402900"
                  },
                  "error": {
                    "fillColor": "#C0800000",
                    "strokeColor": "#C0400000"
                  },
                  "hover": {
                    "scale": 0.65
                  }
                },
                "Info": {
                  "Water": {
                    "fillColor": "#80FFFFFF", 
                    "iconColor": "#FF0000BF", 
                    "iconScale": 1.2, 
                    "scale": 0.85, 
                    "shape": {
                      "icon": "brewery"
                    },
                    "strokeColor": "#00000000"
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