## How to add intent graphs to your mod

You need to add the following files to your mod PCK:

- `{yourmodid}/intentgraph.json` - the main intent graph file, used to define intent graphs for monsters.
- `{yourmodid}/localization/{language}/intentgraph.json` - the localization file for intent graphs, used to define text for conditions and intent graphs.

The contents of these files are described in later sections.

## Automatic generation and condition handling

In most cases, you don't need to manually add an intent graph to your mod. It can be generated automatically. The exception is that if you use `ConditionalBranchState`, you need to add text describing the condition. This can be done by adding a localization file at `{yourmodid}/localization/{language}/intentgraph.json`. Condition text uses the following format:

```json
{
    "branch.{namespace of monster model}.{class name of monster model}.{ID of ConditionalBranchState}.{ID of child state}": "{text to describe the condition}",
    // Example
    "branch.MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock.POST_HEADBUTT.DIZZY_MOVE": "Blocked"
}
```

This can also be used to override the default text of `RandomBranchState` using the same key pattern.

## Manually adding or modifying generated intent graph

If you want to manually add or modify a generated intent graph, you need to add a JSON file that describes it. The file should be placed at `{yourmodid}/intentgraph.json`. The content of the file should look like this:

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

Here're some use cases for manually adding or modifying a generated intent graph:

### Secondary initial state

Your monster may have multiple forms triggered by a certain buff or other condition rather than by state machine transitions. In this case, you can add secondary initial states. The generator will generate intent graphs for the secondary initial states below the initial state.

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

### Overwriting the whole intent graph

You can also overwrite the whole intent graph. This is useful when the generated intent graph is not good enough, or you want to add some custom nodes that can't be generated.

It's recommended to use the `stateMachine` property to define the overwritten intent graph:

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
                    // Optional. If there are multiple initial states, the lower priority is shown first.
                    "initialStatePriority": 0,
                    // Name of the next state node.
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
                            // Same as other nodes. It can have its own follow-up state and children.
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

Alternatively, you can use the `graph` property to define the graph precisely. This lets you set the position of every icon, text label, or arrow. When `graph` is used, `stateMachine` is ignored. Here is an example:

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
                    // State ID defined in the monster model. If it contains multiple intents, this creates multiple icons.
					"id": "RAMMING_SPEED_MOVE"
				}
			],
            // It's recommended to use moves instead of icons, so you don't need to change damage values for different ascensions.
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

### Patching an intent graph

You can also patch the generated intent graph. This is useful when you just want to add some text, icons, or arrows but don't want to define the whole graph.
It uses the same format as `graph`, but the property name is `graphPatch`. It can be used together with `stateMachine`.

### Replace values of an intent

A monster may have dynamic values for its intents. For example, it may attack one more time every two turns. You may want to replace its value and add a description for it. This can be done with the `moveReplacements` property. Here is an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.TestSubject": [
        {
            "moveReplacements": {
                // State ID defined in the monster model.
                "MULTI_CLAW_MOVE": [
                    // Each object is related to an intent of the state. You can put `null` here to skip an intent.
                    {
                        // Both are optional; replace only the one you need.
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

The intent graph of a monster can be different for different ascensions or when it appears in different monster slots. You can set the `condition` property to choose which graph to show. The **last** graph that matches the condition will be shown. Here is an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.TwoTailedRat": [
        {
            // Default graph
        },
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

You may only use `true`, `false`, or number literals in a condition, and the supported operators are `(`, `)`, `==`, `!=`, `>`, `<`, `>=`, `<=`, `&&`, and `||`.

Supported variables are:
- `ascension`: current ascension level.
- `slotIndex`: current monster slot index, starting at `0`.
- `act`: current act number, starting at `0`. Underdocks is `0`, Hive is `1`, Glory is `2`, etc.
- `m.{field or property name}`: a field or property of the monster model. Note that this is only read after the monster is added to combat.