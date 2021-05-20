![Prefabs Logo](https://avatars.githubusercontent.com/u/50210138?s=200&v=4)

_This prefab could not have been constructed without the kind support of the Prefabs community. <3_

# VRCBilliards: Community Edition

![Image of the table](https://i.imgur.com/cLoMK2p.png)

A pool table for VRChat SDK3 worlds. Want to play 8 Ball, 9 Ball, or Japanese or Korean 4 Ball? This is the prefab for you! With the power of the Udon Networking Update, you can even have several tables in the same world without issue!

This prefab has no limitations in terms of its use. It can be:

- Placed anywhere in the scene!
- Have any rotation!
- Have any scale!
- Be on a rotating platform!
- Used repeatedly, within reason.
- Used on both PC and Quest worlds (note that this prefab can be a little CPU heavy on Quest)

It's also 100% free to modify, re-use and re-distribute. Make it your own!

If you'd like to get in touch with the repo maintainer:

@FairlySadPanda on Twitter
FairlySadPanda#9528 on Discord

# Installation

Requirements:

1. A project with the latest VRChat SDK3 Release installed in it
2. The project also has the latest UdonSharp (https://github.com/MerlinVR/UdonSharp)
3. The project also has TextMeshPro installed in it.

Recommended:

1. CyanEmu for emulating locally (https://github.com/CyanLaser/CyanEmu)
2. VR World Toolkit for general world development assistance (https://github.com/oneVR/VRWorldToolkit)

Installation Steps:

1. Download the latest release's unitpackage: https://github.com/FairlySadPanda/vrcbce/releases
2. Open the unitypackage in your VRChat world's scene.
3. Import all the assets.
4. In your Project folder, find "PoolTable" inside the VRCBilliards folder and drag-and-drop it into the scene.

# Notes on "Community"

This is a full, history-free fork of the Harry_T 8Ball prefab. This project is an alternative to that prefab, but is not a competitor. This prefab exists as a Community Edition which simplifies the code a lot and makes it easier to edit. It is also provided under MIT, and the maintainers of this codebase commit to being open and inclusive to anyone who would like to modify the prefab, add additional modes, fix bugs, and use the prefab as a learning tool.

We encourage anyone to have a go modifing this prefab.

# Note on Pull Requests to this repo

The code in this project is written to look like normal Unity/C# code. C# has several standards (and teams tend to set their own) but for reference, refer to the Unity documentation, Unity example scripts, and Microsoft's best practice [https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions](here). Generally, put variables at the top of the behaviour, avoid using underscore ahead of properties and methods unless it's a public method that needs to be non-RPCable for security reasons (a Udon-specific use of the underscore)), use camelCase for properties and arguments, and use PascalCase for everything else.
