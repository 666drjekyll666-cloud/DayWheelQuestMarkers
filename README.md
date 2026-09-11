# Day Wheel Quest Markers

A lightweight QoL/UI mod for **Graveyard Keeper 1.407**.

**Day Wheel Quest Markers** adds small quest markers to the existing weekday wheel when the player's **current actionable objective** requires interaction with an NPC who appears on a specific day.

## How it works

A day is not marked merely because an unfinished quest belongs to an NPC. A marker appears only when the current progression state actually allows the relevant weekday-NPC interaction. Missing items, crafting steps, insufficient quality or relationship requirements, exploration steps, and other unmet prerequisites do not create reminders.

The mod follows Graveyard Keeper's own dialogue and resource checks where those paths are verified, and deliberately prefers a missing marker over a misleading one for unsupported quest structures.

## Features

- supports the six vanilla weekday NPCs: Astrologer, Inquisitor, Snake, Merchant, Ms. Charm, and Bishop;
- handles normal NPC-owned objectives and verified cross-owner objectives;
- covers the verified Miller → Astrologer mill-calculation and Astrologer → Snake instrument bridge chains;
- preserves the game's quest-marker categories and colors;
- shows separate markers for multiple simultaneous actionable objectives on the same weekday;
- follows the wheel as weekday symbols rotate;
- survives normal HUD/menu hide and recreation without duplicating markers;
- keeps heavy quest-graph parsing behind the loading screen on developed saves.

## Requirements

- Graveyard Keeper 1.407
- BepInEx 5.x

## Installation

Copy `DayWheelQuestMarkers.dll` into:

`Graveyard Keeper/BepInEx/plugins/`

Restart the game.

## Performance

Normal gameplay work is bounded and low-frequency. Quest graph structure is cached rather than reparsed continuously, developed-save cache construction is prewarmed during loading, fresh saves with no weekday NPCs stay on a cheap path, and marker objects are reused.

## Status

**1.0.21 candidate** preserves the accepted 1.0.17 quest/reminder logic while changing only how the marker artwork is obtained: the mod now resolves and caches the game's already-loaded native marker sprites during the loading window instead of carrying its own pixel copies. This candidate requires a short in-game regression test before becoming the stable public release.
