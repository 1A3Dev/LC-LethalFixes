### 1.2.6

- Fixed NullReferenceException if the moon has a null spawn denial point.

### 1.2.5

- Fixed the terminal scan command including dead bodies in the scrap counts

### 1.2.4

- Fixed compatibility with the ProtectConductiveItems mod

### 1.2.3

- Corrected the public beta outdated version check

### 1.2.2

- Fixed old birds erroring if the enemy they are targeting gets destroyed by an earth leviathan

### 1.2.1

- Removed fixes for issues that no longer exist in v64:
  - Made the fix for the masked enemy being unable to enter/exit the facility only apply to v60-v62
  - Removed the fix for the level ambience not being set
  - Removed the fix for other players reloading a shotgun making the item you have in the same hotbar slot that they had their ammo in invisible
- Improved the existing fixes of stormy weather
- Added an additional fix to ensure stormy weather doesn't break when an item is destroyed
- Made toggling the prop shadow config option mid-game (using a mod such as LethalConfig) update the shadow for existing props

### 1.2.0

- Fixed the masked enemy being unable to enter/exit the facility on all interiors except the mineshaft

### 1.1.9

- Changed README to have supported version at v60+

### 1.1.8

- Corrected version check for public beta branch

### 1.1.7

- [Client] Fixed old bird blowtorch particles not working after the first kill. [@ButteryStancakes](https://github.com/ButteryStancakes)
- [Client] Fixed the current vehicle passenger being set to null on the local client.
- [Client] Fixed the vehicle steering wheel visually desyncing from the actual steering angle.
- [Client] Fixed getting stuck in the vehicle drivers seat when getting in at the same time as someone else.

### 1.1.6

- Removed a bunch of fixes that are fixed in v56.

### 1.1.5

- This version should still be compatible with v50!
- Added a config option to enable prop shadows (which are disabled by default to help with performance).
- [Client] Fixed the terminal scan command not using the scrapValueMultiplier for the approximate calculation.
- [Host] Fixed indoor enemies being unable to spawn in groups (the barber is supposed to spawn two at once). [@ButteryStancakes](https://github.com/ButteryStancakes)
- [v55] Removed the forest giant insta-kill fix since it was fixed in vanilla.
- [v55] Removed the fix for the shotgun having increased damage for clients (fixed in vanilla).
- [v55] [Client] Fixed the hotbar breaking when grabbing an object from the shelves of the Company Cruiser whilst having an item in the currently selected slot.

### 1.1.4

- Fixed button to join a LAN lobby not being aligned properly.

### 1.1.3

- [Client] Fixed other players reloading a shotgun making the item you have in the same hotbar slot that they had their ammo in invisible.

### 1.1.2

- [Host] Fixed stormy weather only working for the first stormy day of each session if items are left outside. [@digger1213](https://github.com/digger1213)
- [Client] Fixed the main menu buttons not being aligned properly.
  - The same as [Align Menu Buttons](https://thunderstore.io/c/lethal-company/p/GoldenGuy1000/Align_Menu_Buttons/).
- [Client] Fixed old birds being desynced on clients after the first flight. [@digger1213](https://github.com/digger1213)
- [Client] Fixed nutcrackers not moving whilst aiming for clients. [@digger1213](https://github.com/digger1213)

### 1.1.1

- Added a config option to decide whether the PTT speaking indicator shows for voice activity.
- Fixed the name above your head in LAN not including the player id.

### 1.1.0

- [Host] Fixed the host's rank not syncing to clients on initial join.
  - The same as [RankFix](https://thunderstore.io/c/lethal-company/p/Glitch/RankFix/).
- [Client] Removed shadows from the fancy lamp & apparatus to improve performance.
  - The same as [NoPropShadows](https://thunderstore.io/c/lethal-company/p/Glitch/NoPropShadows/) but without the requirement of having to re-join the lobby for the shadows to be removed from newly spawned props.
- [Client] Made PTT speaking indicator only visible when speaking.
- [Client] Made PTT speaking indicator show for voice activity too.

### 1.0.9

- Fixed a bug caused by v1.0.6 which resulted in clients being immune to shotgun damage.
- Added a popup when entering the main menu whilst on the public beta branch to inform people that they are on an outdated version of v50.

### 1.0.8

- Fixed the old bird being unable to move after successfully grabbing a player.

### 1.0.7

- Fixed enemies being able to be assigned to vents that were already occupied during the same hour.

### 1.0.6

- Fixed the shotgun having increased damage for clients.
- Fixed the death sound of Baboon Hawks, Hoarder Bugs & Nutcrackers not working.
- Fixed the old bird missiles getting stuck if the owning old bird is destroyed by an earth leviathan.

### 1.0.5

- Fixed exception if the moon has a null outside object.
- Added a config option to choose the Dissonance (Voice Chat) log level.
- Added a config option to choose the NetworkManager (RPC) log level.

### 1.0.4

- Fixed nearby activity constantly flashing between "Near activity detected" & "Enter: [LMB]" when there is an enemy near the door.

### 1.0.3

- Changed dissonance log levels.
- Fixed negative weight speed bug.
- Fixed clients seeing the default disconnect message when kicked instead of a kick message.
- Fixed EntranceTeleport error spam when a naturally spawned masked enemy is despawned whilst using ModelReplacementAPI.
- Added a config option (disabled by default) to enable the spike trap safety period when inverse teleporting.

### 1.0.2

- Added extra spike trap fix to make the entrance safety period also apply if the trap is already mid-slamming.

### 1.0.1

- Fixed dead enemies being included on the entrance nearby activity check.
- Added some additional configuration options.

### 1.0.0

- Initial Release
