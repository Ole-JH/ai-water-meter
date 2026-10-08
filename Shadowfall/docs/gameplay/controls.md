# Controls

## Movement & combat

| Input | Action |
| --- | --- |
| ++"Left click"++ on the ground | Walk there. Hold the button to keep walking toward the cursor |
| ++"Left click"++ on a monster | Walk to it and attack until it dies |
| ++shift++ + ++"Left click"++ | Attack in place, toward the cursor |
| ++"Left click"++ on an NPC, tree, rock, fishing spot, anvil or campfire | Walk over and interact |
| ++"Left click"++ on a ladder (during a town invasion) | Climb onto the wall. Up there, click along the wall to walk, outside it to jump down among the invaders, inside it to get back down ([invasions](world.md#town-invasions)) |
| ++"Left click"++ on a loot label | Pick it up. Gold is picked up automatically when you walk over it |
| ++"Right click"++ (hold) | Your class's second ability toward the cursor (Holy Bolt, Throwing Axe, Fireball or Multishot) |
| ++"Right click"++ on another player | Their menu: invite to party, whisper, trade, challenge to a duel (anywhere on their character, not just the name) |
| ++1++ – ++5++ | The five slots of your action bar: your class's abilities (see [Items & progression](progression.md#classes-and-abilities)). **Drag** an ability onto another slot to swap them; the keys follow the new order, and it's saved with your character. (Right-click always casts the class's second ability, wherever it sits.) |
| ++q++ / ++e++ | Drink a health / mana potion (potions and food share a 15 s cooldown) |
| ++v++ | Mount or dismount (once you have bought a mount from Beastmaster Orla) |
| ++r++ | Recall to the nearest town whose waystone you know (Hollowmere at first): a 3 s channel (moving, casting or taking damage interrupts it; 20 s cooldown). Press ++r++ again in town to step back to where you left (not into a dungeon) |
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
| ++y++ | Achievements and titles ([Achievements & titles](achievements.md)) |
| ++o++ | Your guild: message of the day and members ([Guilds](world.md#guilds)) |
| ++m++ | World map: the zones you've found (the one you're in in gold), towns, waystones and the dungeon doors you've come across; under each town a row of small icons for its shops, healer, stash, auction house and bounty board you've seen (hover for the list) |
| ++alt++ + click on the world map or minimap | Ping that spot for your party: it ripples on their maps and a pillar of light stands there for a few seconds |
| **R** / **N** button on the minimap | The minimap turns with the camera (R, the default) or keeps north up (N); the gold **N** on the rim always points north |
| ++f1++ or ++h++ | Help |
| ++f10++ | Admin panel (admins only, see [Admin module](../deployment/admin.md)) |
| ++enter++ or ++slash++ | Chat. `/p` party, `/g` guild, `/w name` whisper, `/r` reply, `/invite name`, `/leave`, `/who` |
| ++ctrl+v++ / ++cmd+v++ | Paste into the chat or the text field you're typing in (from anywhere: the browser's clipboard) |
| ++ctrl+c++ / ++cmd+c++, ++ctrl+x++ | Copy (or cut) what's typed in the chat, or the selection in a text field (all of it if nothing is selected) |
| Right-click a chat line | Copy it |
| ++tab++ / ++shift+tab++ | Next / previous field (login, account, guild, admin and auction windows) |
| Point at the chat | A bar of buttons above it for the same: Say, Party, Guild, Whisper, Reply, Who, Invite, Emotes (with chat open, a channel button switches what you're typing to that channel) |
| ++esc++ | Close the open windows; with nothing open, the **game menu** (also the cog button at the bottom right): Resume, Settings, How to Play, What's New, Account, Character Select, Log Out (and Admin for admins) |
| ++arrow-up++ / ++arrow-down++ in chat | Recall the messages you sent before |
| ++shift++ + click an item (bags or worn) with the chat open | Link it in your message: everyone sees `[Item Name]` in its colour and can point at it for the full tooltip |
| Click a name in the chat | Their player menu (whisper, invite, trade) if they're near, otherwise starts a whisper to them. Every line shows the time it came in |

## Inventory

- **Left click** an item to equip it, or use it if it's a potion or food.
- **Right click** an item while talking to a vendor to sell it, while the stash is open to store it, or while trading to offer it.
- **Left click** a gem, then an item with an empty socket, to socket it. Right click cancels.
- **Shift + right click** drops an item on the ground.
- Click an equipped item in the character window to unequip it.
- **Drag** an item onto another slot to move it there (onto another item swaps them; onto the same potion or material tops up the stack).
- New items sparkle until you hover them. A green arrow means a piece is better than what you wear in that slot, a red one worse (a rough guess from damage or armour, stats, sockets, sets and legendary powers).
- **Sort** (bottom of the bags) merges stacks and orders your bags: equipment by rarity, then gems, potions and food, then materials.
- Gold, potions and gems are picked up automatically when you walk over them.
- Hover over an item to compare it with what you're wearing (damage per second and armor difference). Hold ++shift++ to see the equipped item's full tooltip next to it.
- The character window (++c++) and the portrait at the top left show your hero as they look right now, with the weapon and helm you have equipped.

## Settings

The settings are in the game menu (++esc++ → **Settings**):

- **Graphics**: a preset (*Low*, *Medium*, *High*) and **More...** for every option on its own. Changing any option makes the preset *Custom*.

    | Option | Choices | What it does |
    | --- | --- | --- |
    | Resolution | 60%, 75%, 100%, 150%, Native | Pixels drawn, compared with the page's size. *Native* uses every pixel of a Retina / high-DPI screen (up to 4x the work). The biggest setting for speed |
    | Frame rate | 30, 60, Unlimited | The most frames a second. 30 halves the work and saves battery. In the browser the game always draws in step with the screen's refresh and skips whole refreshes to stay under the cap (Unity's own cap would time frames apart from the screen and often show 30 on a 60 Hz screen) |
    | Shadows | Off, Hard, Soft | Sun and moon shadows |
    | Shadow distance | Short, Medium, Far | How much of the ground in view gets shadows (the distance follows the camera: zoomed in, shadows are drawn close and sharp) |
    | Lights | Few, Some, Many | How many lanterns, torches and spells light an object at once, and how far away lights still shine |
    | Grass | Off, Near, Far | Grass blades up to 24 or 45 paces away |
    | Small details | Off, On | Flowers, ferns, pebbles, mushrooms |
    | Effects | Low, High | Particles, blood stains and the colour grade |
    | View distance | Near, Normal, Far | How far out the land is drawn before the haze: Near draws far fewer trees and rocks (the biggest help in the forests) |

    *Low* is 75% resolution, no shadows, grass or small details, few lights and a near view distance; *Medium* (the default; phones and tablets start on *Low*) 100% resolution, hard
    shadows, near grass and some lights; *High* adds soft far shadows, far grass and many lights. Slow on a laptop? Lower
    *Resolution* first. The resolution is remembered by the browser and used from the first frame next time.
- **UI scale** makes the whole interface bigger or smaller (70–150%), applied when you let go of the slider. However big you make it, the interface never grows past what fits the screen, so on a small screen every window still fits (just smaller). On phones and tablets it's drawn a quarter bigger for fingers; hold the phone sideways.
- **Show FPS** puts a frame counter at the top of the screen.
- **Label common items**: when off, plain white gear on the ground has no label unless you hold ++alt++ or hover it.
- **Gore**: *Off* (no blood), *Normal* (blood sprays and stains the ground for about three minutes), *Extra* (more of everything, chunks on every kill, stains last twice as long).
- **Volume**.
- **Comfort & effects...** tones down what hits the eyes:
  - *Screen shake*, 0–100%.
  - *Damage numbers*: *All*, *Big only* (crits and damage you take), or *Off*.
  - *Hit pauses*: the freeze-frame on crits and killing blows.
  - *Screen flashes* from lightning, dying and levelling up.
  - *Colour-blind loot colours*: set items turn teal and legendaries pink, so no colour depends on telling red from green.

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

++esc++ → **Character Select** saves your character and takes you back to your heroes, still logged in, to play or create another one. ++esc++ → **Log Out** saves and returns to the login screen. You can't do either within 6 seconds of taking damage, so you can't escape a fight by logging off.

## Account

++esc++ → **Account** changes your password, sets or removes your email address, or gives you a new recovery code. Each change needs your current password. Changing the password logs you out everywhere else.

## Login screens

| Screen | What's on it |
| --- | --- |
| **Enter the World** | Account name and password. **Create an account** and **Forgot password?** below |
| **Create an Account** | Account name, password (twice, at least 6 characters), optional email for password resets. Afterwards you are shown your **recovery code**: keep it safe, it is shown only once |
| **Forgot Your Password?** | **I have a code** (your recovery code, or a reset code from an email or an admin), or **Email Me a Code** if your account has an email address |
| **Choose a New Password** | Account name, code and the new password. A link from a reset email opens this screen already filled in |
| **Your Heroes** | Your characters with class and level: double-click or **Enter World**, **New Hero**, or **Delete Hero** (asks for your password) |
| **Create a Hero** | Name and class (Knight, Barbarian, Mage, Rogue). The class can't be changed later |

One account holds up to 10 heroes. See [Accounts & passwords](../deployment/accounts.md) for how resets work.
