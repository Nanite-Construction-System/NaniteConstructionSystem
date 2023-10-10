using NaniteConstructionSystem;
using NaniteConstructionSystem.Extensions;
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using VRage.Game.Components;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game.ModAPI;
using VRage.Game;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces.Terminal;

//Supported Languages reference:

// MyLanguagesEnum.English
// MyLanguagesEnum.Czech
// MyLanguagesEnum.Slovak
// MyLanguagesEnum.German
// MyLanguagesEnum.Russian
// MyLanguagesEnum.Spanish_Spain
// MyLanguagesEnum.French
// MyLanguagesEnum.Italian
// MyLanguagesEnum.Danish
// MyLanguagesEnum.Dutch
// MyLanguagesEnum.Icelandic
// MyLanguagesEnum.Polish
// MyLanguagesEnum.Finnish
// MyLanguagesEnum.Hungarian
// MyLanguagesEnum.Portuguese_Brazil
// MyLanguagesEnum.Estonian
// MyLanguagesEnum.Norwegian
// MyLanguagesEnum.Spanish_HispanicAmerica
// MyLanguagesEnum.Swedish
// MyLanguagesEnum.Catalan
// MyLanguagesEnum.Croatian
// MyLanguagesEnum.Romanian
// MyLanguagesEnum.Ukrainian
// MyLanguagesEnum.Turkish
// MyLanguagesEnum.Latvian
// MyLanguagesEnum.ChineseChina

