#include "wnp.h"
#include <stdio.h>
#include <windows.h>

void __cdecl LSLog(int levle, LPCWSTR unused, LPCWSTR message);
void __stdcall RmLog(void* rm, int level, LPCWSTR message);
void __cdecl RmLogF(void* rm, int level, LPCWSTR message, ...);
LPCWSTR __stdcall RmReadString(void* rm, LPCWSTR option, LPCWSTR default_value, BOOL replace_measures);
LPCWSTR __stdcall RmPathToAbsolute(void* rm, LPCWSTR relativePath);
__inline LPCWSTR RmReadPath(void* rm, LPCWSTR option, LPCWSTR defValue)
{
  LPCWSTR relativePath = RmReadString(rm, option, defValue, TRUE);
  return RmPathToAbsolute(rm, relativePath);
}
enum LOGLEVEL { LOG_ERROR = 1, LOG_WARNING = 2, LOG_NOTICE = 3, LOG_DEBUG = 4 };

#ifndef WNPRAINMETER_VERSION
#define WNPRAINMETER_VERSION "0.0.0"
#endif

int MEASURE_COUNT = 0;
#define MAX_LEGACY_COVER_PATHS 64
char* LEGACY_COVER_PATHS[MAX_LEGACY_COVER_PATHS] = {0};
char LAST_TITLE[WNP_STR_LEN];
int g_last_updated = 1;

enum PLAYER_TYPE {
  PT_STATUS,
  PT_PLAYER_COUNT,
  PT_NAME,
  PT_TITLE,
  PT_ARTIST,
  PT_ALBUM,
  PT_COVER,
  PT_COVER_SRC,
  PT_STATE,
  PT_POSITION,
  PT_DURATION,
  PT_REMAINING,
  PT_POSITION_PERCENT,
  PT_VOLUME,
  PT_RATING,
  PT_REPEAT,
  PT_SHUFFLE,
  PT_RATING_SYSTEM,
  PT_AVAILABLE_REPEAT,
  PT_CAN_SET_STATE,
  PT_CAN_SKIP_PREVIOUS,
  PT_CAN_SKIP_NEXT,
  PT_CAN_SET_POSITION,
  PT_CAN_SET_VOLUME,
  PT_CAN_SET_RATING,
  PT_CAN_SET_REPEAT,
  PT_CAN_SET_SHUFFLE,
  PT_CREATED_AT,
  PT_UPDATED_AT,
  PT_ACTIVE_AT,
  PT_IS_WEB_BROWSER,
  PT_PLATFORM,
};

typedef struct {
  const wchar_t* name;
  enum PLAYER_TYPE type;
} PlayerTypeMapping;

PlayerTypeMapping playerTypeMappings[] = {
    {L"status", PT_STATUS},
    {L"playercount", PT_PLAYER_COUNT},
    {L"name", PT_NAME},
    {L"title", PT_TITLE},
    {L"artist", PT_ARTIST},
    {L"album", PT_ALBUM},
    {L"cover", PT_COVER},
    {L"coversrc", PT_COVER_SRC},
    {L"state", PT_STATE},
    {L"position", PT_POSITION},
    {L"duration", PT_DURATION},
    {L"remaining", PT_REMAINING},
    {L"positionpercent", PT_POSITION_PERCENT},
    {L"volume", PT_VOLUME},
    {L"rating", PT_RATING},
    {L"repeat", PT_REPEAT},
    {L"shuffle", PT_SHUFFLE},
    {L"ratinfsystem", PT_RATING_SYSTEM},
    {L"availablerepeat", PT_AVAILABLE_REPEAT},
    {L"cansetstate", PT_CAN_SET_STATE},
    {L"canskipprevious", PT_CAN_SKIP_PREVIOUS},
    {L"canskipnext", PT_CAN_SKIP_NEXT},
    {L"cansetposition", PT_CAN_SET_POSITION},
    {L"cansetvolume", PT_CAN_SET_VOLUME},
    {L"cansetrating", PT_CAN_SET_RATING},
    {L"cansetrepeat", PT_CAN_SET_REPEAT},
    {L"cansetshuffle", PT_CAN_SET_SHUFFLE},
    {L"createdat", PT_CREATED_AT},
    {L"updatedat", PT_UPDATED_AT},
    {L"activeat", PT_ACTIVE_AT},
    {L"iswebbrowser", PT_IS_WEB_BROWSER},
    {L"platform", PT_PLATFORM},
    // legacy mappings
    {L"player", PT_NAME},
    {L"progress", PT_POSITION_PERCENT},
    {L"coverwebaddress", PT_COVER_SRC},
    {L"supportsplaypause", PT_CAN_SET_STATE},
    {L"supportsskipprevious", PT_CAN_SKIP_PREVIOUS},
    {L"supportsskipnext", PT_CAN_SKIP_NEXT},
    {L"supportssetposition", PT_CAN_SET_POSITION},
    {L"supportssetvolume", PT_CAN_SET_VOLUME},
    {L"supportstogglerepeatmode", PT_CAN_SET_REPEAT},
    {L"supportstoggleshuffleactive", PT_CAN_SET_SHUFFLE},
    {L"supportssetrating", PT_CAN_SET_RATING},
};

