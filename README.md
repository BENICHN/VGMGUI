# VGMGUI
A graphic interface for vgmstream.

:warning: Once vgmgui has downloaded vgmstream, you'll have to rename the file "vgmstream-cli.exe" to "test.exe" in the "vgmstream" folder.

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
### 1.2.1
 - Enable the [ ! ] button in the searchbox to hide files that contains the entered keywords instead of hiding the others (reverse search)
 - Tooltips added to list items to see all data they contain
 - The new button in the status bar allows you to resize elements and window as default
 - Bug fixes and stability improvements
   - Replaced ⬤ character by an ellipse int the status bar because some devices does not support this symbol
   - The temporary file created when downloading VLC is no longer deleted when the application is closed, which makes it possible to extract VLC
   - The output sample quantity is now checked (error if this number is too big for an int) for each file when changing the output settings
   - Fixed "Rise of..." and "Descend of..." commands when the number exceeds the maximal or minimal index of the list
   - Fixed error when clicking outside a SwitchableTextBox with `<Multiple>`
   - Other...
### 1.3.0
 - Replaced the [ ! ] button in the searchbox by a [ .* ] regex button
 - Informations about conversion in status bar
 - Files copy and paste feature
 - Removed DKCTF CSMP additional format, now supported by vgmstream (DKCTFCSMP.cs stays avaliable)
 - When adding files, click on the errors count to view them
 - It's now possible to remove files with the same name or the same path in the list or prevent adding files with the same name (ignore case, including extension)
 - Bug fixes and stability improvements
    - Optimizations in the determination of the files destinations
    - Until now, when a temporary file was created, .NET generated a .tmp file and VGMGUI created a file with the same name and changed the extension but DIDN'T deleted the .tmp file resulting in a multiplication of empty .tmp files. This bug is now fixed
    - LINQ and collections optimizations
    - Fixed player volume
    - Fixed loop icon
    - Other...
