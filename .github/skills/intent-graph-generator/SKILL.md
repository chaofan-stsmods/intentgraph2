---
name: intent-graph-generator
description: Generates intent graphs based on monster model and user input. Use this skill when asked to generate an intent graph.
---

1. Ask the user for the monster name if not provided. It should be a class name that inherits from `MonsterModel` and contains method `GenerateMoveStateMachine`.
2. Find mod ID which will be used in the next step. Mod ID is usually name of the current folder. In the folder there should be a folder named exactly the same as the mod ID. There may be a `{modid}.json` file in current folder with `id` field that is also the mod ID. If you are not sure about the mod ID, ask the user to confirm it.
3. Reference `how-to-add-intent-graph.md` for how to define the intent graph. Ask the user for what they need and choose the right method to generate the graph. When adding texts, it is recommeneded to also add localization. Valid language codes can be found below.
4. State IDs can be acquired from `GenerateMoveStateMachine` method. It's usually the first argument of State constructor. You may ask the user if you are not sure.
5. Check the generated or updated `intentgraph.json` file, make sure it's still a valid JSON.

Valid language codes: eng, zhs, deu, esp, fra, ita, jpn, kor, pol, ptb, rus, spa, tha, tur.

**important**:
Don't change files except `intentgraph.json`.