enum PLAYER_TYPE parse_player_type(LPCWSTR str)
{
  wchar_t _str[WNP_STR_LEN] = {0};
  wcscpy(_str, str);
  wchar_t* str_lower = _wcslwr(_str);
  size_t numMappings = sizeof(playerTypeMappings) / sizeof(PlayerTypeMapping);
  for (size_t i = 0; i < numMappings; i++) {
    if (wcscmp(str_lower, playerTypeMappings[i].name) == 0) {
      return playerTypeMappings[i].type;
    }
  }
  return PT_STATUS;
}

int parse_player_id(void* rm, LPCWSTR str)
{
  wchar_t _str[WNP_STR_LEN] = {0};
  wcscpy(_str, str);
  wchar_t* str_lower = _wcslwr(_str);
  if (wcscmp(str_lower, L"active") == 0) {
    return -1;
  } else {
    return parse_int_from_lpcwstr(rm, str, -1);
  }
}

int parse_int_from_lpcwstr(void* rm, LPCWSTR str, int default_return)
{
  wchar_t* endPtr;
  errno = 0;
  long int result = wcstol(str, &endPtr, 10);
  if (errno == ERANGE || result > INT_MAX || result < INT_MIN || endPtr == str || *endPtr != L'\0') {
    RmLogF(rm, LOG_ERROR, L"[WebNowPlaying] Failed to parse int from: %ls", str);
    return default_return;
  }
  return (int)result;
}

typedef struct {
  void* rm;
  int player_id;
  enum PLAYER_TYPE player_type;
  bool default_cover_path_added;
  char default_cover_path[WNP_STR_LEN];
  bool legacy_cover_path_added;
  char legacy_cover_path[WNP_STR_LEN];
  double last_double;
  wchar_t last_string[WNP_STR_LEN];
  int last_updated_double;
  int last_updated_string;
} measure_t;

bool should_measure_update_double(measure_t* measure)
{
  bool ret = false;
  if (measure->last_updated_double != g_last_updated) {
    ret = true;
    measure->last_updated_double = g_last_updated;
  }
  return ret;
}

bool should_measure_update_string(measure_t* measure)
{
  bool ret = false;
  if (measure->last_updated_string != g_last_updated) {
    ret = true;
    measure->last_updated_string = g_last_updated;
  }
  return ret;
}

bool get_player_from_measure(measure_t* measure, wnp_player_t* player_out)
{
  if (measure->player_id == -1) {
    return wnp_get_active_player(player_out);
  } else {
    return wnp_get_player(measure->player_id, player_out);
  }
}

void on_any_event(wnp_player_t* player, void* data)
{
  g_last_updated++;
}

