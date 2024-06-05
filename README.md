# LethalFixes

[![Latest Version](https://img.shields.io/thunderstore/v/Dev1A3/LethalFixes?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/lethal-company/p/Dev1A3/LethalFixes)
[![Total Downloads](https://img.shields.io/thunderstore/dt/Dev1A3/LethalFixes?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/lethal-company/p/Dev1A3/LethalFixes)
[![Discord](https://img.shields.io/discord/646323142737788928?style=for-the-badge&logo=discord&logoColor=white&label=Discord)](https://discord.gg/DZD2apDnMM)
[![Ko-fi](https://img.shields.io/badge/Donate-F16061.svg?style=for-the-badge&logo=ko-fi&logoColor=white&label=Ko-fi)](https://ko-fi.com/K3K8SOM8U)

### Information

This mod fixes a bunch of common Lethal Company issues:

- General Fixes

  - [Host] Made the debug menu test room & invincibility toggles show whether they are enabled/disabled.
  - [Host] Fixed flooded weather only working for the first day of each session.
  - [Host] Fixed clients seeing the default disconnect message when kicked instead of a kick message.
  - [Host] Fixed the host's rank not syncing to clients on initial join.
    - The same as [RankFix](https://thunderstore.io/c/lethal-company/p/Glitch/RankFix/).
  - [Host] Fixed stormy weather only working for the first stormy day of each session if items are left outside.
  - [Client] Fixed the start lever cooldown not being reset on the deadline when attempting to route to the company after attempting to go to a regular moon.
  - [Client] Fixed the terminal scan command including items inside the ship in the calculation of the approximate value.
  - [Client] Fixed the shotgun having increased damage for clients.
  - [Client] Removed shadows from the fancy lamp & apparatus to improve performance.
    - The same as [NoPropShadows](https://thunderstore.io/c/lethal-company/p/Glitch/NoPropShadows/) but without the requirement of having to re-join the lobby for the shadows to be removed from newly spawned props.
  - [Client] Made PTT speaking indicator only visible when speaking.
  - [Client] Made PTT speaking indicator show for voice activity too.
  - [Client] Fixed the name above your head in LAN not including the player id (currently it just shows Player #).
  - [Client] Fixed the main menu buttons not being aligned properly.
    - The same as [Align Menu Buttons](https://thunderstore.io/c/lethal-company/p/GoldenGuy1000/Align_Menu_Buttons/).
  - [Client] Fixed other players reloading a shotgun making the item you have in the same hotbar slot that they had their ammo in invisible.
  - [Client] Changed Dissonance log levels to fix log spam during voice chat lag.
    - The same as [DissonanceLagFix](https://thunderstore.io/c/lethal-company/p/linkoid/DissonanceLagFix/) but with the ability to change the log level in the config and with all of the log levels changed instead of only the recording one.
    - Mod Default: Error (4)
  - [Client] Changed NetworkManager log level to fix log spam from RPCs.
    - The same as [FixRPCLag](https://thunderstore.io/c/lethal-company/p/Bobbie/FixRPCLag/) but with the ability to change the log level in the config.
    - Mod Default: Normal (1)

- Enemy Fixes

  - [Host] Fixed dead enemies being able to open doors.
  - [Host] Fixed outdoor enemies being able to spawn inside the outdoor objects.
  - [Host] Fixed indoor enemies being able to be assigned to vents that were already occupied during the same hour.
    - In vanilla when this bug occurs it results in less enemies spawning since the original enemy that was going to spawn in the vent gets overwritten by the new one.
  - [Host] Fixed butler knife and metal ordered items not attracting lightning until the next round.
  - [Host] Fixed the hoarder bug not dropping the held item if it's killed too quickly.
  - [Host] Fixed the old bird being unable to move after successfully grabbing a player.
  - [Client] Fixed entrance nearby activity including dead enemies.
  - [Client] Fixed forest giants being able to kill before their spawn animation finishes.
    - Similar to [LCSpawnOnPlayerFix](https://thunderstore.io/c/lethal-company/p/Suki/LCSpawnOnPlayerFix/) but instead of disabling nearby spawn points this fixes the actual cause of the issue.
  - [Client] Fixed the death sound of Baboon Hawks, Hoarder Bugs & Nutcrackers not working.
    - For additional sound fixes I would suggest using [EnemySoundFixes](https://thunderstore.io/c/lethal-company/p/ButteryStancakes/EnemySoundFixes/).
  - [Client] Fixed the old bird missiles getting stuck if the owning old bird is destroyed by an earth leviathan.
  - [Client] Fixed old birds being desynced on clients after the first flight. [@digger1213](https://github.com/digger1213)
  - [Client] Fixed nutcrackers not moving whilst aiming for clients. [@digger1213](https://github.com/digger1213)

- Spike Trap Fixes
  - [Host] Fixed interval spike trap entrance safety period activating when exiting the facility instead of when entering.
  - [Host] Fixed interval spike trap entrance safety period not preventing death if the trap slams at the exact same time that you enter.
  - [Client] Fixed player detection spike trap having no entrance safety period.
  - [Client] Fixed player detection spike trap entrance safety period not preventing death if the trap slams at the exact same time that you enter.
  - [Client] Fixed spike traps having no indication when disabled via the terminal (they now play the landmine deactivate sound and have the light emissive disabled).
  - [Client] Added the ability to enable the spike trap safety period when inverse teleporting (disabled by default).

### Configuration

The mod has some configuration options including:

- Toggle for the terminal scan changes to allow compatibility with other mods.
- Toggle for the spike trap terminal activate/deactivate sounds.
- Toggle for the spike trap safety period when inverse teleporting.
- Toggle to decide whether the PTT speaking indicator shows for voice activity.

### Support

You can get support in any the following places:

- The [thread](https://discord.com/channels/1168655651455639582/1235731485894643722) in the [LC Modding Discord Server](https://discord.gg/lcmod)
- [GitHub Issues](https://github.com/1A3Dev/LC-LethalFixes/issues)
- [My Discord Server](https://discord.gg/DZD2apDnMM)

### Compatibility

- Supported Game Versions:
  - v50+
- Works Well With:
  - N/A
- Not Compatible With:
  - Symbiosis
    - This mod does the same thing as the "ItemDeathDrop" config option so I would suggest disabling that option in the Symbiosis config to prevent any issues.
