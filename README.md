# VRCBilliards: Community Edition

A pool table for VRChat SDK3 worlds.

If you'd like to get in touch with the repo maintainer:

@FairlySadPanda on Twitter
FairlySadPanda#9528 on Discord

# Notes on "Community"

This is a full, history-free fork of the Harry_T 8Ball prefab. This project is an alternative to that prefab, but is not a competitor. This prefab exists as a Community Edition which simplifies the code a lot and makes it easier to edit. It is also provided under MIT, and the maintainers of this codebase commit to being open and inclusive to anyone who would like to modify the prefab, add additional modes, fix bugs, and use the prefab as a learning tool.

We encourage anyone to have a go modifing this prefab.

# Note on Pull Request Standards

There are only two rules for pull request to this repo:

Rule 1: The code in this project is written to look like normal Unity/C# code. C# has several standards (and teams tend to set their own) but for reference, refer to the Unity documentation, Unity example scripts, and Microsoft's best practice [https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions](here). Generally, put variables at the top of the behaviour, avoid using underscore ahead of properties and methods unless it's a public method that needs to be non-RPCable for security reasons (a Udon-specific use of the underscore)), use
camelCase for properties and arguments, and use PascalCase for everything else.

Rule 2: Be aware you might be asked to make small or large changes to have code accepted onto this repo. If you'd like to diverge, feel free to fork.
