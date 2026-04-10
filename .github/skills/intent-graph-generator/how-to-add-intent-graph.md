## How to add intent graph to your mod

You need add following files in your mod pck:

- `{yourmodid}/intentgraph.json` - main intent graph file, used to define intent graph for monsters.
- `{yourmodid}/localization/{language}/intentgraph.json` - localization file for intent graph, used to define text for conditions and intent graphs.

Content of these files will be described in later sections.

## Automatic generation and condition handling

In most cases you don't need to manually add intent graph to your mod. It can be generated automatically. An exception is that if you use `ConditionalBranchState`, you need to add text to describe the condition. This can be done by adding a localization file at `{yourmodid}/localization/{language}/intentgraph.json`. Text for conditions is like:

```json
{
    "branch.{namespace of monster model}.{class name of monster model}.{ID of ConditionalBranchState}.{ID of child state}": "{text to describe the condition}",
    // Example
    "branch.MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock.POST_HEADBUTT.DIZZY_MOVE": "Blocked"
}
```

This can also be used to override default text of `RandomBranchState`, using same key pattern.

## Manually adding or modifying generated intent graph

If you want to manually add or modify generated intent graph, you need to add a json file to describe the intent graph. The file should be placed at `{yourmodid}/intentgraph.json`. The content of the file should be like:

```json
{
    "{namespace of monster model}.{class name of monster model}": [
        {
            // Graph definition, will be mentioned later
        }
    ],
    // Example
    "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast": [
    ]
}
```

Here're some use cases for manually adding or modifying generated intent graph:

### Secondary initial state

Your monster may have multiple forms triggered by certain buff or other condition, not by state machine transitions. In this case you can add secondary initial states. The generater will generate intent graphs for the secondary initial states below the initial state.

Here's an example of the content in `intentgraph.json` for this use case:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast": [
        {
            // An array of state IDs of the secondary initial states.
            "secondaryInitialStates": [
                "STUN_MOVE"
            ]
        }
    ]
}
```

### Overwriting whole intent graph

You can also overwrite the whole intent graph. This is useful when the generated intent graph is not good enough, or you want to add some custom nodes that can't be generated.

It's suggested to use `stateMachine` property to define overwritten intent graph:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.HauntedShip": [
        {
            // Contains list of nodes
            "stateMachine": [
                {
                    // Node name, must be unique
                    "name": "HAUNT_MOVE",
                    // State ID defined in the monster model, can be omitted if same as node name.
                    "moveName": "HAUNT_MOVE",
                    // Whether this node is an initial state.
                    "isInitialState": true,
                    // Optional, if there're multiple initial states, smaller priority is shown first.
                    "initialStatePriority": 0,
                    // Node name of next state.
                    "followUpState": "RAMMING_SPEED_MOVE"
                },
                {
                    "name": "RAMMING_SPEED_MOVE",
                    "followUpState": "random1"
                },
                {
                    "name": "random1",
                    "followUpState": "RAMMING_SPEED_MOVE",
                    "children": [
                        {
                            "label": "50%",
                            // Same as other nodes. It can have its own follow up state and children.
                            "node": {
                                "name": "SWIPE_MOVE"
                            }
                        },
                        {
                            "label": "50%",
                            "node": {
                                "name": "STOMP_MOVE"
                            }
                        }
                    ]
                }
            ]
        }
    ]
}
```

Alternatively, you can use `graph` property to accurately define the graph. It means you can set position of every icon, text or arrow. When `graph` is used, the `stateMachine` will be ignored. Here's an example:

```json
{
	"MegaCrit.Sts2.Core.Models.Monsters.HauntedShip": {
		"graph": {
            // Define width and height
			"width": 7.86,
			"height": 3.6,
			"moves": [
				{
                    // Position on the graph, icons are 1 unit high and wide.
					"x": 0,
					"y": 0,
                    // State ID defined in the monster model. If it contains multiple intents, this create multiple icons.
					"id": "RAMMING_SPEED_MOVE"
				}
			],
            // It's suggested to use moves instead of icons. So you don't need to change damage values for different ascensions.
            "icons": [
                {
                    "x": 0,
                    "y": 1.5,
                    // See `IntentType` enum
                    "intentType": "Attack",
                    // 10x2 attack
                    "value": 10,
                    "times": 2,
                    // Optional, show value and times as texts instead of numbers
                    "valueText": "N",
                    "timesText": "T"
                }
            ],
            // Squares
			"iconGroups": [
				{
					"x": 3.72,
					"y": 0,
					"width": 1.92,
					"height": 3.6
				}
			],
			"labels": [
				{
					"x": 3.82,
					"y": 0.25,
                    // left, center, or right
                    "align": "left",
                    // It can also be a localization key defined in `{yourmodid}/localization/{language}/intentgraph.json`.
					"text": "33%, ≤1"
				}
			],
			"arrows": [
				{
                    // Format [ start_direction, start_x, start_y, ... ]
                    // If start_direction is 0, it starts horizontally, 1 is vertically.
                    // So [ 0, x0, y0, x1, y2, x3, y4, ... ] creates arrow at
                    // (x0, y0) -> (x1, y0) -> (x1, y2) -> (x3, y2) -> ...
                    // [ 1, x0, y0, y1, x2, y3, ... ] creates arrow at
                    // (x0, y0) -> (x0, y1) -> (x2, y1) -> (x2, y3) -> ...
					"path": [0, 1.72, 0.5, 2.22]
				}
			]
		}
	}
}
```

### Patching intent graph

You can also patch the generated intent graph. This is useful when you just want to add some texts, icons, or arrows, but don't want to define the whole graph.
It's in same format as `graph`, but the property name is `graphPatch`. It can be used together with `stateMachine`.

### Replace values of an intent

A monster may have dynamic values for its intents. For example, it attacks 1 more time after every 2 turns. You may want to replace its value and add descriptions for it. It can be done by `moveReplacements` property. Here's an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.TestSubject": [
        {
            "moveReplacements": {
                // State ID defined in the monster model.
                "MULTI_CLAW_MOVE": [
                    // Each object is related to an intent of the state. You can put `null` here to skip an intent.
                    {
                        // Both are optional, you can only replace one of them needed.
                        "valueText": "N",
                        "timesText": "T"
                    }
                ]
            },
            // Add a description using `graphPatch`.
            "graphPatch": {
                "labels": [
                    {
                        "x": 3.5,
                        "y": 0.8,
                        "text": "text.MegaCrit.Sts2.Core.Models.Monsters.TestSubject.MULTI_CLAW_MOVE"
                    }
                ]
            }
        }
    ]
}
```

## Different intent graphs for different conditions

Intent graph of a monster can be different for different ascensions or when in different monster slots. You can set `condition` property to choose which graph to show. The **last** graph that matches the condition will be shown. Here's an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.TwoTailedRat": [
        {
            // Default graph
        }
        {
            "condition": "ascension >= 9",
            // Graph definition
        },
        {
            "condition": "ascension >= 9 && slotIndex == 2",
            // Graph definition
        }
    ]
}
```

You may only use `true`, `false`, or number literals in condition, and supported operators are `(`, `)`, `==`, `!=`, `>`, `<`, `>=`, `<=`, `&&`, and `||`.

Supported variables are:
- `ascension`: current ascension level.
- `slotIndex`: current monster slot index, starts from `0`.
- `act`: current act number, starts from `0`. Underdocks is `0`, Hive is `1`, Glory is `2`, etc.
