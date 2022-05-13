<p align="center"><a href="https://github.com/VRCBilliards/vrcbce/blob/master/README_es.md">🇲🇽 Spanish 🇪🇸</a> &nbsp;&nbsp;&nbsp;&nbsp;&nbsp; <a href="https://github.com/VRCBilliards/vrcbce/blob/master/README_ja.md">🇯🇵 Japanese 🇯🇵</a></p>

<p align="center"><img src="https://avatars.githubusercontent.com/u/50210138?s=200&v=4" alt="Prefabs Logo"></p>

<p align="center"><i>This prefab could not have been constructed without the kind support of the Prefabs community. <3</i></p>

![Header](https://user-images.githubusercontent.com/6299186/136136789-f195e2ef-0cce-4807-8313-f62c39159b2f.png)

A pool table for VRChat SDK3 worlds. Want to play 8 Ball, 9 Ball, or Japanese / Korean 4 Ball? This is the prefab for you! With the power of the Udon Networking Update, you can even have several tables in the same world without issue!

This prefab exists as a "Community Edition" of the original ht8b pool table. It simplifies the code a lot and makes it easier to edit. It is also provided under MIT, and the maintainers of this codebase commit to being open and inclusive to anyone who would like to modify the prefab, add additional modes, fix bugs, and use the prefab as a learning tool. We **strongly** encourage anyone to have a go modifying and/or contributing to this prefab!

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

1. [Download the latest release's unitypackage](https://github.com/VRCBilliards/vrcbce/releases/latest).
2. Import the unitypackage.
3. Inside the VRCBilliardsCE folder, select any of the table prefabs and drag-n-drop it into the scene.
4. Profit!

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

# Making Pull Requests to this repo

The code in this project is written to look like normal Unity/C# code. C# has several standards (and teams tend to set their own) but for reference, refer to the Unity documentation, Unity example scripts, and [Microsoft's best practice guidelines](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions).

  Generally: 
  - Put variables at the top of the behaviour.
  - Avoid using underscore ahead of properties and methods unless it's a public method that needs to be non-RPCable for security reasons (an Udon-specific use of the underscore).
  - Use camelCase for properties and arguments, and use PascalCase for everything else.

# Crediting in your Worlds

As VRCBCE is a complex multi-layered project, we've recieved help on it from many fronts over the span of its development. As the amount of people who contribute to this project increases, it is only fair to everyone involved that the group as a whole gets representation when being credited in worlds. The easiest and most inclusive way to credit us would be to credit the organization "VRCBilliards", or perhaps you could write "VRCBCE Team" or something. If you ABSOLUTELY insist on naming names: "FairlySadPanda & Table" works as a bare minimum, and it is **highly recommended** that you also credit the creator of the table mesh you are using as well (the name of the UI's creator is already included in the info section of each table, but it can't hurt to re-credit them as well!). Other contributors to credit can be found in the credits below.

# Original Creator

The original creator of this prefab was harry_t. harry_t did (unsuccessfully) attempt to DMCA this repo off of Github, but didn't realize that they were releasing the exact same assets on their own Github as public domain. They are currently MIA after nuking their Github/Twitter. Despite this, it's only fair to cite them as the original source, and pay credit to the impressive bit of physics code that drives this entire prefab. They also made a small contribution directly to this repo.

# Credits
🐼 FairlySadPanda - Maintainer, Lead Programmer, Networking, Refactoring

😺 Table - Maintainer, Designer, Optimization, General polish, QA

✨ esnya - UI, UdonChips implementation, misc. fixes

🌙 M.O.O.N - UI

🌳 Ivylistar - Metal Table

🦊 Juice - CottonFox Table

🦈 akalink - Classic Table, UI, Color Change shaders

🚗 Varneon - Optimization

🧙‍♂️ Xiexe - Original Forker, Early refactor work

🧙‍♀️ Silent - [Filamented](https://gitlab.com/s-ilent/filamented)

🎨 Floatharr & Synergiance - Textures

💻 Vowgan & Legoman99573 - Misc. commits

harry_t - Original Prefab, Physics code
