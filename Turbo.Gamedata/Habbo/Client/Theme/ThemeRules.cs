using System.Text.Json.Nodes;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>
/// The port's rules over what the client's files say of each themed component, copied from Nitro
/// Studio's <c>server/theme/theme-rules.json</c>: per component (by cascade key) and style, the
/// variants the theme has, the fields where nitro-react draws a component other than Flash does
/// (<c>set</c>) or leaves one out (<c>unset</c>), the Flash window type when it is not the default
/// one (<c>element</c>), and what a variant cascades to its children over the derived cascade. A
/// variant the rules do not name is not in the theme. Kept as Studio's file is, so a change there is
/// copied here whole.
/// </summary>
internal static class ThemeRules
{
    /// <summary>The rules as a fresh JSON object.</summary>
    public static JsonObject Read() => JsonNode.Parse(JSON)!.AsObject();

    private const string JSON = """
        {
          "components": {
            "border": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "3": {},
                "4": {},
                "5": {},
                "6": {},
                "7": {},
                "8": {},
                "9": {
                  "set": {
                    "tintColor": "#686661"
                  }
                },
                "10": {},
                "11": {},
                "12": {},
                "13": {},
                "14": {},
                "15": {},
                "16": {},
                "100": {},
                "101": {},
                "102": {},
                "103": {},
                "104": {},
                "105": {},
                "106": {},
                "107": {},
                "108": {},
                "200": {}
              }
            },
            "bubble": {
              "variants": {
                "0": {},
                "7": {
                  "set": {
                    "layout": {
                      "minWidth": 27,
                      "minHeight": 38
                    }
                  }
                }
              },
              "cascade": {
                "0": {
                  "unset": [
                    "bubblePointerUp",
                    "bubblePointerDown",
                    "bubblePointerLeft",
                    "bubblePointerRight"
                  ]
                }
              }
            },
            "button": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "3": {},
                "4": {},
                "5": {},
                "6": {},
                "100": {},
                "101": {},
                "102": {},
                "103": {},
                "104": {},
                "105": {},
                "106": {},
                "200": {}
              }
            },
            "buttonGroupCenter": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "100": {}
              }
            },
            "buttonGroupLeft": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "100": {}
              }
            },
            "buttonGroupRight": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "100": {}
              }
            },
            "buttonThick": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "3": {},
                "4": {},
                "5": {},
                "6": {}
              }
            },
            "checkBox": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "width": 15,
                      "height": 15,
                      "padding": 0
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "width": 15,
                      "height": 15,
                      "padding": 0
                    }
                  }
                },
                "2": {
                  "set": {
                    "layout": {
                      "width": 15,
                      "height": 15,
                      "padding": 0
                    }
                  }
                },
                "100": {
                  "set": {
                    "layout": {
                      "width": 38,
                      "height": 21
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight",
                    "layout.paddingRight"
                  ]
                },
                "101": {
                  "set": {
                    "layout": {
                      "width": 19,
                      "height": 20
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight",
                    "layout.paddingRight"
                  ]
                }
              }
            },
            "closeButton": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "3": {},
                "4": {},
                "5": {},
                "100": {},
                "101": {},
                "102": {},
                "103": {},
                "10000": {},
                "10001": {},
                "10002": {},
                "10003": {},
                "10004": {},
                "10005": {},
                "10006": {},
                "10007": {}
              }
            },
            "containerButton": {
              "variants": {
                "0": {
                  "set": {
                    "textStyle": "button_regular"
                  }
                },
                "1": {
                  "set": {
                    "textStyle": "button_regular",
                    "textColor": "#ffffff"
                  }
                },
                "2": {
                  "set": {
                    "textStyle": "button_regular"
                  }
                },
                "3": {
                  "set": {
                    "textStyle": "button_shiny_regular"
                  }
                },
                "4": {
                  "set": {
                    "textStyle": "button_shiny_regular"
                  }
                },
                "5": {
                  "set": {
                    "textStyle": "button_shiny_regular"
                  }
                },
                "6": {
                  "set": {
                    "textStyle": "button_shiny_regular",
                    "textColor": "#ffffff"
                  }
                },
                "7": {
                  "unset": [
                    "layout"
                  ]
                },
                "100": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "101": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "102": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "103": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "104": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "105": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "106": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "107": {
                  "set": {
                    "textStyle": "il_button_white"
                  }
                },
                "108": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "109": {
                  "set": {
                    "textStyle": "il_button"
                  }
                },
                "200": {
                  "set": {
                    "textStyle": "id_button"
                  }
                }
              }
            },
            "contentArea": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "marginLeft": 6,
                      "marginTop": 4,
                      "marginRight": 6,
                      "marginBottom": 7
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "marginLeft": 3,
                      "marginTop": 3,
                      "marginRight": 3,
                      "marginBottom": 3
                    }
                  }
                },
                "7": {
                  "set": {
                    "layout": {
                      "marginLeft": 3,
                      "marginTop": 3,
                      "marginRight": 3,
                      "marginBottom": 12
                    }
                  }
                },
                "100": {
                  "set": {
                    "layout": {
                      "marginLeft": 1,
                      "marginTop": 0,
                      "marginRight": 1,
                      "marginBottom": 1
                    }
                  }
                },
                "101": {
                  "set": {
                    "layout": {
                      "marginLeft": 1,
                      "marginTop": 0,
                      "marginRight": 1,
                      "marginBottom": 1
                    }
                  }
                },
                "10000": {
                  "set": {
                    "layout": {
                      "marginLeft": 10,
                      "marginTop": 12,
                      "marginRight": 10,
                      "marginBottom": 9
                    }
                  }
                }
              }
            },
            "droplist": {
              "variants": {
                "0": {
                  "unset": [
                    "textStyle"
                  ]
                },
                "1": {
                  "unset": [
                    "textStyle"
                  ]
                }
              },
              "cascade": {
                "0": {
                  "unset": [
                    "region"
                  ]
                },
                "1": {
                  "unset": [
                    "region"
                  ]
                }
              }
            },
            "droplistItem": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "minWidth": 5,
                      "minHeight": 19
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "minWidth": 5,
                      "minHeight": 19
                    }
                  }
                }
              }
            },
            "dropmenu": {
              "variants": {
                "0": {
                  "set": {
                    "textColor": "#000000",
                    "listInset": {
                      "left": 6,
                      "top": 2,
                      "right": 6
                    }
                  }
                },
                "1": {
                  "set": {
                    "listInset": {
                      "left": 6,
                      "top": 2,
                      "right": 6
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "minHeight": 23
                    },
                    "textColor": "#000000",
                    "listInset": {
                      "left": 2,
                      "top": 2,
                      "right": 2
                    }
                  }
                },
                "100": {
                  "set": {
                    "textColor": "#000000",
                    "listInset": {
                      "left": 6,
                      "top": 2,
                      "right": 6
                    }
                  }
                }
              },
              "cascade": {
                "0": {
                  "unset": [
                    "region"
                  ]
                },
                "1": {
                  "unset": [
                    "region"
                  ]
                },
                "3": {
                  "unset": [
                    "region"
                  ]
                },
                "100": {
                  "unset": [
                    "region"
                  ]
                }
              }
            },
            "dropmenuItem": {
              "variants": {
                "0": {
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                },
                "1": {
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                },
                "3": {
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                },
                "100": {
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                }
              }
            },
            "frame": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "3": {},
                "4": {},
                "7": {
                  "unset": [
                    "tintColor"
                  ]
                },
                "100": {
                  "unset": [
                    "textStyle",
                    "dropShadow"
                  ]
                },
                "101": {
                  "unset": [
                    "textStyle"
                  ]
                },
                "102": {
                  "unset": [
                    "textStyle",
                    "dropShadow"
                  ]
                },
                "103": {
                  "unset": [
                    "textStyle",
                    "dropShadow"
                  ]
                },
                "200": {
                  "unset": [
                    "textStyle",
                    "dropShadow"
                  ]
                },
                "10000": {
                  "unset": [
                    "tintColor",
                    "dropShadow"
                  ]
                },
                "10001": {
                  "unset": [
                    "tintColor",
                    "dropShadow"
                  ]
                },
                "10002": {
                  "unset": [
                    "tintColor",
                    "dropShadow"
                  ]
                },
                "10003": {
                  "unset": [
                    "tintColor",
                    "dropShadow"
                  ]
                },
                "10004": {
                  "unset": [
                    "tintColor",
                    "dropShadow"
                  ]
                },
                "10005": {
                  "unset": [
                    "tintColor",
                    "dropShadow"
                  ]
                },
                "10006": {
                  "unset": [
                    "tintColor",
                    "dropShadow"
                  ]
                },
                "10007": {
                  "unset": [
                    "tintColor",
                    "dropShadow"
                  ]
                }
              },
              "cascade": {
                "0": {
                  "set": {
                    "contentArea": "0"
                  },
                  "unset": [
                    "scaler"
                  ]
                },
                "1": {
                  "unset": [
                    "scaler"
                  ]
                },
                "2": {
                  "unset": [
                    "scaler"
                  ]
                },
                "3": {
                  "set": {
                    "tabContext": "3",
                    "tabContent": "3",
                    "contentArea": "3"
                  }
                },
                "4": {
                  "set": {
                    "header": "3",
                    "tabContext": "3",
                    "tabContent": "3",
                    "scaler": "3",
                    "contentArea": "3"
                  }
                },
                "7": {
                  "set": {
                    "tabContext": "3",
                    "tabContent": "3",
                    "scaler": "3",
                    "contentArea": "7"
                  },
                  "unset": [
                    "framePointerDown"
                  ]
                },
                "100": {
                  "set": {
                    "header": "frame-100",
                    "contentArea": "100"
                  },
                  "unset": [
                    "closeButton",
                    "scaler"
                  ]
                },
                "101": {
                  "set": {
                    "header": "frame-101",
                    "contentArea": "101"
                  },
                  "unset": [
                    "border",
                    "closeButton",
                    "scaler"
                  ]
                },
                "102": {
                  "set": {
                    "contentArea": "100"
                  },
                  "unset": [
                    "border",
                    "closeButton",
                    "scaler"
                  ]
                },
                "103": {
                  "set": {
                    "header": "frame-103",
                    "contentArea": "100"
                  },
                  "unset": [
                    "closeButton",
                    "scaler"
                  ]
                },
                "200": {
                  "set": {
                    "header": "frame-200",
                    "contentArea": "100"
                  },
                  "unset": [
                    "closeButton",
                    "scaler"
                  ]
                },
                "10000": {
                  "set": {
                    "contentArea": "10000"
                  }
                },
                "10001": {
                  "set": {
                    "contentArea": "10000"
                  }
                },
                "10002": {
                  "set": {
                    "contentArea": "10000"
                  }
                },
                "10003": {
                  "set": {
                    "contentArea": "10000"
                  }
                },
                "10004": {
                  "set": {
                    "contentArea": "10000"
                  }
                },
                "10005": {
                  "set": {
                    "contentArea": "10000"
                  }
                },
                "10006": {
                  "set": {
                    "contentArea": "10000"
                  }
                },
                "10007": {
                  "set": {
                    "contentArea": "10000"
                  }
                }
              }
            },
            "framePointerDown": {
              "variants": {
                "7": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 12
                    }
                  }
                }
              }
            },
            "header": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "margin": 6,
                      "padding": 0
                    },
                    "needsBgChip": true,
                    "captionTop": 1,
                    "controlsTop": 0,
                    "menuButton": {
                      "variant": "5",
                      "left": 1,
                      "top": 0
                    },
                    "textColor": "#ffffff"
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingLeft",
                    "layout.paddingTop",
                    "layout.paddingRight",
                    "layout.paddingBottom"
                  ]
                },
                "1": {
                  "set": {
                    "layout": {
                      "margin": 6,
                      "padding": 0
                    },
                    "needsBgChip": true,
                    "captionTop": 1,
                    "controlsTop": 0,
                    "menuButton": {
                      "variant": "5",
                      "left": 1,
                      "top": 0
                    },
                    "textColor": "#ffffff"
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingLeft",
                    "layout.paddingTop",
                    "layout.paddingRight",
                    "layout.paddingBottom"
                  ]
                },
                "2": {
                  "set": {
                    "layout": {
                      "margin": 6,
                      "padding": 0
                    },
                    "needsBgChip": true,
                    "captionTop": 1,
                    "controlsTop": 0,
                    "menuButton": {
                      "variant": "5",
                      "left": 1,
                      "top": 0
                    },
                    "textColor": "#ffffff"
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingLeft",
                    "layout.paddingTop",
                    "layout.paddingRight",
                    "layout.paddingBottom"
                  ]
                },
                "3": {
                  "set": {
                    "layout": {
                      "position": "relative",
                      "marginTop": 6,
                      "marginLeft": 6,
                      "marginRight": 8,
                      "height": 27
                    },
                    "captionTop": 3,
                    "controlsTop": 2,
                    "helpButton": "4"
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight",
                    "layout.paddingLeft",
                    "layout.paddingTop",
                    "layout.paddingRight",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "4": {
                  "set": {
                    "layout": {
                      "minHeight": 20
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "tintColor"
                  ]
                },
                "7": {
                  "set": {
                    "layout": {
                      "position": "relative",
                      "marginTop": 6,
                      "marginLeft": 6,
                      "marginRight": 8,
                      "height": 27
                    },
                    "textColor": "#000000",
                    "captionTop": 3,
                    "controlsTop": 2,
                    "helpButton": "4"
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight",
                    "layout.paddingLeft",
                    "layout.paddingTop",
                    "layout.paddingRight",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "200": {
                  "set": {
                    "layout": {
                      "minHeight": 30,
                      "padding": 0
                    },
                    "captionTop": 4
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingLeft",
                    "layout.paddingTop",
                    "layout.paddingRight",
                    "layout.paddingBottom"
                  ]
                },
                "10000": {
                  "set": {
                    "layout": {
                      "minHeight": 40
                    },
                    "captionTop": 7
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingTop",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "10001": {
                  "set": {
                    "layout": {
                      "minHeight": 40
                    },
                    "captionTop": 7
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingTop",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "10002": {
                  "set": {
                    "layout": {
                      "minHeight": 40
                    },
                    "captionTop": 7
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingTop",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "10003": {
                  "set": {
                    "layout": {
                      "minHeight": 40
                    },
                    "captionTop": 7
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingTop",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "10004": {
                  "set": {
                    "layout": {
                      "minHeight": 40
                    },
                    "captionTop": 7
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingTop",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "10005": {
                  "set": {
                    "layout": {
                      "minHeight": 40
                    },
                    "captionTop": 7
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingTop",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "10006": {
                  "set": {
                    "layout": {
                      "minHeight": 40
                    },
                    "captionTop": 7
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingTop",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "10007": {
                  "set": {
                    "layout": {
                      "minHeight": 40
                    },
                    "captionTop": 7
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.paddingTop",
                    "layout.paddingBottom",
                    "tintColor"
                  ]
                },
                "frame-100": {
                  "set": {
                    "textColor": "#000000"
                  }
                },
                "frame-103": {},
                "frame-101": {
                  "set": {
                    "layout": {
                      "height": 70
                    }
                  },
                  "unset": [
                    "layout.minHeight"
                  ]
                },
                "frame-200": {}
              },
              "cascade": {
                "0": {
                  "set": {
                    "closeButton": "0"
                  }
                },
                "1": {
                  "set": {
                    "closeButton": "0"
                  }
                },
                "2": {
                  "set": {
                    "closeButton": "0"
                  }
                },
                "3": {
                  "set": {
                    "closeButton": "3"
                  }
                },
                "4": {
                  "set": {
                    "closeButton": "0"
                  }
                },
                "7": {
                  "set": {
                    "closeButton": "3"
                  }
                }
              }
            },
            "iconButton": {
              "variants": {
                "3": {
                  "set": {
                    "layout": {
                      "width": 22,
                      "height": 22,
                      "flexShrink": 0
                    }
                  }
                },
                "4": {
                  "set": {
                    "layout": {
                      "width": 22,
                      "height": 22,
                      "flexShrink": 0
                    }
                  }
                }
              }
            },
            "radioButton": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 16,
                      "padding": 0
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 16,
                      "padding": 0
                    }
                  }
                },
                "2": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 16,
                      "padding": 0
                    }
                  }
                },
                "100": {
                  "set": {
                    "layout": {
                      "width": 11,
                      "height": 14
                    },
                    "textColor": "#000000"
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight",
                    "layout.paddingTop",
                    "layout.paddingRight",
                    "layout.paddingBottom"
                  ]
                }
              }
            },
            "region": {
              "variants": {
                "0": {}
              }
            },
            "scaler": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "position": "absolute",
                      "right": 0,
                      "bottom": 0,
                      "width": 15,
                      "height": 15
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                },
                "1": {
                  "set": {
                    "layout": {
                      "position": "absolute",
                      "right": 0,
                      "bottom": 0,
                      "width": 15,
                      "height": 15
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                },
                "2": {
                  "set": {
                    "layout": {
                      "position": "absolute",
                      "right": 0,
                      "bottom": 0,
                      "width": 15,
                      "height": 15
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                },
                "3": {
                  "set": {
                    "layout": {
                      "position": "absolute",
                      "right": 3,
                      "bottom": 4,
                      "width": 20,
                      "height": 20
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                },
                "4": {
                  "set": {
                    "layout": {
                      "position": "absolute",
                      "right": 3,
                      "bottom": 4,
                      "width": 20,
                      "height": 20
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                },
                "100": {
                  "set": {
                    "zIndex": 20
                  }
                }
              }
            },
            "scrollbarSliderBarHorizontal": {
              "variants": {
                "0": {},
                "1": {},
                "3": {},
                "100": {},
                "200": {}
              }
            },
            "scrollbarSliderBarVertical": {
              "variants": {
                "0": {},
                "1": {},
                "3": {},
                "100": {},
                "200": {}
              }
            },
            "scrollbarSliderButtonDown": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "width": 17,
                      "height": 16
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "width": 17,
                      "height": 16
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "width": 17,
                      "height": 16
                    }
                  }
                }
              }
            },
            "scrollbarSliderButtonLeft": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 17
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 17
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "width": 17,
                      "height": 17
                    }
                  }
                }
              }
            },
            "scrollbarSliderButtonRight": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 17
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 17
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "width": 16,
                      "height": 17
                    }
                  }
                }
              }
            },
            "scrollbarSliderButtonUp": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "width": 17,
                      "height": 16
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "width": 17,
                      "height": 16
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "width": 17,
                      "height": 16
                    }
                  }
                }
              }
            },
            "scrollbarSliderTrackHorizontal": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "minWidth": 1,
                      "minHeight": 17
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "minWidth": 1,
                      "minHeight": 17
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "minWidth": 2,
                      "minHeight": 17
                    }
                  }
                },
                "100": {},
                "200": {}
              }
            },
            "scrollbarSliderTrackVertical": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "minWidth": 17,
                      "minHeight": 1
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "minWidth": 17,
                      "minHeight": 1
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "minWidth": 17,
                      "minHeight": 2
                    }
                  }
                },
                "100": {
                  "set": {
                    "layout": {
                      "minWidth": 10,
                      "minHeight": 1
                    }
                  }
                },
                "200": {
                  "set": {
                    "layout": {
                      "minWidth": 8,
                      "minHeight": 1
                    }
                  }
                }
              }
            },
            "tabButton": {
              "variants": {
                "0": {},
                "1": {},
                "2": {},
                "3": {
                  "set": {
                    "layout": {
                      "paddingTop": 0,
                      "paddingBottom": 0,
                      "maxHeight": 32
                    }
                  },
                  "unset": [
                    "layout.minWidth"
                  ]
                }
              }
            },
            "tabContent": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "padding": 6,
                      "marginTop": 2
                    }
                  }
                },
                "1": {
                  "set": {
                    "layout": {
                      "padding": 6,
                      "marginTop": 2
                    }
                  }
                },
                "2": {
                  "set": {
                    "layout": {
                      "padding": 6
                    }
                  }
                },
                "3": {
                  "set": {
                    "layout": {
                      "paddingTop": 6,
                      "paddingLeft": 5,
                      "paddingRight": 5,
                      "paddingBottom": 2,
                      "marginTop": 2,
                      "minHeight": 15
                    }
                  }
                }
              }
            },
            "tabContext": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "minHeight": 22,
                      "maxHeight": 22,
                      "paddingLeft": 6,
                      "paddingRight": 6
                    }
                  },
                  "unset": [
                    "layout.minWidth"
                  ]
                },
                "3": {
                  "set": {
                    "layout": {
                      "paddingLeft": 8,
                      "paddingRight": 8
                    }
                  },
                  "unset": [
                    "layout.minWidth",
                    "layout.minHeight"
                  ]
                }
              },
              "cascade": {
                "0": {
                  "unset": [
                    "tabContent"
                  ]
                },
                "3": {
                  "set": {
                    "tabButton": "3"
                  },
                  "unset": [
                    "tabContent"
                  ]
                },
                "7": {
                  "set": {
                    "tabButton": "3"
                  }
                }
              }
            },
            "tooltip": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "flex": 1,
                      "justifyContent": "center",
                      "alignItems": "center"
                    }
                  },
                  "unset": [
                    "layout.paddingTop",
                    "layout.paddingBottom"
                  ]
                }
              }
            },
            "bubblePointerLeft": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "marginRight": -2
                    }
                  },
                  "note": "The port lays the pointer out in flow beside its bubble: this pulls it over the bubble's edge (and style 7's 6px transparent margin) where BubbleController.pointerOffset puts it."
                },
                "7": {
                  "set": {
                    "layout": {
                      "marginRight": -9
                    }
                  },
                  "note": "The port lays the pointer out in flow beside its bubble: this pulls it over the bubble's edge (and style 7's 6px transparent margin) where BubbleController.pointerOffset puts it."
                }
              }
            },
            "bubblePointerRight": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "marginLeft": -2
                    }
                  },
                  "note": "The port lays the pointer out in flow beside its bubble: this pulls it over the bubble's edge (and style 7's 6px transparent margin) where BubbleController.pointerOffset puts it."
                },
                "7": {
                  "set": {
                    "layout": {
                      "marginLeft": -9
                    }
                  },
                  "note": "The port lays the pointer out in flow beside its bubble: this pulls it over the bubble's edge (and style 7's 6px transparent margin) where BubbleController.pointerOffset puts it."
                }
              }
            },
            "bubblePointerUp": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "marginBottom": -3
                    }
                  },
                  "note": "The port lays the pointer out in flow beside its bubble: this pulls it over the bubble's edge (and style 7's 6px transparent margin) where BubbleController.pointerOffset puts it."
                },
                "7": {
                  "set": {
                    "layout": {
                      "marginBottom": -8
                    }
                  },
                  "note": "The port lays the pointer out in flow beside its bubble: this pulls it over the bubble's edge (and style 7's 6px transparent margin) where BubbleController.pointerOffset puts it."
                }
              }
            },
            "bubblePointerDown": {
              "variants": {
                "0": {
                  "set": {
                    "layout": {
                      "marginTop": -3
                    }
                  },
                  "note": "The port lays the pointer out in flow beside its bubble: this pulls it over the bubble's edge (and style 7's 6px transparent margin) where BubbleController.pointerOffset puts it."
                },
                "7": {
                  "set": {
                    "layout": {
                      "marginTop": -9
                    }
                  },
                  "note": "The port lays the pointer out in flow beside its bubble: this pulls it over the bubble's edge (and style 7's 6px transparent margin) where BubbleController.pointerOffset puts it."
                }
              }
            },
            "scrollableItemGridVertical": {
              "variants": {
                "0": {},
                "3": {}
              }
            },
            "scrollableItemListVertical": {
              "variants": {
                "0": {},
                "3": {},
                "100": {}
              }
            }
          }
        }
        """;
}
