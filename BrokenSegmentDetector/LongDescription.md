Read the description. It is very important.

This is a barebones diagnostic script for saves that are suffering from the ElectricityFlowEdge and No Connected Edge errors and save file corruption. 

## Dependencies
None

## Donations
If you want to say thank you with a donation you can do so on Paypal.

# Detailed Description
If your save isn't loading (aka the game is crashing after pressing Load Game), you need to use Skyve's Safe Mode (or "--burst-disable-compilation" launch parameter) to investigate the issue. 

Safe Mode will allow the game to not crash but you can't play on this mode regularly. If you encounter an error message that mentions ElectricityFlowEdge then you have a use for this mod.

With this mod and while running the game in safe mode from Skyve, load your save and this will highlight broken segments around broken nodes so that they can be manually removed.

There are no settings for this mod, yet. There is no UI for this mod, yet.

Please note: networks whose prefab did not load correctly are filtered out as they usually generate false positives otherwise.

This will delete edges (segments) with no connected edge (nodes) on game load which involves looping through all networks in the save.
  
# Instructions

1. Include this mod in your playset.
2. Run the game in Skyve safe mode or use "--burst-disable-compilation" launch parameter. 
3. Load the afflicted save. 
4. Remove highlighted segments so that the nodes are removed. You can also check the log file for the mod "..\Logs\BrokenSegmentDecector.log".
5. Save As your save game.
6. Relaunch game without safe mode. Remove "--burst-disable-compilation" launch parameter if you used that.
7. Load and verify that your save game is no longer corrupted 
8. You can now remove the script from your playset.  

## Disclaimer
This mod may not detect the problems in every save affected by this error. Providing your save to the tech support channel on Cities Skylines Modding discord, can further the development of this mod's ability to detect more broken segments.

Anarchy v1.7.2 was known to sometimes produce this problem in people's saves. 

## Support
I will respond on the code modding channels on **Cities: Skylines Modding Discord**

## Credits 
* yenyang - Mod Author
* StarQ for providing tech support including: determining how the problem could be resolved, finding broken segments, and fixing people's saves.
* Chameleon TBN - Logo
* krzychu124 for developing Scene Explorer which was vital in determining the cause of these problems.
* Those that provided saves and feedback that helped this mod develop.