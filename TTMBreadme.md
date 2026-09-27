# BayTurtle's Ability Loader

This mod lets you load and use custom abilities loaded via .turtability files. These files contain raw, uncompiled C-Sharp and can run any code.

The mod will compile the abilities at runtime and add them to UC. This may make the loading screen take longer if you have many abilities.

This is **not** a shared library. This is just for sharing and making custom ability files.

## Why?

This mod was made because all previous ability creators used block code. However, I do not like block coding and I prefer text coding.

I myself have been using this to test custom abilities before I add them to an official version of **Mobility Abilities** (another mod I made), and I decided to make it public.

## How can I make an ability?

To make an ability, you must know how to program in C#. There are example/preset abilities provided in every install. Every built-in example will have easily-findable variables to allow you to customize them!

## Where do .turtability files go?

If you are using a mod manager like R2modman, you can go:

1. into the settings tab
2. click *"Browse"* next to *"Profile Folder"*
3. enter the *"BepInEx" folder*
4. enter the *"Plugins" folder*
5. and finally enter the *"TurtAbilities" folder*.

This is where your abilities will go.

 Example/built-in ability files can be found and edited under the same *"Plugins"* folder but in the *"BayTurtleKing-Turtle_Ability_Loader"* subfolder instead. If you are planning on customizing them, I recommend moving them to the custom ability folder so they don't get overwritten on an update.