// This doesn't work with PlayerId=x because this is legacy behaviour which
// should only be implemented by old (or poorly written) skins.
void on_player_updated(wnp_player_t* player, void* data)
{
  on_any_event(player, data);

  wnp_player_t active_player = WNP_DEFAULT_PLAYER;
  wnp_get_active_player(player);

  if (player->id != active_player.id) {
    return;
  }

  if (strlen(player->cover) == 0) {
    return;
  }

  // Can't compare:
  // - player->cover: will be the same for the same player id (eg libwnp-cover-1.png)
  // - player->cover_src: dp_windows does not have a direct source path so it uses the same as player->cover
  // In dp_windows and web the title is updated together with cover so it should be fine
  if (strcmp(LAST_TITLE, player->title) != 0) {
    strncpy(LAST_TITLE, player->title, WNP_STR_LEN);
    char cover[WNP_STR_LEN];
    strncpy(cover, player->cover + 7, WNP_STR_LEN);
    for (int i = 0; cover[i] != '\0'; i++) {
      if (cover[i] == '/') {
        cover[i] = '\\';
      }
    }
    for (int i = 0; i < MAX_LEGACY_COVER_PATHS; i++) {
      if (LEGACY_COVER_PATHS[i] != NULL) {
        if (CopyFileA(cover, LEGACY_COVER_PATHS[i], false) == 0) {
          // Cant be bothered converting to utf16 to log paths in the error
          LSLog(LOG_ERROR, NULL, L"[WebNowPlaying] Failed to copy cover path. Do not set CoverPath=x on the Cover measure.");
        }
      }
    }
  }
}

__declspec(dllexport) void Initialize(void** data, void* rm)
{
  measure_t* measure = calloc(1, sizeof(measure_t));
  measure->rm = rm;
  *data = measure;

  if (MEASURE_COUNT == 0 || !wnp_is_initialized()) {
    wnp_args_t args = {
        .web_port = 8974,
        .adapter_version = WNPRAINMETER_VERSION,
        .on_player_added = &on_any_event,
        .on_player_updated = &on_any_event,
        .on_player_removed = &on_any_event,
        .on_active_player_changed = &on_any_event,
    };
    wnp_init_ret_t ret = wnp_init(&args);
    if (ret != WNP_INIT_SUCCESS) {
      RmLogF(rm, LOG_ERROR, L"[WebNowPlaying] Failed to start with error code %d", ret);
    }
  }
  MEASURE_COUNT++;
}

void copy_wide_to_char(LPCWSTR src, char dest[WNP_STR_LEN])
{
  size_t len = wcstombs(NULL, src, 0) + 1;
  wcstombs(dest, src, len);
}

__declspec(dllexport) void Reload(void* data, void* rm, double* max_value)
{
  measure_t* measure = (measure_t*)data;
  measure->rm = rm;
  measure->player_type = parse_player_type(RmReadString(measure->rm, L"PlayerType", L"Status", true));
  measure->player_id = parse_player_id(measure->rm, RmReadString(measure->rm, L"PlayerId", L"active", true));

  switch (measure->player_type) {
    case PT_COVER: {
      if (!measure->default_cover_path_added) {
        LPCWSTR default_path = RmReadPath(measure->rm, L"DefaultPath", L"");
        if (wcslen(default_path) > 0) {
          measure->default_cover_path_added = true;
          copy_wide_to_char(default_path, measure->default_cover_path);
        }
      }

      if (!measure->legacy_cover_path_added) {
        LPCWSTR cover_path = RmReadPath(measure->rm, L"CoverPath", L"");
        if (wcslen(cover_path) > 0) {
          RmLog(measure->rm, LOG_NOTICE, L"[WebNowPlaying] CoverPath is deprecated. Measure PlayerType=Cover instead.");
          for (int i = 0; i < MAX_LEGACY_COVER_PATHS; i++) {
            if (LEGACY_COVER_PATHS[i] == NULL) {
              copy_wide_to_char(cover_path, measure->legacy_cover_path);
              LEGACY_COVER_PATHS[i] = (char*)&measure->legacy_cover_path;
              measure->legacy_cover_path_added = true;
              break;
            }
          }
        }
      }
      break;
    }
    case PT_POSITION_PERCENT:
    case PT_VOLUME:
      *max_value = 100;
      break;
  };
}

