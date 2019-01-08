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
                case (1):
                    switch (language)
                    {
                        default:
                            return "This is likely due to a list being modified during enumeration in a parallel thread, which is probably harmless and can be ignored.";
                    }
                    break;
                case (2):
                    switch (language)
                    {
                        default:
                            return "This section includes some debugging variables that can help the devs solve problems. Please use this command when reporting errors and include the output in your bug report.";
                    }
                case (3):
                    switch (language)
                    {
                        default:
                            return "All target processing moved to parallel for better performance";
                    }
                case (4):
                    switch (language)
                    {
                        default:
                            return "Code optimized and made more stable for dedicated servers";
                    }
                case (5):
                    switch (language)
                    {
                        default:
                            return "New models! Old models have a rusty look. They can be torn down to retrieve parts";
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
<--- Debugging Information --->

{Localize(2)}

IsClient: {Sync.IsClient}
IsServer: {Sync.IsServer}
IsDedicated: {Sync.IsDedicated}
IsOffline: {MyAPIGateway.Session.OnlineMode == VRage.Game.MyOnlineModeEnum.OFFLINE}

MyAPIGateway.Session.Player.Client: {MyAPIGateway.Session.Player.Client}
MyAPIGateway.Multiplayer.IsServerPlayer(Client): {MyAPIGateway.Multiplayer.IsServerPlayer(MyAPIGateway.Session.Player.Client)}
";
                    break;
                case "/nanite changelog":
                    title = "Changelog";
                    message = $@"
<--- Version 2.0! Jan. 6, 2019 --->

- {Localize(3)}

- {Localize(4)}

- {Localize(5)}

- New mining logic and ore detector block. Install a Nanite Ore Detector near a Nanite Control Facility that is near some voxels

- Nearby, friendly facilities now automatically share upgrades and grid inventories

- New help documentation. Type: /nanite help

- New logging system for admins. For info, type: /nanite help config

- Projector upgrade removed. Construction upgrade now also affects projection nanites
";
                    break;

                case "/nanite help":
                    title = "Help";
                    message = $@"
<--- Commands --->

/nanite help
/nanite help basics
/nanite help assemblers
/nanite help beacons
/nanite help colors
{(NaniteConstructionManager.Settings.CleanupEnabled ? "/nanite help cleanup" : "")}
{(NaniteConstructionManager.Settings.ConstructionEnabled ? "/nanite help construction OR /nanite help repair" : "")}
{(NaniteConstructionManager.Settings.ProjectionEnabled ? "/nanite help projections" : "")}
{(NaniteConstructionManager.Settings.DeconstructionEnabled ? "/nanite help deconstruction" : "")}
{(NaniteConstructionManager.Settings.MedicalEnabled ? "/nanite help medical" : "")}
{(NaniteConstructionManager.Settings.MiningEnabled ? "/nanite help mining" : "")}
/nanite help upgrades
/nanite help cooperation
/nanite changelog
/nanite credits
";
                    break;

                case "/nanite help upgrades":

                    title = "Upgrades";
                    message = $@"
<--- Improving Performance --->

Upgrades allow the player to fine tune the capabilities of both
the Nanite Control Facility and the Nanite Ore Detector.
Here's what they do.

<--- Nanite Control Facility Upgrades --->

Construction: Increases construction/repair nanites by {NaniteConstructionManager.Settings.ConstructionNanitesPerUpgrade}
and projection nanites by {NaniteConstructionManager.Settings.ProjectionNanitesPerUpgrade}.

Deconstruction: Increases deconstruction nanites by {NaniteConstructionManager.Settings.DeconstructionNanitesPerUpgrade}.

Cleanup: Increases cleanup nanites by {NaniteConstructionManager.Settings.CleanupNanitesPerUpgrade}.

Medical: Increases medical nanites by {NaniteConstructionManager.Settings.MedicalNanitesPerUpgrade}.

Mining: Increases mining nanites by {NaniteConstructionManager.Settings.MiningNanitesPerUpgrade}.

Speed: Reduces nanite travel time by {NaniteConstructionManager.Settings.SpeedIncreasePerUpgrade} seconds.

Power: Reduces nanite power consumption by {NaniteConstructionManager.Settings.PowerDecreasePerUpgrade}MW.


<--- Nanite Ore Detector Upgrades --->

Range: Increases range by {NaniteConstructionManager.Settings.OreDetectorRangePerUpgrade}m.

Power: Decreases power usage by {NaniteConstructionManager.Settings.OreDetectorPowerPercentReducedPerEfficiencyUpgrade * 100} percent.

Scanning: Allows rare ores to be detected (max 2 upgrades).

Filter: Allows selection of ore data to be stored (max 1 upgrade).

";
                    break;

                case "/nanite credits":

                    title = "Credits";
                    message = $@"
<--- Meet the Developers --->

- Nukeguard -
Modeling, textures, block definitions, distribution

- Tysis -
Programming of original mod

- Fank -
Programming, concept/implementation focus

- Splen -
Programming, optimization/performance focus
Documentation/help/tutorials

<--- Localization contributors --->

<--- GitHub --->

https://github.com/nukeguard/NaniteConstructionSystem
Please post any bug reports, feature suggestions and other
issues here. Include your log files and config for bug
reports. No exceptions.

<--- Testing Server --->

Splen's Server, STC Trading Co., will always be using the
latest development version of Nanite Control Facility.
If you want to test new features before they appear in the
live version, join us! Get the server address, rules and
more information at https://discord.gg/neAUzaq

<--- A Sincere Thanks --->

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
<--- Working Together --->

Friendly Nanite Control Facilities within {NaniteConstructionManager.Settings.MasterSlaveDistance}m of each
other will automatically join together and combine upgrades and
target scanning to both increase performance and productivity.

All settings within individual facilities are respected and no
additional configuration is needed. It should mostly feel like
nothing has changed, but you may notice a few things:

<--- Shared Resources --->

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
better support for inventory operations.
";
                    break;

                case "/nanite help mining":

                    if (!NaniteConstructionManager.Settings.MiningEnabled)
                        donothing = true;

                    title = "Mining";
                    message = $@"
<--- Mining Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
for nearby Nanite Ore Detectors within {NaniteConstructionManager.Settings.OreDetectorToNaniteFactoryCommunicationDistance}m.
If an ore detector is found, the facility will attempt to download
any ore target information from the ore detector. This usually takes
a few seconds.

A Nanite Ore Detector is very different from the built-in ore
detectors that you are probably familiar with. Unlike the vanilla
ore detectors, a Nanite Ore Detector does NOT provide the player
with on-screen locations of ore. Instead, the Nanite Ore Detector
carefully scans nearby voxels (asteroids or planets) for their
material content, then saves the information in its onboard
data storage.

These blocks are fairly large (3x5x3) and also have eight upgrade
slots which can take up an additional two spaces on each side.
Plan accordingly.

There's no need to connect the ore detector to any conveyor system.
The only thing it needs is a fair amount of power. All data is
transferred wirelessly: It doesn't even have to be on the same
grid as any Nanite Control Facility.

Once your Nanite Ore Detector is built, consider installing some
upgrades. Nanite Ore Detector Scanning Upgrades allow the ore
detector to find more valuable materials, such as gold and
platinum. Only two are needed to maximize the scanning frequency.

Also consider installing a single Nanite Ore Detector Filter
Upgrade, which allows the user to select which ore location data
will be stored. This is good for filtering out more common materials
like stone or ice. Simple highlight the ores in the list that should
be stored (CTRL + click by default).

Install as many Nanite Ore Detector Range Upgrades as
desired. Each one will increase the detector's maximum range by {NaniteConstructionManager.Settings.OreDetectorRangePerUpgrade}m.

Actual range scanned is controlled by a slider in the control panel.
Larger ranges will take longer to scan the area, so be sure to only
scan what is needed to save time. To visualize the scanning area,
a convenient checkbox in the ore detector will project a spherical
measurement of the scanning range.

Finally, consider installing Nanite Ore Detector Power Efficiency
Upgrades if power consumption is a concern. Each upgrade will reduce
the total power consumed by {NaniteConstructionManager.Settings.OreDetectorPowerPercentReducedPerEfficiencyUpgrade * 100} percent.

ALL of the above mentioned upgrades are installed on the Nanite
Ore Detector itself, NOT on the Nanite Control Facility.

WARNING: Multiple Nanite Ore Detectors within {NaniteConstructionManager.Settings.OreDetectorToNaniteFactoryCommunicationDistance}m
of each other will shut down automatically, as their radio waves
will block out each others' signals. Only one Ore Detector is
needed, even for many facilities within range. Once any problems
are rectified, restart the Ore Detector by switching it back on
in the control panel.

Once the Ore Detector has the desired upgrades installed and has
been configured properly in the control panel, turn it on. The
scanning radar in the middle of the detector will begin to quickly
spin. Monitor the progress of the scanning by viewing the info
terminal in the right side of the control panel.

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

<--- Troubleshooting --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the mining targets.

Ensure the proper checkbox is enabled on the facility itself
and that the facility is within {NaniteConstructionManager.Settings.MiningMaxDistance}m of the
desired ore.

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

                    if (!NaniteConstructionManager.Settings.MedicalEnabled)
                        donothing = true;

                    title = "Medical";
                    message = $@"
<--- Medical Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
for nearby players within {NaniteConstructionManager.Settings.MedicalMaxDistance}m of the facility.
If an injured player is found, the facility will produce
WHITE nanites that will slowly increase the player's health.

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more medical nanites at once,
install Nanite Medical Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- Troubleshooting --->

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

<--- Troubleshooting --->

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

<--- Troubleshooting --->

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

<--- Troubleshooting --->

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

<--- Troubleshooting --->

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

Please note that control panel checkboxes on the Nanite Control
Facility must still be properly configured for the beacons to be
detected.

<--- Area Beacons --->

An Area Beacon is a special beacon that defines an entire area,
rather than a specific grid, for operations. Build this block on
any grid within {NaniteConstructionManager.Settings.AreaBeaconMaxDistanceFromNaniteFacility}m from a Nanite Facility.
Then, configure the size and shape of the area in the control panel.
Finally, use the checkboxes to determine what operations should take
place within that area (if you're making a chop shop, ONLY check
Deconstruction or you may get undesired results).
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
- Heal players: {(NaniteConstructionManager.Settings.MedicalEnabled ? "Enabled" : "Disabled")}

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
the 'Missing Parts' state (blinking yellow). To see more information
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
