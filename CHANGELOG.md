# Change log

## 2.6.5
**2022-05-13**
* Fix issue where files opened for merge/integrate could not be checked out.

## 2.6.4
**2022-03-17**
* Better support for case-sensitive Perforce servers. See [issue #12](https://github.com/SimpsonGSD/P4EditVS/issues/12) for more details.

## 2.6.3
**2022-03-04**
* Merged [pull request](https://github.com/SimpsonGSD/P4EditVS/pull/11) from [Tom Seddon](https://github.com/tom-seddon) that makes the Time-lapse View command open the time-lapse view at the current line.

## 2.6.2
**2022-03-01**
* VS crash fix for Open P4V command [issue #21](https://github.com/SimpsonGSD/P4EditVS/issues/21).

## 2.6.1
**2021-11-23**
* Merged [PR](https://github.com/SimpsonGSD/P4EditVS/pull/18) to display P4EditVS command results in VS status bar. 

## 2.6
**2021-08-20**
* Fixed issue with .filters file not being checked out with project [(issue #13)](https://github.com/SimpsonGSD/P4EditVS/issues/13).
* Attempted a fix for case-sensitive Perforce servers not being issues commands with file paths is correct case [(issue #12)](https://github.com/SimpsonGSD/P4EditVS/issues/12). 
* Merged [pull request](https://github.com/SimpsonGSD/P4EditVS/pull/10) from [Eris Koleszar](https://github.com/bellicapax) to add a checkout prompt filter for specific files.

## 2.5
**2021-04-29**
* Added option to disable using read-only flag to support [allwrite workspaces](https://github.com/SimpsonGSD/P4EditVS/issues/9).

## 2.4
**2021-04-29**
* Added configurable command timeout.

## 2.3
**2021-02-08**
* Added Open in P4V command.
* Moved saved workspace settings to %appdata%/P4EditVS to avoid issues with .suo files being deleted in temporary workspaces.

## 2.2
**2021-01-14**
* Added commands to file tab context menu.
* Set allow environment setting to true by default.
* Made environment setting the default workspace.
* Set auto-checkout setting to true by default.

## 2.1
**2020-12-20**
* VS2019 support.

## 2.0
**2020-10-12**  
(Thanks to Tom Seddon for the following changes)
* P4CONFIG file now supported.
* Fixed error in Output Window that occurred for some commands.

## 1.9
**2020-10-09**
* Fixed [[Bug] Handling spaces in file path](https://github.com/SimpsonGSD/P4EditVS/issues/4)

## 1.8
**2020-10-08**

* Fixed issue where performing operations on project files in the Solution Explorer would check out all project items. Now only .filters are checked out too.

## 1.7
**2020-10-02**

* Add and Delete command now supported.

## 1.6
**2020-10-01**

* Auto-checkout is now avaiable in the Options page.

Auto-checkout will checkout edited read-only files on save/build.

## 1.5
**2020-09-28**

* Checking out files will now run commands on associated files. E.g. checking out a project will also checkout the .filters file, checking out a C# form will checkout the .resx and .cs, etc.
* Multiselection now supported when using right-click context menu in Solution Explorer.

(Thanks to Tom Seddon for the following changes)

* Can now view P4 command logging and results in P4EditVS output pane.
* Can now choose Allow Environment for workspace setting which will use config as shown by P4 Set (or see P4V Connection -> Environment Settings).
* Workspace selection is now saved per Solution in the .suo file.

## 1.4
**2020-07-14**

* Fixed diff against have revision not working somtimes

## 1.3.1
**2020-06-25**

* Fixed history icon on solution explorer menu.

## 1.3
**2020-06-25**

* Added new commands.
   * Diff against have revision.
   * History.
   * Time-lapse view.
   * Revision graph.
* Renamed checkout command so it can be identified easier in the keyboard shortcuts window.

## 1.2
**2020-06-23**

* Now supports up to 6 workspace settings.

## 1.1
**2020-06-15**

* Added commands to solution explorer context menus for files, solutions and projects.
* Fixed crash when no file was open and top level menu was accessed.