__declspec(dllexport) double Update(void* data)
{
  measure_t* measure = (measure_t*)data;

  if (!should_measure_update_double(measure)) {
    return measure->last_double;
  }

  double ret = 0.0;
  wnp_player_t player = WNP_DEFAULT_PLAYER;
  get_player_from_measure(measure, &player);
  switch (measure->player_type) {
    case PT_STATUS: {
      ret = player.id != -1;
    } break;
    case PT_PLAYER_COUNT: {
      int num = wnp_get_all_players(NULL);
      ret = num;
    } break;
    case PT_STATE:
      switch (player.state) {
        case WNP_STATE_STOPPED:
          ret = 0;
          break;
        case WNP_STATE_PLAYING:
          ret = 1;
          break;
        case WNP_STATE_PAUSED:
          ret = 2;
          break;
      }
      break;
    case PT_POSITION:
      ret = player.position;
      break;
    case PT_DURATION:
      ret = player.duration;
      break;
    case PT_REMAINING:
      ret = wnp_get_remaining_seconds(&player);
      break;
    case PT_POSITION_PERCENT:
      ret = wnp_get_position_percent(&player);
      break;
    case PT_VOLUME:
      ret = player.volume;
      break;
    case PT_RATING:
      ret = player.rating;
      break;
    case PT_REPEAT:
      switch (player.repeat) {
        case WNP_REPEAT_NONE:
          ret = 0;
          break;
        case WNP_REPEAT_ONE:
          ret = 1;
          break;
        case WNP_REPEAT_ALL:
          ret = 2;
          break;
      }
      break;
    case PT_SHUFFLE:
      ret = player.shuffle;
      break;
    case PT_RATING_SYSTEM:
      ret = player.rating_system;
      break;
    case PT_AVAILABLE_REPEAT:
      // I probably wont document this because I cant be bothered to
      // properly format this in a way it makes sense for rainmeter.
      ret = player.available_repeat;
      break;
    case PT_CAN_SET_STATE:
      ret = player.can_set_state;
      break;
    case PT_CAN_SKIP_PREVIOUS:
      ret = player.can_skip_previous;
      break;
    case PT_CAN_SKIP_NEXT:
      ret = player.can_skip_next;
      break;
    case PT_CAN_SET_POSITION:
      ret = player.can_set_position;
      break;
    case PT_CAN_SET_VOLUME:
      ret = player.can_set_volume;
      break;
    case PT_CAN_SET_RATING:
      ret = player.can_set_rating;
      break;
    case PT_CAN_SET_REPEAT:
      ret = player.can_set_repeat;
      break;
    case PT_CAN_SET_SHUFFLE:
      ret = player.can_set_shuffle;
      break;
    case PT_CREATED_AT:
      ret = player.created_at;
      break;
    case PT_UPDATED_AT:
      ret = player.updated_at;
      break;
    case PT_ACTIVE_AT:
      ret = player.active_at;
      break;
    case PT_IS_WEB_BROWSER:
      ret = player.is_web_browser;
      break;
    case PT_PLATFORM:
      ret = player.platform;
      break;
  }

  measure->last_double = ret;
  return ret;
}

bool get_string(measure_t* measure, char out[WNP_STR_LEN])
{
  wnp_player_t player = WNP_DEFAULT_PLAYER;
  get_player_from_measure(measure, &player);
  switch (measure->player_type) {
    case PT_NAME:
      strncpy(out, player.name, WNP_STR_LEN);
      break;
    case PT_TITLE:
      strncpy(out, player.title, WNP_STR_LEN);
      break;
    case PT_ARTIST:
      strncpy(out, player.artist, WNP_STR_LEN);
      break;
    case PT_ALBUM:
      strncpy(out, player.album, WNP_STR_LEN);
      break;
    case PT_COVER: {
      if (strlen(player.cover) == 0) {
        if (measure->default_cover_path_added) {
          strncpy(out, measure->default_cover_path, WNP_STR_LEN);
        }
        break;
      }
      if (measure->legacy_cover_path_added) {
        strncpy(out, measure->legacy_cover_path, WNP_STR_LEN);
        break;
      }
      strncpy(out, player.cover + 7, WNP_STR_LEN);
      for (int i = 0; out[i] != '\0'; i++) {
        if (out[i] == '/') {
          out[i] = '\\';
        }
      }
      break;
    }
    case PT_COVER_SRC:
      strncpy(out, player.cover_src, WNP_STR_LEN);
      break;
    case PT_POSITION:
      wnp_format_seconds(player.position, true, out);
      break;
    case PT_DURATION:
      wnp_format_seconds(player.duration, true, out);
      break;
    case PT_REMAINING:
      wnp_format_seconds(wnp_get_remaining_seconds(&player), true, out);
      break;
    case PT_PLATFORM:
      // TODO
      break;
    default:
      return false;
  }

  return true;
}