namespace NaniteConstructionSystem
{
    public static class Localization
    {
        public static string Localize(int message)
        {
            MyLanguagesEnum language = MyAPIGateway.Session.Config.Language;
            switch (message)
            {
                case (0): // This is an unused test to show how the formatting goes
                    switch (language)
                    {
                        case (MyLanguagesEnum.Portuguese_Brazil):
                            return "Olá!";
                            break;
                        case (MyLanguagesEnum.Dutch):
                            return "Hallo!";
                            break;
                        case (MyLanguagesEnum.Spanish_HispanicAmerica):
                        case (MyLanguagesEnum.Spanish_Spain): // Both versions of Spanish will return "¡Hola!"
                            return "¡Hola!";
                            break;
                        case (MyLanguagesEnum.French):
                            return "Bonjour!";
                            break;
                        default: // English will always be "default" if the user's language hasn't been added yet
                            return "Hello!";
                    }
                    break;
                case (11):
                    switch (language)
                    {
                        default:
                            return "Many mods/scripts, especially those that modify ore detectors, ship welders, power usage and inventory management, can cause undesired results given the enormous scope of this mod.";
                    }
                case (12):
                    switch (language)
                    {
                        default:
                            return "Before submitting a bug report, PLEASE try to recreate the bug in a world where Nanite Construction Facility is the only mod.";
                    }
                case (13):
                    switch (language)
                    {
                        default:
                            return "There are thousands of unpredictable mods for Space Engineers that simply cannot be accounted for.";
                    }
                case (14):
                    switch (language)
                    {
                        default:
                            return "To submit a bug report, go to the Github page at";
                    }
                case (15):
                    switch (language)
                    {
                        default:
                            return "Click on the 'Issues' tab. Please search for an existing issue before creating a new issue.";
                    }
                case (16):
                    switch (language)
                    {
                        default:
                            return "You may comment your information on an existing issue and include screenshots.";
                    }
                case (17):
                    switch (language)
                    {
                        default:
                            return "If the issue does not exist, you may create a new one. You must include the following items or your issue will be deleted:";
                    }
                case (18): // This is a bulleted list item
                    switch (language)
                    {
                        default:
                            return "Your Space Engineers log, located at %appdata%/roaming/SpaceEngineers/SpaceEngineers.log";
                    }
                case (19): // This is a bulleted list item
                    switch (language)
                    {
                        default:
                            return "Your local world/save (or steam link to it) where you can reproduce the problem.";
                    }
                case (20): // This is a bulleted list item
                    switch (language)
                    {
                        default:
                            return "The output of"; // a debugging help command, /nanite debug
                    }
                case (21):
                    switch (language)
                    {
                        default:
                            return "Thank you in advance. Taking these measures allows the developers to address issues faster.";
                    }
                case (22): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Troubleshooting";
                    }
                case (23): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Commands";
                    }
                case (24): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Debugging Information";
                    }
                case (25): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Improving Performance";
                    }
                case (26): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Nanite Control Facility Upgrades";
                    }
                case (28): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Meet the Developers";
                    }
                case (30): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Testing Server";
                    }
                case (31): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "A Sincere Thanks";
                    }
                case (32): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Working Together";
                    }
                case (33): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Shared Resources";
                    }
                case (34): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Mining Nanites";
                    }
                case (35): // This is a help section header
                    switch (language)
                    {
                        default:
                            return "Medical Nanites";
                    }
                default:
                    return ""; 
            }
        }

        public static void Help(string messageText, out bool donothing, out string message, out string title)
        {
            donothing = false;
            message = "";
            title = "";

            switch (messageText)
            {
                case "/nanite debug":
                    title = "Debugging";
                    message = $@"
<--- {Localize(24)} --->

{Localize(2)}

IsClient: {Sync.IsClient}
IsServer: {Sync.IsServer}
IsDedicated: {Sync.IsDedicated}
IsOffline: {MyAPIGateway.Session.OnlineMode == VRage.Game.MyOnlineModeEnum.OFFLINE}

MyAPIGateway.Session.Player.Client: {MyAPIGateway.Session.Player.Client}
MyAPIGateway.Multiplayer.IsServerPlayer(Client): {MyAPIGateway.Multiplayer.IsServerPlayer(MyAPIGateway.Session.Player.Client)}
";
                    break;

                case "/nanite help":
                    title = "Help";
                    message = $@"
<--- {Localize(23)} --->

/nanite help
/nanite help basics
/nanite help assemblers
/nanite help beacons
/nanite help colors
{(NaniteConstructionManager.Settings.CleanupEnabled ? "/nanite help cleanup" : "")}
{(NaniteConstructionManager.Settings.ConstructionEnabled ? "/nanite help construction OR /nanite help repair" : "")}
{(NaniteConstructionManager.Settings.ProjectionEnabled ? "/nanite help projections" : "")}
{(NaniteConstructionManager.Settings.DeconstructionEnabled ? "/nanite help deconstruction" : "")}
{(NaniteConstructionManager.Settings.LifeSupportEnabled ? "/nanite help medical" : "")}
{(NaniteConstructionManager.Settings.MiningEnabled ? "/nanite help mining" : "")}
/nanite help upgrades
/nanite help cooperation
/nanite credits

<--- {Localize(22)} --->

{Localize(11)}

{Localize(12)}

{Localize(13)}

{Localize(14)} https://github.com/nukeguard/NaniteConstructionSystem

{Localize(15)}

{Localize(16)}

{Localize(17)}

- {Localize(18)}

- {Localize(19)}

- {Localize(20)} /nanite debug

{Localize(21)}
";
                    break;

                case "/nanite help upgrades":

                    title = "Upgrades";
                    message = $@"
<--- {Localize(25)} --->

Upgrades allow the player to fine tune the capabilities of the
Nanite Control Facility. Here's what they do.

<--- {Localize(26)} --->

Construction: Increases construction/repair nanites by {NaniteConstructionManager.Settings.ConstructionNanitesPerUpgrade}
and projection nanites by {NaniteConstructionManager.Settings.ProjectionNanitesPerUpgrade}.

Deconstruction: Increases deconstruction nanites by {NaniteConstructionManager.Settings.DeconstructionNanitesPerUpgrade}.

Cleanup: Increases cleanup nanites by {NaniteConstructionManager.Settings.CleanupNanitesPerUpgrade}.

Medical: Increases medical nanites by {NaniteConstructionManager.Settings.LifeSupportNanitesPerUpgrade}.

Mining: Increases mining nanites by {NaniteConstructionManager.Settings.MiningNanitesPerUpgrade}.

Speed: Reduces nanite travel time by {NaniteConstructionManager.Settings.SpeedIncreasePerUpgrade} seconds.

Power: Reduces nanite power consumption by {NaniteConstructionManager.Settings.PowerDecreasePerUpgrade}MW.

";
                    break;

                case "/nanite credits":

                    title = "Credits";
                    message = $@"
<--- {Localize(28)} --->

- Nukeguard -
Modeling, textures, block definitions, distribution

- Tysis -
Programming of original mod

- Fank -
Programming, concept/implementation focus

- Splen -
Programming, optimization/performance focus
Documentation/help/tutorials

- BookBurner -
Programming, optimization/dedicated server focus
help update

<--- GitHub --->

https://github.com/nukeguard/NaniteConstructionSystem
Please post any bug reports, feature suggestions and other
issues here. Include your log files and config for bug
reports. No exceptions.

<--- {Localize(30)} --->

Current version can always be found on workshop published by
nukeguard.

<--- {Localize(31)} --->

Thank you for downloading and supporting this mod. We've
all worked very hard on it. Thanks for making the Space
Engineers community so great and giving us the opportunity
to work on one of the oldest, most well-known and downloaded mods.

And of course, thank you to Keen Software House for not giving
up after so many years of development, even after the peak
popularity has died down. Your commitment to your community has
given us the motivation to keep this mod alive.

";
                    break;

                case "/nanite help cooperation":

                    title = "Facility Cooperation";
                    message = $@"
<--- {Localize(32)} --->

Friendly Nanite Control Facilities within {NaniteConstructionManager.Settings.MasterSlaveDistance}m of each
other will automatically join together and combine upgrades and
target scanning to both increase performance and productivity.

All settings within individual facilities are respected and no
additional configuration is needed. It should mostly feel like
nothing has changed, but you may notice a few things:

<--- {Localize(33)} --->

Inventories are shared between Facilities as well as their grids
and subgrids. That means that if two separate grids with
individual conveyor systems and cargo containers and their own
Nanite Control Facilities are floating nearby each other, they
may join up and be able to pull construction/repair and
projection components for jobs from each other's inventories.
This may or may not be desired, but for now, it's not
configurable. Just be aware of it.

Conversely, items retrieved from deconstruction jobs, cleanup
jobs and mining will sometimes appear in friendly facilities'
inventories. This is normal.

In the future, it may be possible for the developers to add
better support for inventory operations (maybe).
";
                    break;

                case "/nanite help mining":

                    if (!NaniteConstructionManager.Settings.MiningEnabled)
                        donothing = true;

                    title = "Mining";
                    message = $@"
<--- {Localize(34)} --->

When this box is checked in a Nanite Control Facility, it will scan
for nearby Nanite Mining Beacons within {NaniteConstructionManager.Settings.MiningMaxDistance}m.
If Nanite Mining Beacon is found, it is used as a center of a small
sphere in which the mining targets are searched for. You can limit
the ores that are supposed to be mined at that position by using
the select in the Nanite Mining Beacon. Mining beacon is typed as a
small battery, so it doesn't despawn by itself and it renames it's
own grid, so most common server cleanups should not trash it as well.

If properly configured, nearby, online Nanite Control Facilities
will produce RED nanites, which will then travel to the desired
ores and extract them with surgical precision. No undesired
materials will be disturbed, even if the ore vein is completely
surrounded by stone, dirt or ice.

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more mining nanites at once,
install Nanite Mining Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- {Localize(22)} --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the mining targets.

Ensure the proper checkbox is enabled on the facility itself
and that the facility is within {NaniteConstructionManager.Settings.MiningMaxDistance}m of the
desired ore and Mining Beacon.

The facility will attempt to automatically clear its inventory
space if needed to make room for mined ore. Please ensure that
the conveyor system is properly connected to cargo containers
or refineries that can receive ore if needed. Please note
that newly added cargo containers may not be immediately detected
by the Nanite Control Facility. Please give it a few minutes
to rescan the grid when new blocks are installed.
";
                    break;

                case "/nanite help medical":

                    if (!NaniteConstructionManager.Settings.LifeSupportEnabled)
                        donothing = true;

                    title = "Medical";
                    message = $@"
<--- {Localize(35)} --->

When this box is checked in a Nanite Control Facility, it will scan
for nearby players within {NaniteConstructionManager.Settings.LifeSupportMaxDistance}m of the facility.
If an injured player is found, the facility will produce
WHITE nanites that will slowly increase the player's health.

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more medical nanites at once,
install Nanite Medical Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- {Localize(22)} --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the medical targets.

Ensure the proper checkbox is enabled on the facility itself
and that the injured player is within the range described above.
";
                    break;

                case "/nanite help deconstruction":

                    if (!NaniteConstructionManager.Settings.DeconstructionEnabled)
                        donothing = true;

                    title = "Deconstruction";
                    message = $@"
<--- Deconstruction Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
for nearby Nanite Beacons or Nanite Area Beacons for grids that
are marked for deconstruction.

For more info on properly setting up beacons, type in chat:
/nanite help beacons

When a potential grid is found, the Facility scans the blocks on
the grid to determine the best possible order for deconstruction,
saving the beacon itself for last, if one exists. Then, CYAN
nanites will be produced by the facility. They will move to the
target blocks, spend some time grinding, and then turn GREEN
if the grind was successful or RED on failure.

For more information on the colors of the factory and nanite:
/nanite help colors

The components recieved from the grind will then be moved to
the facility's inventory. If there's no room, the components
will appear floating where the block once was.

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more deconstruction nanites at once,
install Nanite Deconstruction Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- {Localize(22)} --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the deconstruction targets.

To ensure components can be moved to the Facility, make sure
there's enough inventory space in the facility itself and that
the conveyor system is properly connected. Please note that
new inventory blocks, such as cargo containers, may not
immediately be detected by the facility. Please give the
facility a few minutes to detect new blocks added to the grid.
";
                    break;

                case "/nanite help projections":

                    if (!NaniteConstructionManager.Settings.ProjectionEnabled)
                        donothing = true;

                    title = "Projections";
                    message = $@"
<--- Projection Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
the Facility's grid group (which includes connected subgrids)
for projectors that are currently projecting blueprints.

When a potential target is found, the Facility scans the connected
inventory of the grid and subgrids for the parts needed to add the
first component to each block on the blueprint, starting with a
block that has a physical connection to an existing block.
If the parts cannot be found and the proper settings are
enabled, the Facility will also attempt to queue up these parts
for production in an assembler.

For more information on configuring assemblers, type in chat:
/nanite help assemblers

To build on nearby grids that are not connected to the facility's
grid group, consider using a Nanite Beacon. Fore info, type:
/nanite help beacons

If the needed components are available, PINK nanites will be
created by the facility and move to the target block. After
spending some time welding, GREEN nanites indicate success,
and RED nanites indicate failure.

For more information on the colors of the factory and nanite:
/nanite help colors

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more projection nanites at once,
install Nanite Construction Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- {Localize(22)} --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the projection targets.

To ensure components can be moved to the Facility, make sure
there's enough inventory space in the facility itself and that
the conveyor system is properly connected. Please note that
new inventory blocks, such as cargo containers, may not
immediately be detected by the facility. Please give the
facility a few minutes to detect new blocks added to the grid.
";
                    break;

                case "/nanite help repair":
                case "/nanite help construction":

                    if (!NaniteConstructionManager.Settings.ConstructionEnabled)
                        donothing = true;

                    title = "Construction/Repair";
                    message = $@"
<--- Construction/Repair Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
the Facility's grid group (which includes connected subgrids)
for deformed, incomplete or damaged blocks.

When a potential target is found, the Facility scans the connected
inventory of the grid and subgrids for the parts needed to do the
job. If the parts cannot be found and the proper settings are
enabled, the Facility will also attempt to queue up these parts
for production in an assembler.

For more information on configuring assemblers, type in chat:
/nanite help assemblers

To repair nearby grids that are not connected to the facility's
grid group, consider using a Nanite Beacon. Fore info, type:
/nanite help beacons

If the needed components are available, BLUE nanites will be
created by the facility and move to the target block. After
spending some time welding, GREEN nanites indicate success,
and RED nanites indicate failure.

For more information on the colors of the factory and nanite:
/nanite help colors

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more projection nanites at once,
install Nanite Construction Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- {Localize(22)} --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the repair targets.

To ensure components can be moved to the Facility, make sure
there's enough inventory space in the facility itself and that
the conveyor system is properly connected. Please note that
new inventory blocks, such as cargo containers, may not
immediately be detected by the facility. Please give the
facility a few minutes to detect new blocks added to the grid.
";
                    break;

                case "/nanite help cleanup":

                    if (!NaniteConstructionManager.Settings.CleanupEnabled)
                        donothing = true;

                    title = "Cleanup";
                    message = $@"
<--- Cleanup Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
the area within {NaniteConstructionManager.Settings.CleanupMaxDistance}m from the Facility
for loose objects that can normally be picked up by the player.

If a target is found, YELLOW nanites will be produced by the
facility and move toward the target. The item will then be
added to the Facility's inventory.

If the Nanite Facility's inventory is over 75% full, the facility
will flush its inventory into any free cargo containers on the grid.
This is to make room for the objects that the cleanup nanites are
trying to pick up.

In the future, this functionality will be more configurable and
obey sorters and other inventory rules.

To reduce the time it takes for nanites to clean up objects,
install Nanite Speed Upgrades on any of the facility's eight
upgrade slots.

To increase the amount of cleanup nanites the factory can
produce simultaneously, install Nanite Cleanup Upgrades.

For more detailed information on upgrades, type in chat:
/nanite help upgrades

<--- {Localize(22)} --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the cleanup targets.

To ensure items can be moved to the Facility, make sure
there's enough inventory space in the facility itself and that
the conveyor system is properly connected. Please note that
new inventory blocks, such as cargo containers, may not
immediately be detected by the facility. Please give the
facility a few minutes to detect new blocks added to the grid.
";
                    break;

                case "/nanite help beacons":
                    title = "Beacons";
                    message = $@"
<--- Nanite Beacons --->

Nanite Control Facilities will automatically queue jobs for targets
on the same grid or attached as subgrids, but what about for other
friendly grids nearby? This is where Beacons come in handy.

There are three beacons: Repair, projection and deconstruction.

REPAIR: When built on a grid, Nanite Facilities within {NaniteConstructionManager.Settings.ConstructionMaxBeaconDistance}m
will scan that grid for blocks that need repaired.

PROJECTION: These should be built on a grid that has a projector that
is projecting a blueprint to be built. All Nanite Control Facilities
within {NaniteConstructionManager.Settings.ProjectionMaxBeaconDistance}m will build that projection.

DECONSTRUCTION: When this beacon is built on a grid, Facilities within
{NaniteConstructionManager.Settings.DeconstructionMaxDistance}m will grind down every block on that grid.

MINING: When this beacon is built onto terrain (planet or asteroid) within
{NaniteConstructionManager.Settings.MiningMaxDistance}m of the nanite facility, it will mark the voxel for
mining.

Please note that control panel checkboxes on the Nanite Control
Facility must still be properly configured for the beacons to be
detected.
";
                    break;

                case "/nanite help basics":
                    title = "Basics";
                    message = $@"
<--- What are Nanites? --->

Nanites are tiny, flying machines that can easily do many things.
Here's what they are currently configured to do:

- Welding: {(NaniteConstructionManager.Settings.ConstructionEnabled ? "Enabled" : "Disabled")}
- Grinding: {(NaniteConstructionManager.Settings.DeconstructionEnabled ? "Enabled" : "Disabled")}
- Mining: {(NaniteConstructionManager.Settings.MiningEnabled ? "Enabled" : "Disabled")}
- Cleanup loose objects: {(NaniteConstructionManager.Settings.CleanupEnabled ? "Enabled" : "Disabled")}
- Build projections: {(NaniteConstructionManager.Settings.ProjectionEnabled ? "Enabled" : "Disabled")}
- Heal players: {(NaniteConstructionManager.Settings.LifeSupportEnabled ? "Enabled" : "Disabled")}

Nanites are created and given orders in a Nanite Control Facility.
These 'factory' blocks can be built on any grid just like any
other block in Space Engineers.

Many players use nanites to greatly reduce the time spent doing
the more tedious tasks in the game, such as welding hundreds
of armor blocks by hand. More advanced players can use them to
extract ore from asteroids and planets without touching a drill,
set up automatic ship factories using projectors, or create
chop shops that deconstruct grids within range.

<--- Getting Started --->

First, you'll need to build a Nanite Control Facility.

It's usually pretty expensive, so make sure you have a good amount of
resources and a good grasp of production mechanics before you begin.
The large grid version is 3x3x3, with upgrade slots that stick out
an additional block on each side (5x5x3), so plan accordingly.

Build the facility so it has access to your grid's conveyor system.
The large grid facility has five conveyor connections. One is on
the bottom in the very middle. If viewed from the bottom, that side
looks like this:

                    XXX
                    XOX
                    XXX

where O is the conveyor connection.

The others are on each side on the bottom middle like so:

                    XXX
                    XXX
                    UOU

where O is the conveyor connection and U is where upgrades
can be installed. Upgrade slots CANNOT be used for conveyor
connections, and conversely, conveyor connections will not
support upgrades.

For more information about upgrades, type in chat:
/nanite help upgrades

When repairing blocks and building projections, the facility will
use your grid's connected inventory blocks, such as cargo
containers, to find the parts it needs to do its job.

Power availability can also limit the factory's performance.
Make sure you have ample power and fuel available, especially before
adding upgrades or building additional facilities.

<--- Configuration --->

Once your factory is up and running, open the terminal and scroll down
to see the various checkboxes. Here, you can control what the factory
will or wont do. 'Use Assemblers' is off by default. Check this box
to allow the factory to use assemblers on the same conveyor system
to create parts that are needed for jobs. For more information, type:
/nanite help assemblers
";
                    break;

                case "/nanite help assemblers":
                    title = "Assemblers";
                    message = @"
<--- Nanite Facility Configuration --->

Open the terminal of your Nanite Control Facility. 'Use Assemblers' is
off by default. Check this box to allow the factory to use assemblers
on the same conveyor system to create parts that are needed for building
jobs (construction/repair and projection building).

<--- Assembler Configuration --->

Next, open the terminals of all assemblers that will be used by the
Nanite Control Facility. Check the 'Nanite Factory Queuing' box.
Then, open the production tab and make sure the assembler is set to
build, not deconstruction.

Please note that all standard rules for assemblers still apply.
They will not manufacture the parts if they are missing the
required raw materials, such as ingots.

If properly configured, your Nanite Control Facility will now
automatically attempt to queue up parts to be manufactured when in
the 'Missing Parts' state (blinking yellow). Just a small warning,
Nanite Facility tend's to overproduce. To see more information
about the colors used in this mod, type in chat:
/nanite help colors
";
                    break;

                case "/nanite help colors":
                    title = "Colors";
                    message = @"
<--- Nanite Facility Colors --->

The Nanite Control Facilities color emitters (located on the 'arms' of
the block) will indicate the current status of the facility.

RED means the facility is disabled. You can turn the facility on and
off by accessing the grid control panel from any terminal, including
the 'monitor' on the Nanite Control Facility itself.

DARK PURPLE (with lightning effects) means the facility is active.
The center spinning orb will move up or down depending on the
status of active jobs. When all the way up, the facility will
make crackling sounds and the lightning will appear. This is the
state that actively creates the nanites themselves.

DEEP PINK (BLINKING) means the facility is missing parts for a job.
Either supply the missing components manually, or check out this
command for more information about automatic assembly:
/nanites help assemblers

DARK YELLOW (BLINKING) means the facility does not have enough
power to enter a nominal state. Increase the grid's power by
creating new reactors/batteries/solar panels or adding more fuel.

LIME (BLINKING) means the facility has invalid targets. This state
is a bit of a wildcard as many things can go wrong, such as
unfriendly facilities already working on targets. Fore more
information on this status, open the terminal of the facility
and scroll down in the information area on the right side.

DARK GREEN means the facility is enabled but currently has no
jobs to do. As long as the proper checkboxes are enabled in the
control panel, the facility will always be actively scanning for
jobs to do while in this state. Certain conditions must be met
for certain situations, however. See the help commands for
mining, beacons and area beacons for more information.

<--- Nanite Colors --->

The nanites themselves will have different colors depending on
what job they are doing and what status they have.

GREEN is a nanite returning from a completed task.

BRIGHT RED is a nanite moving to a mining target (ore or stone).
DARK RED is a nanite coming back from a mining target.

YELLOW is a nanite moving to clean up loose items and bring
them back to the facility's inventory.

PINK is a nanite moving to build a projection target.

CYAN is a nanite moving to deconstruct a block.

WHITE is a nanite moving to heal a player.

BLUE is a nanite moving to construct/repair a block.

For non-mining targets, DARK RED means a nanite is returning
from a task that has failed for some reason.

";
                    break;

                default:
                    donothing = true;
                    break;
            }

        }

    }
}
