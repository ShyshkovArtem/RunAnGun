# Run&Gun

Run&Gun is a fast-paced first-person movement and weapon-platforming game built with Unity. Combine precise movement with weapon recoil and explosive force to cross obstacle courses, complete trials, and chase faster times.

The repository currently contains a playable vertical slice featuring the core movement, shooting, progression, checkpoint, timing, and ranking systems.

[Download the latest Windows build](https://github.com/ShyshkovArtem/RunAnGun/releases/latest) · [Report a bug](https://github.com/ShyshkovArtem/RunAnGun/issues) · [Read the game design document](./Run%26Gun_GDD.pdf)

## Features

- Fast first-person movement with sprinting, crouching, sliding, and momentum
- Four weapons with distinct handling and movement interactions
- Weapon switching, ammunition, reloading, impacts, and visual effects
- Movement and weapon-focused tutorial trials
- Checkpoints, falling recovery, and level retries
- Tutorial dialogue and guided challenges
- Timed Final Test with persistent best times and rank requirements
- Main menu, level selection, progress display, and pause/final screens
- Shared timing definitions that keep gameplay and level-select ranks synchronized

## Controls

| Action | Input |
| --- | --- |
| Move | `W` `A` `S` `D` or Arrow Keys |
| Look | Mouse |
| Jump | `Space` |
| Sprint | `Left Shift` |
| Crouch / Slide | `Left Ctrl` or `C` |
| Fire | Left Mouse Button |
| Reload | `R` |
| Select weapon | `1`–`4` or Mouse Wheel |
| Advance dialogue | `Enter` |
| Pause / Resume | `Escape` |

## Play the vertical slice

1. Open the [GitHub Releases](https://github.com/ShyshkovArtem/RunAnGun/releases) page.
2. Download the latest Windows x64 ZIP.
3. Extract the entire archive to a folder.
4. Run `Run&Gun.exe`.

Keep the executable, `Run&Gun_Data`, `MonoBleedingEdge`, `D3D12`, and `UnityPlayer.dll` together. Windows SmartScreen may display a warning because development builds are not code-signed; only run builds downloaded from this repository's official Releases page.

## Development setup

### Requirements

- Unity `6000.3.10f1`
- Unity Hub
- Git
- Git LFS
- A Windows development environment is recommended for producing the current release target

### Open the project

```bash
git lfs install
git clone https://github.com/ShyshkovArtem/RunAnGun.git
cd RunAnGun
git lfs pull
```

Add the cloned directory through Unity Hub and open it with Unity `6000.3.10f1`. Opening it with a different editor version may upgrade serialized assets and create a large, unrelated diff.

Unity restores the packages declared in `Packages/manifest.json` when the project opens. The project uses URP, the Input System, TextMesh Pro, Cinemachine, ProBuilder, and other Unity packages.

## Repository structure

```text
Assets/
├── RunGun/
│   ├── Art/          Game-specific visual assets
│   ├── Audio/        Music and sound effects
│   ├── Editor/       Project editor utilities
│   ├── Font/         TextMesh Pro font assets
│   ├── Levels/       Scenes and level controller prefabs
│   ├── Resources/    Runtime-loaded configuration assets
│   └── Scripts/      Gameplay, level, weapon, and UI code
├── ...               Licensed third-party packages and assets
Packages/              Unity package manifest and lock file
ProjectSettings/       Unity project configuration
```

The editable game design document is available as [DOCX](./Run%26Gun_GDD.docx), with a convenient [PDF version](./Run%26Gun_GDD.pdf).

## Building

The current public build targets Windows x64.

1. Open the project in the required Unity version.
2. Open Unity's Build Profiles window.
3. Select the Windows profile and x86-64 architecture.
4. Confirm that the intended menu and trial scenes are enabled.
5. Build into a clean directory outside the tracked project assets.

For distribution, include the generated executable, data folder, Unity runtime files, `MonoBleedingEdge`, and `D3D12`. Exclude `Run&Gun_BurstDebugInformation_DoNotShip` from public archives.

## Project status

Run&Gun is in active development. The available build is a vertical slice, not a finished release. Features, balance, visuals, audio, level content, saved data, and hardware requirements may change.

Bug reports and playtest feedback are welcome through [GitHub Issues](https://github.com/ShyshkovArtem/RunAnGun/issues). Helpful reports include reproduction steps, the affected level, Windows version, hardware details, and screenshots or video.

## Credits and licensing

Created by Artem Shyshkov with Unity.

This repository contains third-party fonts, art, audio, animations, visual effects, tools, and other assets used under their respective licenses. Those assets remain the property of their original creators and licensors.

No repository-wide open-source license is currently provided. Public access to the source does not grant permission to redistribute the project or its third-party assets outside the terms of their applicable licenses.