__declspec(dllexport) const wchar_t* GetString(void* data)
{
  measure_t* measure = (measure_t*)data;

  if (!should_measure_update_string(measure)) {
    if (measure->last_string[0] == '\0') {
      return NULL;
    } else {
      return (const wchar_t*)&measure->last_string;
    }
  }

  char str[WNP_STR_LEN] = {0};
  if (get_string(measure, str)) {
    wnp_utf8_to_utf16(str, strlen(str), measure->last_string, WNP_STR_LEN);
    return (const wchar_t*)&measure->last_string;
  } else {
    measure->last_string[0] = '\0';
  }

  return NULL;
}

__declspec(dllexport) void ExecuteBang(void* data, const wchar_t* args)
{
  measure_t* measure = (measure_t*)data;
  wchar_t _args[WNP_STR_LEN] = {0};
  wcscpy(_args, args);
  wchar_t* bang = _wcslwr(_args);
  wnp_player_t player = WNP_DEFAULT_PLAYER;
  get_player_from_measure(measure, &player);

  // SetState x
  if (wcsstr(bang, L"setstate") != NULL) {
    wchar_t* args = wcsstr(bang, L" ");
    int len = wcslen(args);
    if (args == NULL || len == 0) {
      RmLog(measure->rm, LOG_ERROR, L"[WebNowPlaying] Missing argument for SetState");
      return;
    }
    enum wnp_state state = WNP_STATE_STOPPED;
    if (wcscmp(args, L"playing") == 0) {
      state = WNP_STATE_PLAYING;
    } else if (wcscmp(args, L"paused") == 0) {
      state = WNP_STATE_PAUSED;
    } else if (wcscmp(args, L"stopped") == 0) {
      state = WNP_STATE_STOPPED;
    } else {
      RmLogF(measure->rm, LOG_ERROR, L"[WebNowPlaying] Invalid argument for SetState: %ls", args);
      return;
    }
    wnp_try_set_state(&player, state);
    // PlayPause
  } else if (wcsstr(bang, L"playpause") != NULL) {
    wnp_try_play_pause(&player);
    // ToggleState
  } else if (wcsstr(bang, L"togglestate") != NULL) {
    wnp_try_play_pause(&player);
    // Play
  } else if (wcsstr(bang, L"play") != NULL) {
    wnp_try_set_state(&player, WNP_STATE_PLAYING);
    // Pause
  } else if (wcsstr(bang, L"pause") != NULL) {
    wnp_try_set_state(&player, WNP_STATE_PAUSED);
    // SkipPrevious, Previous
  } else if (wcsstr(bang, L"previous") != NULL) {
    wnp_try_skip_previous(&player);
    // SkipNext, Next
  } else if (wcsstr(bang, L"next") != NULL) {
    wnp_try_skip_next(&player);
    // SetPosition n
  } else if (wcsstr(bang, L"setposition") != NULL) {
    wchar_t* args = wcsstr(bang, L" ");
    int len = wcslen(args);
    if (args == NULL || len <= 1) {
      RmLog(measure->rm, LOG_ERROR, L"[WebNowPlaying] Missing argument for SetPosition");
      return;
    }
    args++; // skip the space

    if (len > 1 && args[0] == L'+') {
      args++; // skip the +
      int percent = parse_int_from_lpcwstr(measure->rm, args, 0);
      wnp_try_set_position_percent(&player, (int)wnp_get_position_percent(&player) + percent);
    } else if (len > 1 && args[0] == L'-') {
      args++; // skip the -
      int percent = parse_int_from_lpcwstr(measure->rm, args, 0);
      wnp_try_set_position_percent(&player, (int)wnp_get_position_percent(&player) - percent);
    } else {
      wnp_try_set_position_percent(&player, parse_int_from_lpcwstr(measure->rm, args, 0));
    }
    // SetVolume n
  } else if (wcsstr(bang, L"setvolume") != NULL) {
    wchar_t* args = wcsstr(bang, L" ");
    int len = wcslen(args);
    if (args == NULL || len <= 1) {
      RmLog(measure->rm, LOG_ERROR, L"[WebNowPlaying] Missing argument for SetVolume");
      return;
    }
    args++; // skip the space

    if (len > 1 && args[0] == L'+') {
      args++; // skip the +
      int volume = parse_int_from_lpcwstr(measure->rm, args, 0);
      wnp_try_set_volume(&player, player.volume + volume);
    } else if (len > 1 && args[0] == L'-') {
      args++; // skip the -
      int volume = parse_int_from_lpcwstr(measure->rm, args, 0);
      wnp_try_set_volume(&player, player.volume - volume);
    } else {
      wnp_try_set_volume(&player, parse_int_from_lpcwstr(measure->rm, args, 0));
    }
    // ToggleThumbsUp
  } else if (wcsstr(bang, L"togglethumbsup") != NULL) {
    wnp_try_set_rating(&player, player.rating == 5 ? 0 : 5);
    // ToggleThumbsDown
  } else if (wcsstr(bang, L"togglethumbsdown") != NULL) {
    wnp_try_set_rating(&player, player.rating == 1 ? 0 : 1);
    // SetRating x
  } else if (wcsstr(bang, L"setrating") != NULL) {
    wchar_t* args = wcsstr(bang, L" ");
    int len = wcslen(args);
    if (args == NULL || len == 0) {
      RmLog(measure->rm, LOG_ERROR, L"[WebNowPlaying] Missing argument for SetRating");
      return;
    }
    wnp_try_set_rating(&player, parse_int_from_lpcwstr(measure->rm, args, 0));
    // SetRepeat x
  } else if (wcsstr(bang, L"setrepeat") != NULL) {
    wchar_t* args = wcsstr(bang, L" ");
    int len = wcslen(args);
    if (args == NULL || len == 0) {
      RmLog(measure->rm, LOG_ERROR, L"[WebNowPlaying] Missing argument for SetRepeat");
      return;
    }
    enum wnp_repeat repeat = WNP_REPEAT_NONE;
    if (wcscmp(args, L"none") == 0) {
      repeat = WNP_REPEAT_NONE;
    } else if (wcscmp(args, L"ALL") == 0) {
      repeat = WNP_REPEAT_ALL;
    } else if (wcscmp(args, L"ONE") == 0) {
      repeat = WNP_REPEAT_ONE;
    } else {
      RmLogF(measure->rm, LOG_ERROR, L"[WebNowPlaying] Invalid argument for SetRepeat: %ls", args);
      return;
    }
    wnp_try_set_repeat(&player, repeat);
    // ToggleRepeat, Repeat
  } else if (wcsstr(bang, L"repeat") != NULL) {
    wnp_try_toggle_repeat(&player);
    // SetShuffle
  } else if (wcsstr(bang, L"setshuffle") != NULL) {
    wchar_t* args = wcsstr(bang, L" ");
    int len = wcslen(args);
    if (args == NULL || len == 0) {
      RmLog(measure->rm, LOG_ERROR, L"[WebNowPlaying] Missing argument for SetShuffle");
      return;
    }
    bool shuffle = false;
    if (wcscmp(args, L"true") == 0) {
      shuffle = true;
    } else if (wcscmp(args, L"false") == 0) {
      shuffle = false;
    } else {
      RmLogF(measure->rm, LOG_ERROR, L"[WebNowPlaying] Invalid argument for SetShuffle: %ls", args);
      return;
    }
    wnp_try_set_shuffle(&player, shuffle);
    // ToggleShuffle, Shuffle
  } else if (wcsstr(bang, L"shuffle") != NULL) {
    wnp_try_set_shuffle(&player, !player.shuffle);
  } else {
    RmLogF(measure->rm, LOG_ERROR, L"[WebNowPlaying] Unknown bang: %ls", bang);
  }
}

