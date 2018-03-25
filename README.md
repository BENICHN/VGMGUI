# VGMGUI
A graphic interface of vgmstream

![Main interface](https://raw.githubusercontent.com/BenNatNB/VGMGUI/master/VGMGUI/Documentation/EN/VGMGUI.png)

## Changelog
### 1.1.0
 - Added a status bar
 - Added support for DKCTF CSMP format, which is not in vgmstream
 - Change of player from CSCore to VLC
 - Added bit rate info
 - Waiting dialogs now contains more informations
 - Added Windows Explorer properties in file to context menu
 - It's now possible to free memory and delete temporary files without stopping playback (Shift+S)
 - Bug fixes and stability improvements
### 1.2.0
 - New algorithm for DKCTF CSMP
 - Hours on samples removed if necessary
 - When scanning files, the number of errors is now displayed on the waiting dialog
 - Added output total samples info
 - You can now, during the conversion, skip individual files
 - Because VLC 3.0 is now stable, you can play a file by streaming the stdout of vgmstream (but you can only move forward a few minutes)
 - Items added in status bar
   - Icons
   - Streaming mode toggle button
   - Analyze files when adding checkbox
   - Samples display mode toggle button
   - Search delay
   - Context menu to select items you want to display
 - Press Ctrl+Space to scroll into the currently playing file
 - Media keys can now control playback
 - Use Costura.Fody to embed assemblies
 - Bug fixes and stability improvements
   - Asynchronous methods are now all awaited (except those that should not be)
   - Temporary files are better handled
   - Removed FFmpeg, which was only used to merge audio channels (replaced by NAudio)
   - The plugins of VLC are now automatically cached, because VLC 3.0 doesn't do it
   - You now can't play next or previous file if the current is not completely loaded
   - Files with spaces in their output path can now be converted
   - Fixed bug that prevents downloading another version of VLC than 2.2.8
   - Improved pause and cancel features for conversion, scanning and playback
   - Other...