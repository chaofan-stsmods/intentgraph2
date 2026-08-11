## How to add intent graphs to your mod

You need to add the following files to your mod PCK:

- `{yourmodid}/intentgraph.json` - the main intent graph file, used to define intent graphs for monsters.
- `{yourmodid}/localization/{language}/intentgraph.json` - the localization file for intent graphs, used to define text for conditions and intent graphs.

The contents of these files are described in later sections.

You can use `editintent` command in game to edit the intent graph of a monster. This is detailed described in the last section of this document.

## Automatic generation and condition handling

In most cases, you don't need to manually add an intent graph to your mod. It can be generated automatically. The exception is that if you use `ConditionalBranchState`, you need to add text describing the condition. This can be done by adding a localization file at `{yourmodid}/localization/{language}/intentgraph.json`. Condition text uses the following format:

```json
{
    "branch.{monster model full name}.{branch move ID}.{child move ID}": "{text to describe the condition}",
    // Example
    "branch.MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock.POST_HEADBUTT.DIZZY_MOVE": "Blocked",
    // {otherwise} is a special mark that will be replaced with the text for the "otherwise" condition and move to the last child.
    "branch.MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock.POST_HEADBUTT.DIZZY_MOVE": "{otherwise}",
    // It can contain variables here, see supported variable section below.
    "branch.MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock.POST_HEADBUTT.DIZZY_MOVE": "Blocked, {m.BaseDizzyDuration} turns"
}
```

This can also be used to override the default text of `RandomBranchState` using the same key pattern.

## Manually adding or modifying generated intent graph

If you want to manually add or modify a generated intent graph, you need to add a JSON file that describes it. The file should be placed at `{yourmodid}/intentgraph.json`. The content of the file should look like this:

```json
{
    "{monster model full name}": [
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

Or add `offset` to the secondary initial state:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast": [
        {
            "secondaryInitialStates": [
                {
                    "id": "STUN_MOVE",
                    "offset": { "x": 0, "y": 1 }  // Optional. Add a margin between the state and the states above.
                }
            ]
        }
    ]
}
```

### Adjust position of generated nodes