__declspec(dllexport) void Finalize(void* data)
{
  measure_t* measure = (measure_t*)data;
  for (int i = 0; i < MAX_LEGACY_COVER_PATHS; i++) {
    if (LEGACY_COVER_PATHS[i] == (char*)&measure->legacy_cover_path) {
      LEGACY_COVER_PATHS[i] = NULL;
      break;
    }
  }
  free(measure);
  MEASURE_COUNT--;
  if (MEASURE_COUNT == 0 && wnp_is_initialized()) {
    wnp_uninit();
  }
}

__declspec(dllexport) LPCWSTR GetPlayerIds(void* data, int argc, LPCWSTR argv[])
{
  wnp_player_t players[WNP_MAX_PLAYERS] = {0};
  int count = wnp_get_all_players(players);

  static wchar_t out[WNP_STR_LEN * WNP_MAX_PLAYERS] = {0};
  out[0] = L'\0';

  for (int i = 0; i < count; i++) {
    char str[WNP_STR_LEN * WNP_MAX_PLAYERS] = {0};
    snprintf(str, WNP_STR_LEN * WNP_MAX_PLAYERS, "%d %s\n", players[i].id, players[i].name);
    int len = wcslen(out);
    wnp_utf8_to_utf16(str, WNP_STR_LEN * WNP_MAX_PLAYERS, out + len, (WNP_STR_LEN * WNP_MAX_PLAYERS) - len);
  }

  return out;
}

