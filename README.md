# VRCBilliards: Community Edition (Secret pre-release version!)

This is an UNFINISHED and PROBABLY BUGGY adaption of the old VRCBilliards prefab, created by Harry_T and forked/maintained for a while by Xiexe, to the now Udon Reliable Sync ("Spring Networking Patch") version.

If you're here you're presumably curious and would like to take a look. Pull the repo and have fun!

Note that this version (0.1.0) has NO OFFICIAL RELEASE VERSION in the Github page. This commit, however, will be tagged as 0.1.0 for future reference.

This repo will have no official releases until the VRC Spring Networking Patch hits the real Open Beta.

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

Rule 2: Be OK with feedback on your PR and change it if requested! (It's surprising that this needs to be said.)
