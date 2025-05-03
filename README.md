A sample config from my server can be found in the repo.
Adapt it to your likings.

If you want to use the custom UI, you need to upload the images and use the urls.
Its also working with skinids, so feel free to upload them to rust workshop :)


## Skill Tree configs for my setup

```
"Iron Lungs": {
    "Permission required to show this node": null,
    "Minimum prestige required to unlock this node": 0,
    "Skill required to unlock node [Requires max level]": null,
    "Skill that if unlocked, will prevent this node from unlocking": null,
    "enabled": true,
    "max_level": 3,
    "tier": 1,
    "value_per_buff": 0.0,
    "buff_info": {
    "Key": "Permission",
    "Value": "Percentage"
    },
    "icon_url": null,
    "skin": 3475202423,
    "permissions": {
    "description": "This skill increases your max stamina by <color=#00ff00>10%</color> per level",
    "perms": {
        "1": {
        "perms_list": {
            "advancedplayermetabolism.stamina.max1": "10% increased stamina"
        }
        },
        "2": {
        "perms_list": {
            "advancedplayermetabolism.stamina.max2": "20% increased stamina"
        }
        },
        "3": {
        "perms_list": {
            "advancedplayermetabolism.stamina.max3": "30% increased stamina"
        }
        }
    }
    }
},
"Boost": {
    "Permission required to show this node": null,
    "Minimum prestige required to unlock this node": 0,
    "Skill required to unlock node [Requires max level]": null,
    "Skill that if unlocked, will prevent this node from unlocking": null,
    "enabled": true,
    "max_level": 1,
    "tier": 1,
    "value_per_buff": 0.0,
    "buff_info": {
    "Key": "Permission",
    "Value": "Percentage"
    },
    "icon_url": null,
    "skin": 3475218864,
    "permissions": {
    "description": "This enables a boost",
    "perms": {
        "1": {
        "perms_list": {
            "advancedplayermetabolism.boost": "You can have a sprint boost"
        }
        }
    }
    }
},
"Rapid Breath": {
    "Permission required to show this node": null,
    "Minimum prestige required to unlock this node": 0,
    "Skill required to unlock node [Requires max level]": "Iron Lungs",
    "Skill that if unlocked, will prevent this node from unlocking": null,
    "enabled": true,
    "max_level": 4,
    "tier": 2,
    "value_per_buff": 0.0,
    "buff_info": {
    "Key": "Permission",
    "Value": "Percentage"
    },
    "icon_url": null,
    "skin": 3475204116,
    "permissions": {
    "description": "This skill increases your stamina recovery rate by <color=#00ff00>50%</color> per level",
    "perms": {
        "1": {
        "perms_list": {
            "advancedplayermetabolism.stamina.replenish1": "50% increased stamina recovery"
        }
        },
        "2": {
        "perms_list": {
            "advancedplayermetabolism.stamina.replenish1": "100% increased stamina recovery"
        }
        },
        "3": {
        "perms_list": {
            "advancedplayermetabolism.stamina.replenish1": "150% increased stamina recovery"
        }
        },
        "4": {
        "perms_list": {
            "advancedplayermetabolism.stamina.replenish1": "200% increased stamina recovery"
        }
        }
    }
    }
},
"Burst Surge": {
    "Permission required to show this node": null,
    "Minimum prestige required to unlock this node": 0,
    "Skill required to unlock node [Requires max level]": "Boost",
    "Skill that if unlocked, will prevent this node from unlocking": null,
    "enabled": true,
    "max_level": 3,
    "tier": 2,
    "value_per_buff": 0.0,
    "buff_info": {
    "Key": "Permission",
    "Value": "Percentage"
    },
    "icon_url": null,
    "skin": 3475204655,
    "permissions": {
    "description": "This skill increases your max boost by <color=#00ff00>20%</color> per level",
    "perms": {
        "1": {
        "perms_list": {
            "advancedplayermetabolism.boost.max1": "20% increased max boost"
        }
        },
        "2": {
        "perms_list": {
            "advancedplayermetabolism.boost.max2": "40% increased max boost"
        }
        },
        "3": {
        "perms_list": {
            "advancedplayermetabolism.boost.max3": "60% increased max boost"
        }
        }
    }
    }
},
"Adrenaline Rush": {
    "Permission required to show this node": null,
    "Minimum prestige required to unlock this node": 0,
    "Skill required to unlock node [Requires max level]": "Burst Surge",
    "Skill that if unlocked, will prevent this node from unlocking": null,
    "enabled": true,
    "max_level": 4,
    "tier": 3,
    "value_per_buff": 0.0,
    "buff_info": {
    "Key": "Permission",
    "Value": "Percentage"
    },
    "icon_url": null,
    "skin": 3475205037,
    "permissions": {
    "description": "This skill increases your boost recovery rate by <color=#00ff00>50%</color> per level",
    "perms": {
        "1": {
        "perms_list": {
            "advancedplayermetabolism.stamina.replenish1": "50% increased boost recovery"
        }
        },
        "2": {
        "perms_list": {
            "advancedplayermetabolism.stamina.replenish1": "100% increased boost recovery"
        }
        },
        "3": {
        "perms_list": {
            "advancedplayermetabolism.stamina.replenish1": "150% increased boost recovery"
        }
        },
        "4": {
        "perms_list": {
            "advancedplayermetabolism.stamina.replenish1": "200% increased boost recovery"
        }
        }
    }
    }
},
```