By default, the graph starts from x=0, y=0. You can add an `offset` property to the graph to adjust the position of the generated nodes. Here is an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast": [
        {
            "offset": { "x": 0, "y": 0 }
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
                    // Name of the next state node.
                    "followUpState": "RAMMING_SPEED_MOVE",
                    // Optional. If there are multiple initial states, the lower priority is shown first.
                    "initialStatePriority": 0,
                    // Optional. Only take effect when `isInitialState` is true. Offset of the node and its follow-up nodes on the graph.
                    "offset": { "x": 0, "y": 0 },
                    // Optional. If this is set to a number > 0 and moveName is not set,
                    // the node will be shown as a placeholder with the number of intents it has.
                    "placeholderIntentCount": 0,
                },
                {
                    "name": "RAMMING_SPEED_MOVE",
                    "followUpState": "random1"
                },
                {
                    "name": "random1",
                    "followUpState": "RAMMING_SPEED_MOVE",
                    // Optional. Take effect only when children exists. If true, children are shown horizontally, otherwise vertically.
                    // When children have their own follow-up states, it's not recommended to use horizontal layout. Arrows may look weird in this case.
                    "horizontalLayout": false,
                    "children": [
                        {
                            // This can also be key in the localization file
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
					"x": 1,
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

One difference between `graph` and `graphPatch` is that in `graphPatch`, you can use `relativeTo` to specify the position of an item relative to another node. Here is an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.HauntedShip": [
        {
            "graphPatch": {
                "labels": [
                    {
                        // Means the label is 2 units to the right of the node with state ID "HAUNT_MOVE".
                        // `relativeTo` can also be used in moves, icons, iconGroups, and arrows.
                        "x": 2,
                        "y": 0,
                        "relativeTo": "/HAUNT_MOVE",
                        "text": "text.MegaCrit.Sts2.Core.Models.Monsters.HauntedShip.HAUNT_MOVE"
                    }
                ]
            }
        }
    ]
}
```

You may noticed that the `relativeTo` value is `/HAUNT_MOVE`. This is the full name of the state. This is used to distinguish states with the same ID but different parent states. For automatically generated states, the full name is like `/{state id}` or `/{conditional branch state id}/{child state id}`. For states defined in `stateMachine`, the full name is like `/{name}` or `/{name}/{child name}`. Full name is also used in `moveReplacements` to replace values or arrows of a move transition.

### Replace values of an intent

A monster may have dynamic values for its intents. For example, it may attack one more time every two turns. You may want to replace its value and add a description for it. This can be done with the `moveReplacements` property. Here is an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.TestSubject": [
        {
            "moveReplacements": {
                // State ID defined in the monster model. Without the prefix "/", this will change all moves with the same state ID,
                // even if they are child states of a `ConditionalBranchState` or `RandomBranchState`.
                "MULTI_CLAW_MOVE": [
                    // Each object is related to an intent of the state. You can put `null` here to skip an intent.
                    {
                        // Both are optional; replace only the one you need.
                        "valueText": "N",
                        "timesText": "T"
                        // It's allowed to use localization keys here, e.g.
                        // "valueText": "text.MegaCrit.Sts2.Core.Models.Monsters.TestSubject.MULTI_CLAW_MOVE.value"
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

### Change move detail of an intent

When "show move detail" is enabled, the intent graph will show buff/debuff/status of the move. Instead of showing generated icons and values, you may customize it by using `moveReplacements`. Here is an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.SoulFysh": [
        {
            "moveReplacements": {
                "BECKON_MOVE": [
                    {
                        // type can be "card", "power", id is the ID of the card/power.
                        // value is optional. It will show at the left bottom corner of the icon.
                        "details": [{ "type": "card", "id": "BECKON", "value": 2 }]
                    }
                ],
                "GAZE_MOVE": [
                    // Skip the first intent since we don't need to replace it.
                    null,
                    {
                        "details": [{ "type": "card", "id": "BECKON" }]
                    }
                ],
                "FADE_MOVE": [
                    {
                        "details": [{
                            "type": "power",
                            "id": "INTANGIBLE_POWER",
                            // valueText is similar to value but in string type.
                            "valueText": "1"
                            // It's allowed to use localization keys here, e.g.
                            // "valueText": "text.MegaCrit.Sts2.Core.Models.Monsters.SoulFysh.FADE_MOVE.value"
                        }]
                    }
                ]
            }
        }
    ]
}
```

Also it's possible to replace specific move in the graph, since it can appear multiple times, by using the full name of the state. And you may also want to set a condition for when to highlight the move as current move. For what are supported when writting a condition, see section "Different intent graphs for different conditions".

Here is an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.TestSubject": [
        {
            "condition": "showMoveDetail",
            "stateMachine": [
                {
                    "name": "RESPAWN_MOVE1",
                    "moveName": "RESPAWN_MOVE",
                    "isInitialState": true
                },
                {
                    "name": "RESPAWN_MOVE2",
                    "moveName": "RESPAWN_MOVE",
                    "isInitialState": true
                }
            ],
            "moveReplacements": {
                // Both moves are RESPAWN_MOVE but using different power icons on graph.
                // currentMoveCondition defines when to highlight the move as current move.
                // Otherwise both moves will be highlighted at the same time.
                "/RESPAWN_MOVE1": {
                    "intentOverrides": [
                        null,
                        {
                            "details": [{ "type": "power", "id": "PAINFUL_STABS_POWER" }]
                        }
                    ],
                    "currentMoveCondition": "m.Respawns <= 0"
                },
                "/RESPAWN_MOVE2": {
                    "intentOverrides": [
                        null,
                        {
                            "details": [{ "type": "power", "id": "NEMESIS_POWER" }]
                        }
                    ],
                    "currentMoveCondition": "m.Respawns >= 1"
                }
            }
        }
    ]
}
```

### Replace arrow of a move transition

You can also replace the arrow of a move transition. This can also be done with the `moveReplacements` property. Here is an example:

```json
{
    "MegaCrit.Sts2.Core.Models.Monsters.ShrinkerBeetle": [
        {
            "moveReplacements": {
                // State ID defined in the monster model. Full name is used here because arrowOverride won't work without using full name.
                // To replace the arrow of a child state, you need to specify the parent state name, e.g. "/RAND/CHOMP_MOVE".
                // If you use `stateMachine` to define the intent graph, the ID here is constructed from the `name` of the node, not `moveName`.
                "/CHOMP_MOVE": {
                    "arrowOverride": {
                        // Same format as `arrows` in `graph` or `graphPatch`.
                        "path": [ 1, 2, 1, 1.5, 3.5, 1 ]
                        // You can leave start x, y and end x, y null. The position can be automatically calculated.
                        // "path": [ 1, null, null, 1.5, null, null ]
                    },
                    // You can replace the value and times of the intent as well.
                    "intentOverrides": [
                        {
                            "valueText": "N",
                            "timesText": "T"
                        }
                    ]
                }
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

### Supported variables 

- `ascension`: current ascension level.
- `slotIndex`: current monster slot index, starting at `0`.
- `slotName_is_{slotName}`: whether the current monster slot name is the given slot name.
- `act`: current act number, starting at `0`. Underdocks is `0`, Hive is `1`, Glory is `2`, etc.
- `showMoveNames`: whether the setting "show move names" is enabled.
- `inBestiary`: whether the monster is in the bestiary.
- `showMoveDetail`: whether the setting "show move detail" is enabled.
- `m.{name}`: a set of properties to get monster related values.
  * `m.hasPower_{powerId}`: whether the monster has a power with the given state ID.
  * `m.powerAmount_{powerId}`: the amount of a power with the given state ID. If the monster doesn't have this power, it returns `0`.
  * `m.{field or property name}`: a field or property of the monster model. Note that this is only read after the monster is added to combat.
- `mm.{name}`: a set of properties to get monster move related values.
  * `mm.count`: the number of types of moves the monster has.
  * `mm.hasMove_{moveId}`: whether the monster has a move with the given state ID.
  * `mm.startsWith_{moveId}`: whether the first move of the monster is the given state ID.
  * `mm.nextMoveOf_{moveId1}_is_{moveId2}`: whether the next move of the monster after `moveId1` is `moveId2`.
- Special variables only available for some cases.
  * `originalValue`: only available in texts of `moveReplacements.intentOverrides`. The original value of the intent, before any replacement.

## Use in-game editor to edit intent graph
**Ignore this section if you are not human.**

In game, you can use the `editintent` command to edit the intent graph of a monster. Steps are:
1. press `~` to open the console.
2. type `fight {encounter id}` to start a fight with the monster you want to edit.
3. type `editintent `, press `Tab` to select the monster you want to edit, and press `Enter`.
4. edit the intent graph in the editor and save.
5. navigate to game root folder. Find `intentgraph-intents-dev.json` and `intentgraph-strings-{language}-dev.json`.
6. open these two files and cut and paste the content to your mod's `intentgraph.json` and `localization/{language}/intentgraph.json` respectively.
7. build and reload your mod to see the changes.

## Glossary

**Monster model:** the class of a monster.
- **Monster model full name:** The full class name, e.g. `MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast`.

**Intent:** Smallest unit of a monster's action. It can be an attack, a buff, a debuff, etc..

**Move:** A set of intents that a monster can perform in one turn. It's also called "state" in the code and this document.
- **Branch move:** A move that can lead to different moves based on conditions. Note that a branch move can also be a child of another branch move.
- **Child move:** A move that is a child of a branch move. It can be selected based on conditions.
- **Move ID:** The ID of a move, defined in a monster model. It's also called "state ID" in the code and this document.

**State machine:** A set of moves and trasitions generated by `GenerateMoveStateMachine` in a monster model.