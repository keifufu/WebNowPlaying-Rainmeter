function Initialize()
  local skinsPath = SKIN:GetVariable("SKINSPATH")
  local oldPath = skinsPath .. "WebNowPlayingRedux"
  local function directoryExists(path)
    local ok, err, code = os.rename(path, path)
    if not ok then
      if code == 13 then
        return true
      end
      return false, err
    end
    return true
  end
  if directoryExists(oldPath) then
    os.execute("rd /s /q \"" .. oldPath .. "\"")
  end
end

-- Iterate over all available players,
-- check if the current #PlayerId# is still available
-- and set a new #PlayerId# if it is not.
-- Also create tabs for the currently available players.
function Update()
  local playerId = SKIN:GetVariable("PlayerId")
  local foundPlayer = false
  local foundIds = {}
  local maxPlayers = 10 -- TODO: 63
  for line in string.gmatch(SKIN:ReplaceVariables("[&MeasureStatus:GetPlayerIds()]"), "[^\n]+") do
    local id, name = string.match(line, "(%d+)%s+(.+)")
    if id and name and tonumber(id) <= maxPlayers then
      if id == playerId then
        foundPlayer = true
      end
      local icon = SKIN:GetVariable("ico_"..name:gsub(" ", "_"))
      if not icon then
        icon = SKIN:GetVariable("ico_Default")
      end
      SKIN:Bang("!SetOption", id, "Text", icon .. " " .. name)
      table.insert(foundIds, id)
    end
  end

  local function isValueInArray(array, value)
    for _, v in ipairs(array) do
      if v == value then
        return true
      end
    end
    return false
  end

  for i = 0, maxPlayers do
    if isValueInArray(foundIds, tostring(i)) then
      SKIN:Bang("!ShowMeter", i)
    else
      SKIN:Bang("!HideMeter", i)
    end
  end
end
