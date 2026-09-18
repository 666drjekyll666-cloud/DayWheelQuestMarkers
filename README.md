# Day Wheel Quest Markers

A lightweight QoL/UI mod for **Graveyard Keeper 1.407**.

**Day Wheel Quest Markers** adds small quest/reminder markers to the existing weekday wheel when the player's current state has an **actionable interaction** with an NPC who appears on a specific day.

## Download

Stable builds are available from [GitHub Releases](https://github.com/666drjekyll666-cloud/DayWheelQuestMarkers/releases).

## How it works

A day is not marked merely because an unfinished quest belongs to an NPC. A marker appears only when the current state actually allows a relevant weekday-NPC interaction.

This includes both actionable quest steps and authored one-time dialogue topics that are currently available. Missing items, crafting steps, insufficient quality or relationship requirements, exploration steps, and other unmet prerequisites do not create reminders. Repeatable utility/menu choices such as Trade, Leave, Back, or non-consuming submenu headers are not treated as reminders. Follow-up choices that are reachable only after completing an already-reminded interaction are treated as part of the same NPC visit rather than as extra reminders.

Unsupported structures fail closed rather than producing a misleading marker.

## Features

- Supports the six vanilla weekday NPCs: Astrologer, Inquisitor, Snake, Merchant, Ms. Charm, and Bishop.
- Handles normal NPC-owned objectives and verified cross-owner objectives.
- Detects currently available authored one-time weekday-NPC conversations without requiring them to complete a visible journal task.
- Covers verified intermediate and bridge progression cases that are not represented by a simple owner-local task-completion route.
- Preserves the game's quest-marker categories and colors where the interaction has a known task category.
- Shows separate markers for multiple simultaneous independent actionable interactions on the same weekday, without double-counting follow-up choices from the same NPC visit.
- Follows the wheel as weekday symbols rotate.
- Survives normal HUD/menu hide and recreation without duplicating markers.
- Keeps expensive quest/dialogue graph setup out of normal gameplay updates.

## Requirements

- Graveyard Keeper 1.407
- BepInEx 5.x

## Installation

1. Download the DLL from GitHub Releases.
2. Copy it into `Graveyard Keeper/BepInEx/plugins/`.
3. Restart the game.

## Performance

Normal gameplay work is bounded and low-frequency. Quest/dialogue structure is cached during loading rather than reparsed continuously, fresh saves with no weekday NPCs stay on a cheap path, and marker objects are reused.
