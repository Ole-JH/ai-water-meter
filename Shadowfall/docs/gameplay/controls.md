# Controls

## Movement & combat

| Input | Action |
| --- | --- |
| ++"Left click"++ on the ground | Walk there. Hold the button to keep walking toward the cursor |
| ++"Left click"++ on a monster | Walk to it and attack until it dies |
| ++shift++ + ++"Left click"++ | Attack in place, toward the cursor |
| ++"Left click"++ on an NPC, tree, rock, fishing spot, anvil or campfire | Walk over and interact |
| ++"Left click"++ on a loot label | Pick it up. Gold is picked up automatically when you walk over it |
| ++"Right click"++ (hold) | Your class's second ability toward the cursor (Holy Bolt, Throwing Axe, Fireball or Multishot) |
| ++1++ – ++5++ | Your class's five abilities (see [Items & progression](progression.md#classes-and-abilities)) |
| ++q++ / ++e++ | Drink a health / mana potion (potions and food share a 3 s cooldown) |
| ++r++ | Recall to Hollowmere: a 3 s channel (moving, casting or taking damage interrupts it; 20 s cooldown). Press ++r++ again in town to step back to where you left (not into a dungeon) |
| ++alt++ (hold) | Show labels for every item on the ground, including plain white gear when the loot filter hides it |

## Camera

| Input | Action |
| --- | --- |
| Mouse wheel | Zoom in and out |
| ++"Middle mouse"++ drag | Rotate around your hero and tilt (the camera always stays on your hero) |
| ++arrow-left++ / ++arrow-right++ | Rotate |
| ++arrow-up++ / ++arrow-down++ | Tilt |
| ++space++ | Back to the classic Diablo view, centered on your hero |

Every time you enter the world the camera starts in the classic view.

## Windows

| Key | Window |
| --- | --- |
| ++i++ or ++b++ | Bags (inventory) |
| ++c++ | Character: equipment, attributes, stats |
| ++t++ | Talents: spend a point per level |
| ++k++ | Skills: professions and abilities |
| ++l++ | Quest log |
| ++m++ | World map |
| ++f1++ or ++h++ | Help |
| ++f10++ | Admin panel (admins only, see [Admin module](../deployment/admin.md)) |
| ++enter++ or ++slash++ | Chat. `/p` party, `/w name` whisper, `/r` reply, `/invite name`, `/leave`, `/who` |
| ++esc++ | Close the open windows; with nothing open, the **game menu** (also the cog button at the bottom right): Resume, Settings, How to Play, What's New, Log Out (and Admin for admins) |
| ++arrow-up++ / ++arrow-down++ in chat | Recall the messages you sent before |

## Inventory

- **Left click** an item to equip it, or use it if it's a potion or food.
- **Right click** an item while talking to a vendor to sell it, while the stash is open to store it, or while trading to offer it.
- **Left click** a gem, then an item with an empty socket, to socket it. Right click cancels.
- **Shift + right click** drops an item on the ground.
- Click an equipped item in the character window to unequip it.
- **Sort** (bottom of the bags) merges stacks and orders your bags: equipment by rarity, then gems, potions and food, then materials.
- Gold, potions and gems are picked up automatically when you walk over them.
- Hover over an item to compare it with what you're wearing (damage per second and armor difference). Hold ++shift++ to see the equipped item's full tooltip next to it.
- The character window (++c++) and the portrait at the top left show your hero as they look right now, with the weapon and helm you have equipped.

## Settings

The settings are in the game menu (++esc++ → **Settings**):

- **Graphics**: *Low* turns off shadows, grass and the color grade and halves particle effects; *Medium* uses hard shadows and fewer lights; *High* is everything. Try Low on laptops or if the frame rate drops.
- **UI scale** makes the whole interface bigger or smaller (70–150%), applied when you let go of the slider.
- **Show FPS** puts a frame counter at the top of the screen.
- **Label common items**: when off, plain white gear on the ground has no label unless you hold ++alt++ or hover it.
- **Volume**.

Settings are remembered in your browser.

## Emotes

Press ++g++ for the emote menu, or type an emote in chat. Players nearby see the animation and a line in their chat
("Alice waves.").

| Command | Emote |
| --- | --- |
| `/wave` (`/hi`, `/bye`) | Wave |
| `/dance` | Dance until you move |
| `/bow` | A graceful bow |
| `/cheer` | Cheer |
| `/clap` | Applaud |
| `/point` | Point ahead |
| `/flex` | Show off your arms |
| `/sit` | Sit on the ground until you move |
| `/sleep` (`/lie`, `/rest`) | Lie down for a nap until you move |
| `/jump` | Jump for joy |
| `/kick` | Kick the dirt |
| `/shadowbox` (`/punch`) | Punch the air |
| `/guard` (`/block`) | Hold your guard up until you move |

`/e` lists them all; `/e wave` works too. Moving, attacking, casting or getting hit ends an emote.

## What's New

The **What's New** window lists the latest changes to the game, with ones you haven't read marked **NEW**. It opens by itself shortly after you enter the world when there is something new, a pulsing *NEW* chip above the menu bar shows the unread count, and you can open it any time from the game menu (++esc++ → **What's New**). What you have read is saved with your character.

## Warnings

When your life drops below 30%, the edges of the screen pulse red, faster the closer you are to death.

## Logging out

++esc++ → **Log Out** saves your character on the server and returns to the login screen, where you can log in again or with another character. You can't log out within 6 seconds of taking damage, so you can't escape a fight by logging off.
