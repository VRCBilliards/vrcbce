<p align="center">🗾 🇯🇵 -> https://github.com/FairlySadPanda/vrcbce/blob/master/README_ja.md</p>

<p align="center"><img src="https://avatars.githubusercontent.com/u/50210138?s=200&v=4" alt="Prefabs Logo"></p>

<p align="center"><i>This prefab could not have been constructed without the kind support of the Prefabs community. <3</i></p>

![Header](https://user-images.githubusercontent.com/6299186/129038087-3d152dde-86bf-44e6-8fa7-a660230ff01e.png)

A pool table for VRChat SDK3 worlds. Want to play 8 Ball, 9 Ball, or Japanese / Korean 4 Ball? This is the prefab for you! With the power of the Udon Networking Update, you can even have several tables in the same world without issue!

This prefab has no limitations in terms of its use. It can be:

- Placed anywhere in the scene!
- Have any rotation!
- Have any scale!
- Be on a rotating platform!
- Used repeatedly, within reason.
- Used on both PC and Quest worlds (note that this prefab can be a little CPU heavy on Quest)

It's also 100% free to modify, re-use and re-distribute. Make it your own!

If you'd like to get in touch with the repo maintainers:

[@FairlySadPanda](https://twitter.com/FairlySadPanda) on Twitter,
FairlySadPanda#9528 on Discord

[@Metamensa](https://twitter.com/Metamensa) on Twitter,
Metamaniac#3582 on Discord

# Installation

Requirements:

1. A project with the latest VRChat SDK3 Release installed in it
2. The project also has the latest [UdonSharp](https://github.com/MerlinVR/UdonSharp)
3. The project also has TextMeshPro installed in it.

Recommended:

1. [CyanEmu](https://github.com/CyanLaser/CyanEmu) for emulating locally
2. [VRWorldToolkit](https://github.com/oneVR/VRWorldToolkit) for general world development assistance

Installation Steps:

1. Download the latest release's unitpackage: https://github.com/FairlySadPanda/vrcbce/releases
2. Open the unitypackage in your VRChat world's scene.
3. Import all the assets.
4. In your Project folder, find "PoolTable" inside the VRCBilliards folder and drag-and-drop it into the scene.

# Getting Support

Unless it's urgent, please don't DM VRCBCE contributors asking for help!

The best way to get support is to create an Issue. You'll need a GitHub account for this, which takes less than a minute to set up.

Afterwards, click Issues at the top of this page:

![image](https://user-images.githubusercontent.com/732532/127752254-37061d3a-c13e-4de7-9212-792e17fe6472.png)

Then click Create Issue.

![image](https://user-images.githubusercontent.com/732532/127752268-c46fca03-72cf-4712-96b9-24e47764d791.png)

Afterwards, add your bug report or issue into the box and click Submit New Issue.

![image](https://user-images.githubusercontent.com/732532/127752457-03751bba-df2b-48f0-a220-a9cd699d9974.png)

DMing a contributor might get you a faster response, but writing an issue means that all contributors can see the issue, bugs can be tracked and referenced, and overall it's a lot easier to fix things!

# Known Unity-Related Bugs

Problem: The prefab has giant text!

Solution: Go into the TextMeshPro text component on each of the objects with giant text and scale down the text size (which has likely been reset to TMP's default of 36) to about 3. This will be fixed via not changing text size from the default in a future patch.

Problem: I upgraded to a newer version and players now collide with the table (or other "I upgraded and something broke!" issues)

Solution: Delete the version of the table in the scene and replace it with one of the prefab versions. Unity can be quite messy when upgrading prefabs - if you've recently updated, and something has broken, always see if it's an issue on a fresh version of the table first. Obviously, this is a little annoying to do if you've modified the prefab a lot: let us know if this is the case!

# Notes on "Community"

This prefab exists as a "Community Edition" of the original ht8b pool table. It simplifies the code a lot and makes it easier to edit. It is also provided under MIT, and the maintainers of this codebase commit to being open and inclusive to anyone who would like to modify the prefab, add additional modes, fix bugs, and use the prefab as a learning tool.

We encourage anyone to have a go modifying this prefab!

# Note on Pull Requests to this repo

The code in this project is written to look like normal Unity/C# code. C# has several standards (and teams tend to set their own) but for reference, refer to the Unity documentation, Unity example scripts, and [Microsoft's best practice guidelines](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions).

  Generally: 
  - Put variables at the top of the behaviour.
  - Avoid using underscore ahead of properties and methods unless it's a public method that needs to be non-RPCable for security reasons (an Udon-specific use of the underscore).
  - Use camelCase for properties and arguments, and use PascalCase for everything else.

# Original Creator

The original creator of this prefab was Harry_T. Harry_T did (unsuccessfully) attempt to DMCA this repo off of Github, but didn't realize that they were releasing the exact same assets on their own Github as public domain. They are currently MIA after nuking their Github/Twitter. Despite this, it's only fair to cite them as the original source, and pay credit to the impressive bit of physics code that drives this entire prefab. They also made a small contribution directly to this repo.

# UdonChips Integration

With 1.2.1, VRCBCE supports [UdonChips](https://lura.booth.pm/items/3060394)!

To enable UdonChips support, you need to do two things:

  1. Have the UdonChips UdonBehaviour in the project, with the object it's on called "UdonChips".
  2. Tick the "Enable UdonChips" option on your pool table's VRCBilliards object.

The VRCBilliards object, which contains the core PoolStateManager script, contains a number of options. At the moment, the following is supported:

  1. Paying UC to join a game of pool.
  2. If Allow Raising is enabled, you can pay to join multiple times - the more you pay in, the more the table can pay out!
  3. You can also earn UC for winning versus yourself.
  4. All costs and rewards are modifiable via the PoolStateManager script.
  5. The exact message to display on each join button is customizable in the PoolMenu script.

# Credits
🐼 FairlySadPanda - Maintainer, Lead Programmer, Networking, Refactoring

😺 Table - Maintainer, Designer, Optimization, General polish

✨ esnya - UI, UdonChips implementation, misc. fixes

🌙 M.O.O.N - UI

🌳 Ivylistar - Metal Table

🚗 Varneon - Optimization

🧙‍♂️ Xiexe - Original Forker, Early refactor work

🧙‍♀️ Silent - [Filamented](https://gitlab.com/s-ilent/filamented)

💻 Vowgan & Legoman99573 - Misc. commits

harry_t - Original Prefab, Physics code
