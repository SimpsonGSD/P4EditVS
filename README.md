# P4EditVS                  
[![Build Status](https://dev.azure.com/simpsongsd/P4EditVS/_apis/build/status/SimpsonGSD.P4EditVS?branchName=master)](https://dev.azure.com/simpsongsd/P4EditVS/_build/latest?definitionId=2&branchName=master) [![VS Marketplace](https://vsmarketplacebadge.apphb.com/version/ScottSimpson.p4editvs.svg)](https://marketplace.visualstudio.com/items?itemName=ScottSimpson.p4editvs)

Download the latest release from either the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=ScottSimpson.p4editvs) or [GitHub](https://github.com/SimpsonGSD/P4EditVS/releases).

Simple, lightweight Visual Studio extension that allows you to checkout and revert files in Perforce without slowing down or blocking your IDE. 

## Features

![alt text](https://raw.githubusercontent.com/SimpsonGSD/P4EditVS/master/Screenshot.png "Example")

### Supported Commands

* Checkout
* Revert
* Revert If Unchanged
* History
* Diff Against Have Revision
* Time-lapse View
* Revision Graph
* Add
* Delete
* Auto-Checkout (optional)

## Requirements

You'll need a relatively recent version of the Perforce client with
`p4vc` installed. Version `2020.2/2013107` is known to work.

## Configuration

Visit `Tools` > `Options`, `P4EditVS` section.

Specify client, server and user names for up to 6 workspaces. Any
workspaces configured here will show up in the `P4EditVS` menu, so you
can switch between them easily.

Set `Allow Environment` to `True` to enable a 7th workspace in the
`P4EditVS` menu: `(Use environment)`. When selected, P4EditVS runs
`p4` with no additional parameters, with the `p4` working folder being
the folder containing the source file in question. The
[p4config](https://www.perforce.com/manuals/v16.2/cmdref/P4CONFIG.html)
rules apply, if you use that mechanism; otherwise, you get whatever
settings are set with `p4 set` and/or `Connection` > `Environment
Settings...` in p4v.

The selected workspace is saved in the suo file for each solution.

## Notes

* p4.exe must be accessible from PATH for checkout/revert.
* p4v.exe must be accessible from PATH for history/diff/timelapse view/revision graph.
* Up to 6 workspace settings supported.
* The extension does not send or collect any information, settings are only stored locally. 
* This extension silently fails as it does not receive any information from the Perforce server, it only issues commands to it.
* Checkout/revert state is only determined by file read-only flag.
* For more advanced functionality see the offical P4VS extension.
* `(Use environment)` is not much use with p4config files, as p4 is
  not run from the file's folder. This may or may not improve.

## Troubleshooting

Switch the Output Window to P4EditVS to see P4 command logging to help identify issues if commands are not being executed as expected. 
This will help quickly identify issues such as being logged out of the server, expired tickets, etc.

## Building from Visual Studio

Load `P4EditVS.sln` into Visual Studio 2017. Build. A double-clickable
vsix file will be produced in `P4EditVS/bin/Debug` or
`P4EditVS/bin/Release`.

To debug the addin, you can run Visual Studio in the debugger. Right
click the `P4EditVS` project in the Solution Explorer, select
`Properties`, and visit the `Debug` section.

Select `Start external program`, and find the appropriate
`devenv.exe`. (For example, `C:\Program Files (x86)\Microsoft Visual
Studio\2017\Professional\Common7\IDE\devenv.exe`.)

In `Command line arguments`, enter `/rootsuffix Exp /resetsettings`.

Then run. You get a second copy of Visual Studio, with the addin
loaded.

(`/rootsuffix Exp` directs Visual Studio to use a completely different
set of registry keys and whatnot. For good or for ill, the child copy
of Visual Studio is sandboxed in this respect, and doesn't share
settings with your usual copy.)

## Contributions

Contributions are welcomed where they improve the user experience and do not affect IDE performance.
