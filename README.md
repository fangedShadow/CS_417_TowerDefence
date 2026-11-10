# VR Tower Defence

A three-person Unity project about defending a military base using Army, Navy,
and Air Force structures. The original vision is in
[Docs/VisionDesignDocument.txt](Docs/VisionDesignDocument.txt).

## Getting started

1. Install Git LFS and run `git lfs install` before cloning. The existing
   `.gitattributes` stores binary art and audio using LFS.
2. Clone the repository, then run `git lfs pull` from the project folder.
3. Open the project in Unity Hub using **Unity 6000.5.6f1**. Let Unity restore
   the packages and import assets.
4. Open `Assets/Scenes/Main.unity`, the shared scene and build entry point.

OpenXR and XR Interaction Toolkit are already included. This setup retains the
basic camera and light; an XR Origin, controller bindings, and headset-specific
XR settings still need to be configured once the team chooses its target
headset and platform. There is no gameplay implementation yet.

## Who works where

| Owner | Faction | Scripts | Prefabs |
| --- | --- | --- | --- |
| Billy Lu | Army | `Assets/Scripts/Army/` | `Assets/Prefabs/Army/` |
| Bhavpreet Singh | Navy | `Assets/Scripts/Navy/` | `Assets/Prefabs/Navy/` |
| Jackson Drape | Air Force (Sky in the design doc) | `Assets/Scripts/AirForce/` | `Assets/Prefabs/AirForce/` |
| Team | Shared systems | `Assets/Scripts/Shared/` | `Assets/Prefabs/Shared/` |

Each faction will develop Attack Towers, Defense Towers, Support Towers, and
Buildings. Keep faction assets in that faction's prefab folder; create additional
subfolders when they are needed. Agree on shared interfaces before adding code
that another faction depends on.

## Working together in one scene

- Each faction has an empty root prefab already placed in `Main`. Double-click
  your faction's root prefab to edit it in Prefab Mode. Add faction objects as
  child prefabs there so the work is saved outside the shared scene.
- Build individual structures as reusable prefabs. Use names such as
  `Army_AttackTower`, `Navy_SupportTower`, and `AirForce_Building`.
- Coordinate one scene editor at a time for `Main.unity`. Tell the team before
  changing shared map objects, lighting, cameras, XR setup, or scene placement.
  Coordinate edits to shared scripts and project/package settings too.
- Use a short feature branch per task, such as `army/attack-tower`. Save in
  Unity, review the diff, commit, and merge through a pull request. Pull the
  latest shared branch before starting new work.
- Move and rename assets inside Unity, and commit their `.meta` files together
  with them. These files preserve references for everyone else.
- Commit `Assets/`, `Packages/`, and `ProjectSettings/`. Unity uses Force Text
  serialization and Visible Meta Files for Git. Generated caches, build outputs,
  IDE project files, and personal settings are ignored.

The existing `.gitattributes` names the `unityyamlmerge` driver. Each developer
must configure that driver locally to use Unity Smart Merge; it is not enabled
by the attributes alone. Coordinate scene ownership even when using Smart Merge,
and resolve asset conflicts in Unity before merging.
