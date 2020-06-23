# P4EditVS

Simple, lightweight Visual Studio extension that allows you to checkout and revert files in Perforce without slowing down or blocking your IDE. 

Use menu bar or solution explorer context menu.

![alt text](Screenshot.png "Example")


Supported Commands
* Checkout
* Revert
* Revert If Unchanged

Notes
* p4.exe must be accessible by commandline.
* Currently only 1 workspace setting supported at once.
* The extension does not send or collect any information, settings are only stored locally. 
* This extension silently fails as it does not receive any information from the Perforce server, it only issues commands to it.
* Checkout/revert state is only determined by file read-only flag.
* This is not intended to replace the much more functional offical P4VS extension.
