# P4EditVS

Simple, lightweight Visual Studio extension that allows you to checkout and revert files in Perforce without slowing down or blocking your IDE. 

![alt text](Screenshot.png "Example")


Supported Commands
* Checkout
* Revert
* Revert If Unchanged
* History
* Diff Against Have Revision
* Time-lapse View
* Revision Graph

Notes
* p4.exe must be accessible from PATH for checkout/revert.
* p4v.exe must be accessible from PATH for history/diff/timelapse view/revision graph.
* Up to 6 workspace settings supported.
* The extension does not send or collect any information, settings are only stored locally. 
* This extension silently fails as it does not receive any information from the Perforce server, it only issues commands to it.
* Checkout/revert state is only determined by file read-only flag.
* For more advanced functionality see the offical P4VS extension.
