using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// What NPCs and villagers say to themselves (shown as speech bubbles when a hero is nearby).
    /// "{name}" is replaced with the hero's name.
    /// </summary>
    public static class NpcChatter
    {
        static readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>
        {
            { "Captain Aldric", new[] {
                "Keep your blade sharp and your wits sharper.",
                "Wolves on the north road again. I can hear them from here.",
                "If you see goblins, don't count them. Just run. Or don't, and earn my respect.",
                "Hollowmere has stood for three hundred years. It will stand tonight.",
                "{name}! Good to see a capable sword in town.",
            } },
            { "Forester Wren", new[] {
                "Smell that? Pine sap and rain. Best smell there is.",
                "The willows by the lake are ready for cutting.",
                "Every tree you fell, plant a thought for the next one.",
                "Something big has been tearing bark off the yews up north.",
            } },
            { "Smith Gorrin", new[] {
                "*CLANG* ... *CLANG* ...",
                "Copper's soft. Iron's honest. Mithril? Mithril's a diva.",
                "Bring me ore and I'll make something that keeps you alive.",
                "This hammer was my father's. The handle's new. And the head.",
            } },
            { "Merchant Lysa", new[] {
                "Potions! Red for blood, blue for brains!",
                "Got anything shiny? I pay fair. Mostly fair.",
                "Buying everything you dragged out of a goblin. Yes, everything.",
            } },
            { "Sister Mae", new[] {
                "The Light keeps watch, even in the darkest night.",
                "Come to me if you're hurt. Everyone's hurt, eventually.",
                "I pray for those who went into the crypt. Mostly I pray they come back.",
            } },
            { "Thomas", new[] {
                "Those bandits took my best turnips. Who steals turnips?",
                "Rain's coming. My knee says so, and my knee is never wrong.",
                "Mornin'! Or is it evenin'? Farm work blurs it all together.",
                "If you see a scarecrow walking, that one's not mine.",
            } },
            { "Jenkins", new[] {
                "Build #4127 has failed. As expected.",
                "I have scheduled the sunrise for six o'clock. It is, as ever, flaky.",
                "Someone has disabled the tests again. I shall pretend not to notice.",
                "Your pipeline is ready, sir. The plumbing, I mean. Also the other one.",
                "A butler never panics. A butler retries with exponential backoff.",
                "I have automated the village well. It now requires seventeen plugins.",
                "It works on my manor.",
                "Never deploy on a Friday. Never deploy on a Thursday either. I am reconsidering Wednesdays.",
                "The goat has been promoted to production. Nobody approved this.",
                "I have containerized the bread. It is now stale in a reproducible way.",
                "Uptime this month: 99.9 percent. The remaining 0.1 percent was a chicken.",
                "Have you tried turning the village off and on again? I have. Twice. The bell objected.",
                "Our single point of failure is Thomas. I am drafting a redundant Thomas.",
                "Everything is fine. The dashboard is green. I painted it green.",
                "Pager went off at three in the morning. It was an owl. I have silenced the owl.",
                "A merge conflict between the church and the tavern. Both claim Sunday.",
                "I documented the well. Nobody will read it. That is the tradition.",
                "Infrastructure as code: I have written the windmill down. It is still spinning. Unclear why.",
            } },
            { "Armorer Brann", new[] {
                "Plate, mail or leather? Depends how much you like your ribs.",
                "Fresh stock every few hours. Old stock goes to the goblins, apparently.",
                "A dented helm means it worked.",
            } },
            { "Weaponsmith Hilda", new[] {
                "Sharp end goes in the monster. Most people figure that out.",
                "Axes, swords, maces. Pick one that matches your temper.",
                "Hold it like you mean it, {name}.",
            } },
            { "Innkeeper Rosie", new[] {
                "Stew's on! Don't ask what's in it, just eat it while it's hot.",
                "Mind the third step, love, it bites.",
                "A warm meal heals more than any potion. Well, almost as much.",
                "The Prancing Boar is open all night. Mostly for the guards.",
            } },
            { "Curio Dealer Vex", new[] {
                "Rings that remember. Amulets that whisper. Very reasonable prices.",
                "Everything here is genuine. Genuinely something.",
                "Psst. {name}. Want to see something that glows?",
            } },
            // Wandering villagers
            { "Villager", new[] {
                "Lovely day for it.",
                "Did you hear? Someone saw lights in the old crypt again.",
                "My cousin went to the quarry and came back with three golems' worth of stories.",
                "Market's busy today.",
                "Have you tried Rosie's stew? It's... an experience.",
                "I keep telling Jenkins the well worked fine before.",
            } },
            // ---- the outer towns
            { "Jarl Sigrun", new[] {
                "The wolves up here are white as the snow. You see them when they bite.",
                "Frosthaven pays its debts. Ask anyone who's still alive.",
                "{name}, you came the long road. Good. The short one has a giant on it.",
                "A Jarl Frostborn they call him, up in the high camp. Bigger than my hall.",
            } },
            { "Trader Olaf", new[] { "Furs! Rope! Hot coals in a pot!", "Hollowmere prices? Ha. You're not in Hollowmere.", "Buy the potions. Trust me. Buy the potions." } },
            { "Runesmith Halvard", new[] { "*TING* ... *TING* ...", "Cold steel holds an edge longer.", "The runes don't do anything. People just like them." } },
            { "Furrier Eska", new[] { "Wolf, bear, something I can't name. All warm.", "Bring me white wolf pelts and I'll make you something nice. Someday.", "Wrap up, {name}. Your ears are blue." } },
            { "Healer Ingrid", new[] { "Toes count first, then fingers.", "Snow-blind, frost-bit, wolf-bit. Pick one at a time, please.", "Drink this. It's mostly honey." } },
            { "Caravan Master Rahim", new[] {
                "The raiders took my best camels. And my second-best.",
                "Saltreach was built on trade. Trade is built on roads that don't kill you.",
                "{name}! You look like someone who can swing a sword at a sand golem.",
                "They say the Sun King's tomb is out there. They say a lot of things.",
            } },
            { "Spice Trader Nadia", new[] { "Pepper! Saffron! Something orange I can't pronounce!", "Taste this. No, the other one. Trust me.", "Water's free. Everything else costs." } },
            { "Gemcutter Zafir", new[] { "The sand gives up its stones slowly.", "This one hums at night. Half price.", "Rubies from the badlands. Probably rubies." } },
            { "Sandsmith Tariq", new[] { "Curved blade, straight answers.", "The heat makes the steel honest.", "Raiders use my blades too. Bad for business, good for business." } },
            { "Healer Amara", new[] { "Water first. Always water first.", "Sunburn is a wound too, {name}.", "Sit in the shade. That's an order." } },
            { "Commander Varek", new[] {
                "The Reach doesn't burn. It smoulders. Forever.",
                "Every night something comes out of those ruins. Every morning we count the walls.",
                "{name}. I hear you're hard to kill. Prove it out there.",
                "Somewhere in the ash there's a crown. And a king who won't take it off.",
            } },
            { "Quartermaster Bryn", new[] { "Sign here. And here. And here.", "If it's dented, it worked.", "No refunds on armor returned with holes in it." } },
            { "Smuggler Kett", new[] { "I'm not here.", "Prices are, uh, negotiable. Upward.", "Don't tell the Commander about the crates." } },
            { "Ashwarden Lyra", new[] { "Breathe out. Slowly. Now in.", "The ash gets everywhere. Everywhere.", "Light keep you, {name}. It's dim out there." } },
            { "Woodsman Garrick", new[] { "Timber! ...no, false alarm.", "The pines up here are older than Hollowmere.", "Something's been howling by the north tarn." } },
            { "Old Martha", new[] { "Stew's hot!", "Sit, eat. You look like a twig.", "My husband went north for firewood. Thirty years ago. He's late." } },
            { "Guard", new[] {
                "Move along. Nothing to see here. Probably.",
                "All quiet on the walls.",
                "I used to be an adventurer like you.",
                "Stay inside the walls after dark.",
            } },
        };

        static readonly string[] night =
        {
            "Bit late to be out, isn't it?",
            "The dead stir more at night. Keep to the torchlight.",
            "Can't sleep. Too many howls tonight.",
            "Stars are bright tonight.",
        };

        static readonly string[] dawn =
        {
            "Another sunrise. Good.",
            "Up early, {name}?",
        };

        // What everyone talks about: the weather, and the festival of the season.
        static readonly string[] rainLines = { "Rain again. The well's never been fuller.", "Mind the puddles, {name}.", "Thomas says his knee predicted this.", "Good weather for ducks. And for staying indoors." };
        static readonly string[] stormLines = { "Did you hear that thunder? Shook the windows!", "Stay away from the tall trees, {name}.", "The Light protect us, what a storm." };
        static readonly string[] snowLines = { "Look at it come down!", "Don't walk in the deep snow, it'll soak you to the knees.", "The elves will have the streets clear in no time.", "Brrr. Cocoa weather." };
        static readonly string[] fogLines = { "Can't see my own hand in this fog.", "Fog like this, the wolves come close. Stay near the torches.", "Something's moving out there in the murk..." };
        static readonly string[][] festivalLines =
        {
            new[] { "Happy Bloom Festival, {name}!", "Smell the blossoms? Spring at last.", "The garlands went up this morning. Aren't they lovely?", "Flowers on every doorstep. That's Hollowmere in spring." },
            new[] { "The Midsummer Fair! Dance round the maypole with us, {name}!", "Bonfire on the square tonight. Bring a friend.", "Hottest summer in years.", "Ribbons, bunting, and far too much cider." },
            new[] { "Happy Harvest, {name}! The pumpkins came in big this year.", "Don't you dare run through the leaf piles.", "Somebody ran through Old Mara's leaves again. She's still chasing them.", "The trees are on fire with colour this year." },
            new[] { "Merry Winterfest, {name}!", "Have you seen the tree on the square? The elves decorated it.", "Who keeps making snowmen in front of the church?", "The lake froze solid last night.", "Elves! Shoveling! Whatever next." },
        };

        static string SeasonalLine()
        {
            var p = Player.I;
            var at = p != null ? p.transform.position : Vector3.zero;
            if (Weather.Precip > 0.2f)
            {
                if (Weather.ColdAt(at)) return snowLines[Random.Range(0, snowLines.Length)];
                return Weather.Sky == "storm" ? stormLines[Random.Range(0, stormLines.Length)] : rainLines[Random.Range(0, rainLines.Length)];
            }
            if (Weather.Fog > 0.4f) return fogLines[Random.Range(0, fogLines.Length)];
            var f = festivalLines[(int)Weather.Season];
            return f[Random.Range(0, f.Length)];
        }

        public static string Line(string speaker, string heroName)
        {
            string line;
            if (Random.value < 0.3f) line = SeasonalLine();
            else if (DayNight.Night > 0.7f && Random.value < 0.35f) line = night[Random.Range(0, night.Length)];
            else if (DayNight.Phase == "Dawn" && Random.value < 0.3f) line = dawn[Random.Range(0, dawn.Length)];
            else if (lines.TryGetValue(speaker, out var own)) line = own[Random.Range(0, own.Length)];
            else return null;
            return line.Replace("{name}", heroName ?? "traveller");
        }

        static readonly string[] greetings = { "Hello, {name}.", "Well met, {name}!", "Ah, {name}. Back again?", "Good to see you, {name}." };

        public static string Greeting(string heroName) =>
            greetings[Random.Range(0, greetings.Length)].Replace("{name}", heroName ?? "traveller");
    }
}
