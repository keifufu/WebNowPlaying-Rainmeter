# Rainmeter Adapter for WebNowPlaying-Redux
A Rainmeter Adapter for [WebNowPlaying-Redux](https://github.com/keifufu/WebNowPlaying-Redux).

Allows for retrieval of media information and playback control from various websites such as spotify, soundcloud or youtube. See all supported websites [here](https://github.com/keifufu/WebNowPlaying-Redux).

Included in this repo is an example skin that shows how to use every measure and bang.

## Measure Types
These are declared using `PlayerType=`
Name | Default | Description
--- | --- | ---
`Player` | "" | Current player, e.g. YouTube, Spotify, etc.
`Status` | 0 | 0 = No supported websites are connected; 1 = one or more supported websites are connected;
`State` | 0 | Current state of the player (0 = STOPPED, 1 = PLAYING, 2 = PAUSED) 
`Title` | "" | Title
`Artist` | "" | Artist
`Album` | "" | Album
`Cover` | "" | Description too long; read below this table.
`CoverWebAddress` | "" | URL of the current cover image, useful for doing an onChangeAction as cover will update twice when when the media changes. This will only update once and only once the image has been downloaded to the disk.
`Duration` | "0:00" | Duration in (hh):mm:ss (Hours are optional)
`Position` | "0:00" | Position in (hh):mm:ss (Hours are optional)
`Remaining` | "0:00" | Position in (hh):mm:ss (Hours are optional)
`Progress` | 0.0 | Position in percent. To clarify it's formatted ##.##### and has a predefined max of 100.00
`Volume` | 100 | Volume from 1-100
`Rating` | 0 | Rating from 0-5; Thumbs Up = 5; Thumbs Down = 1; Unrated = 0;
`Repeat` | 0 | Current repeat state (0 = NONE, 1 = ONE, 2 = ALL)
`Shuffle` | 0 | If shuffle is enabled (0 = false, 1 = true)

### `Cover`
String returning the path to the current cover art, or the path to the default cover if none is found.  
**Note:** Do not assume the image will always be a square. It won't in most cases.  
**Attributes:**  
DefaultPath - A system path to what image to use as fallback.  
CoverPath (**Legacy, do not use**) - A system path where the cover image is stored.

## Bangs
Name | Parameters | Description
--- | --- | ---
`PlayPause` | none | Toggles the playing state
`Next` | none | Skips to the next media/section
`Previous` | none | Skips to the previous media/section
`SetPosition` | double (##.####) | Sets the medias playback position in percent from 1-100. Add + or - in front to set the position relatively.
`SetVolume` | int | Set the medias volume from 1-100. Add + or - in front to set the position relatively.
`Repeat` | none | Toggles through repeat modes
`Shuffle` | none | Toggles shuffle mode
`ToggleThumbsUp` | none | Toggles thumbs up or similar
`ToggleThumbsDown` | none | Toggles thumbs down or similar
`SetRating` | int | Sites with a binary rating system fall back to: 0 = None; 1 = Thumbs Down; 5 = Thumbs Up

## Building from Source
You will need to open 'Turn Windows features on or off' and ensure that .NET Framework 3.5 is enabled, as pictured [here](https://oldimg.noonly.net/06BR2GT605.jpg).
