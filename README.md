# Day Wheel Quest Markers

A lightweight QoL/UI mod for **Graveyard Keeper 1.407**.

**Day Wheel Quest Markers** adds small quest markers to the existing weekday wheel when the player's **current actionable objective** requires interaction with an NPC who appears on a specific day.

## Download

Stable builds are available from [GitHub Releases](https://github.com/666drjekyll666-cloud/DayWheelQuestMarkers/releases).

## How it works

A day is not marked merely because an unfinished quest belongs to an NPC. A marker appears only when the current progression state actually allows the relevant weekday-NPC interaction.

Missing items, crafting steps, insufficient quality or relationship requirements, exploration steps, and other unmet prerequisites do not create reminders. Unsupported quest structures fail closed rather than producing a misleading marker.

## Features

- Supports the six vanilla weekday NPCs: Astrologer, Inquisitor, Snake, Merchant, Ms. Charm, and Bishop.
- Handles normal NPC-owned objectives and verified cross-owner objectives.
- Covers the Miller → Astrologer mill-calculation and Astrologer → Snake instrument bridge chains.
- Preserves the game's quest-marker categories and colors.
- Shows separate markers for multiple simultaneous actionable objectives on the same weekday.
- Follows the wheel as weekday symbols rotate.
- Survives normal HUD/menu hide and recreation without duplicating markers.
- Keeps expensive quest-graph setup out of normal gameplay updates.

## Requirements

- Graveyard Keeper 1.407
- BepInEx 5.x

## Installation

1. Download `Day Wheel Quest Markers 1.0.21.dll` from GitHub Releases.
2. Copy it into `Graveyard Keeper/BepInEx/plugins/`.
3. Restart the game.

## Performance

Normal gameplay work is bounded and low-frequency. Quest structure is cached rather than reparsed continuously, fresh saves with no weekday NPCs stay on a cheap path, and marker objects are reused.
