# Changelog

Full changelog available via [Github Commits](https://github.com/keifufu/WebNowPlaying-Rainmeter/commits/main)

## v3.0.0

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/3.0.0)

- Updated to new [WebNowPlaying-Library](https://github.com/keifufu/WebNowPlaying), resulting in:
  - Ability to access all open players, instead of just the active one
  - Fixed a bunch of desktop player related issues
  - Downloaded cover images are always in png format
  - Cover images will no longer fail to download
- Added and renamed some bangs and measures, see the [docs](https://wnp.keifufu.dev/rainmeter/usage) for more information.

## v2.0.7

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/2.0.7)

- Added `Play` and `Pause` bangs.
- Fixed Firefox not being ignored when Native APIs are enabled on Windows 10.

## v2.0.6

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/2.0.6)

- `PlayerType=Progress` defaults to 0 instead of 100
- Fix crashes ([#8](https://github.com/keifufu/WebNowPlaying-Rainmeter/issues/8) and [this](https://discord.com/channels/148103787259756544/148718731743199233/1130459707576946748) in the [Rainmeter Discord](https://discord.gg/rainmeter))

## v2.0.5

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/2.0.5)

- Fixed Int32 overflow causing `SetPosition` not being able to set the position past a few minutes for desktop players

## v2.0.4

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/2.0.4)

- Improved optimistic position updates, which makes the position skip less when using desktop players.
  This also fixed [#5](https://github.com/keifufu/WebNowPlaying-Rainmeter/issues/5)

## v2.0.3

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/2.0.3)

- Fixed covers not saving for skins using `CoverPath=`, which the majority of outdated skins use.

## v2.0.2

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/2.0.2)

- Removed Spotify watermark from covers on Windows 10

## v2.0.1

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/2.0.1)

- Updated WNPReduxAdapterLibrary to 2.0.1 ([Read Changelog](https://github.com/keifufu/WNPRedux-Adapter-Library/releases/tag/2.0.1))
- Fixed position updating sluggishly in certain cases (Read Issue [#2](https://github.com/keifufu/WebNowPlaying-Rainmeter/issues/2))

## v2.0.0

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/2.0.0)

- Added support for Desktop Players
- Updated WNPReduxAdapterLibrary to 2.0.0 ([Read Changelog](https://github.com/keifufu/WNPRedux-Adapter-Library/releases/tag/2.0.0))
- Added new measures. Please read the updated usage instructions for reference.
- Bumped minimum supported version to windows 10.

## v1.2.0

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.2.0)

- Added `PlayerType=Remaining` measure, returns remaining time in (hh):mm:ss
- Update Example Skin to include updated instructions/comments and add example of the Remaining measure

## v1.1.7

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.1.7)

- Fix `SetPosition` bang not working with doubles for certain languages

## v1.1.6

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.1.6)

- Update WNPReduxAdapterLibrary to 1.0.7, fixing `PlayerType=Progress` returning either 0 or 1, instead of a double.

## v1.1.5

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.1.5)

- Update WNPReduxAdapterLibrary to 1.0.6, fixing WebNowPlaying being unable to connect after loading a new layout.

## v1.1.4

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.1.4)

- Potential fix for issues with file access when downloading cover art
- Update WNPReduxAdapterLibrary to 1.0.5
- Improved log messages

## v1.1.3

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.1.3)

- Fixed more issues with covers not downloading/returning correctly

## v1.1.2

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.1.2)

- Fixed `CoverWebAddress` updating before the cover art finished downloading

## v1.1.1

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.1.1)

- Fixed random crash

## v1.1.0

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.1.0)

- Fixed Rainmeter hanging while cover art is downloading
- Images are now saved to all paths registered with CoverPath= for backwards compatibility
- CoverPath= should no longer be used if possible, please read the path from the PlayerType=Cover measure

## v1.0.6

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.0.6)

- Fixed `PlayerType=Cover` returning a path with forwards-slashes

## v1.0.5

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.0.5)

- Can now download covers from urls with a permanent redirect (HTTP 308)

## v1.0.4

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.0.4)

- Fixed a crash that would occur when the extension returns a invalid cover URL.

## v1.0.3

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.0.3)

- Updated to WNPReduxAdapterLibrary 1.0.3, which fixed an edgecase for mediaInfo not reflecting the currently playing media.

## v1.0.2

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.0.2)

- Fix cover art only downloading to windows temp directory

## v1.0.1

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.0.1)

- Fixed cover switching incorrectly when new skins loaded

## v1.0.0

[Go to Release](https://github.com/keifufu/WebNowPlaying-Rainmeter/releases/tag/1.0.0)

- Initial Redux Release

## v0.5.0

Just some general fixes with downloading cover art from sites that only support certain sites.
Also added the version check support that I thought I had added before but I guess I did not

## v0.4.0

This version is a complete rewrite of the extension since the last version. This rewrite should make it easier to keep the extension up to date and also merged both browser extensions to a single codebase.

New features include:
Removed unneeded APIs
Fixed issue with icons in example skin.
More sites supported.
A generic site supporter that when enabled will try to support sites without explicit support such a streamable or reddit. (Note: wont capture elements inside a iframe yet)
Version checking, extension will now notify you when the plugin is out of date.
Settings for the companion extension, they are not all implemented yet since the projects they rely on are unreleased.

To update simply install the rmskin down below.

## v0.3.0

Now the extensions support quite a few more sites as well as setting the position and volume of the meter.

The Spotify API is also now being used to get the album and album art of the current song. In the future this API as well as several others such as Twitch and Youtube will be added to make Rainmeter skin makers lives easier.

Since this is now integrated into the official Monstercat Visualizer and we are halfway between releases I am only including the rmskin of the example skin.

## v0.2.5

First release of the plugin.
You will need to install either the chrome or firefox companion for this to work.

Given that this is an early release I expect regional variants or minor issues with the various supported websites. If any information looks wrong or isn't working please report it. (Check that Rainmeter has a firewall exception though if nothing is working :P )

The current list of supported sites is:
Youtube
Twitch
Soundcloud
Google Play Music
Amazon Music

The standard bangs and info are supported and follow a NowPlaying style, for now just look at the example until I get documentation written which will come once SetPosition and SetVolume are supported.