__declspec(dllexport) LPCWSTR GetPreviousPlayerId(void* data, int argc, LPCWSTR argv[])
{
  measure_t* measure = (measure_t*)data;

  if (argc < 1) {
    return L"active";
  }

  int current_id = parse_player_id(measure->rm, argv[0]);
  if (current_id == -1) {
    wnp_player_t player = WNP_DEFAULT_PLAYER;
    wnp_get_active_player(&player);
    current_id = player.id;
    if (current_id == -1) {
      return L"active";
    }
  }
  int new_id = -1;

  // Search between <current> and 0
  for (int i = current_id - 1; i >= 0; i--) {
    wnp_player_t new_player = WNP_DEFAULT_PLAYER;
    if (wnp_get_player(i, &new_player)) {
      new_id = new_player.id;
      break;
    }
  }

  if (new_id == -1) {
    // Search between <max> and <current>
    for (int i = WNP_MAX_PLAYERS; i > current_id; i--) {
      wnp_player_t new_player = WNP_DEFAULT_PLAYER;
      if (wnp_get_player(i, &new_player)) {
        new_id = new_player.id;
        break;
      }
    }
  }

  static wchar_t buffer[12];
  if (new_id == -1) {
    return L"active";
  } else {
    _snwprintf(buffer, sizeof(buffer) / sizeof(buffer[0]), L"%d", new_id);
  }
  return buffer;
}

__declspec(dllexport) LPCWSTR GetNextPlayerId(void* data, int argc, LPCWSTR argv[])
{
  measure_t* measure = (measure_t*)data;

  if (argc < 1) {
    return L"active";
  }

  int current_id = parse_player_id(measure->rm, argv[0]);
  if (current_id == -1) {
    wnp_player_t player = WNP_DEFAULT_PLAYER;
    wnp_get_active_player(&player);
    current_id = player.id;
    if (current_id == -1) {
      return L"active";
    }
  }
  int new_id = -1;

  // Search between <current> and <max>
  for (int i = current_id + 1; i < WNP_MAX_PLAYERS; i++) {
    wnp_player_t new_player = WNP_DEFAULT_PLAYER;
    if (wnp_get_player(i, &new_player)) {
      new_id = new_player.id;
      break;
    }
  }

  if (new_id == -1) {
    // Search between 0 and <current>
    for (int i = 0; i < current_id; i++) {
      wnp_player_t new_player = WNP_DEFAULT_PLAYER;
      if (wnp_get_player(i, &new_player)) {
        new_id = new_player.id;
        break;
      }
    }
  }

  static wchar_t buffer[12];
  if (new_id == -1) {
    return L"active";
  } else {
    _snwprintf(buffer, sizeof(buffer) / sizeof(buffer[0]), L"%d", new_id);
  }
  return buffer;
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
  if (fdwReason == DLL_PROCESS_ATTACH) {
    DisableThreadLibraryCalls(hinstDLL);
  }
  return true;
}
