using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using EnvDTE;
using System.IO;
using System.Collections.Generic;
using EnvDTE80;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio;

using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.Text;

namespace SDEditVS
{
    /// <summary>
    /// Command handler
    /// </summary>
    public class Commands
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CheckoutCommandId = 0x0100;
        public const int RevertIfUnchangedCommandId = 0x0101;
        public const int RevertCommandId = 0x0103;
        public const int CtxtCheckoutCommandId = 0x0104;
        public const int CtxtRevertIfUnchangedCommandId = 0x0105;
        public const int CtxtRevertCommandId = 0x0106;
        public const int Workspace1CommandId = 0x0107;
        public const int Workspace2CommandId = 0x0108;
        public const int Workspace3CommandId = 0x0109;
        public const int Workspace4CommandId = 0x010A;
        public const int Workspace5CommandId = 0x010B;
        public const int Workspace6CommandId = 0x010C;
        public const int DiffCommandId = 0x010D;
        public const int CtxtDiffCommandId = 0x010E;
        public const int HistoryCommandId = 0x010F;
        public const int CtxtHistoryCommandId = 0x0110;
        public const int RevisionGraphCommandId = 0x0111;
        public const int CtxtRevisionGraphCommandId = 0x0112;
        public const int TimelapseViewCommandId = 0x0113;
        public const int CtxtTimelapseViewCommandId = 0x0114;
        public const int WorkspaceUseEnvironmentCommandId = 0x115;
        public const int AddCommandId = 0x0116;
        public const int CtxtAddCommandId = 0x0117;
        public const int DeleteCommandId = 0x0118;
        public const int CtxtDeleteCommandId = 0x0119;
        public const int OpenInSDVCommandId = 0x011A;
        public const int CtxtOpenInSDVCommandId = 0x011B;

        public readonly int[] CommandIds = { CheckoutCommandId, RevertIfUnchangedCommandId, RevertCommandId, DiffCommandId, HistoryCommandId, RevisionGraphCommandId, TimelapseViewCommandId, AddCommandId, DeleteCommandId, OpenInSDVCommandId };
        public readonly int[] CtxtCommandIds = { CtxtCheckoutCommandId, CtxtRevertIfUnchangedCommandId, CtxtRevertCommandId, CtxtDiffCommandId, CtxtHistoryCommandId, CtxtRevisionGraphCommandId, CtxtTimelapseViewCommandId, CtxtAddCommandId, CtxtDeleteCommandId, CtxtOpenInSDVCommandId };
        public readonly int[] WorkspaceCommandIds = { WorkspaceUseEnvironmentCommandId, Workspace1CommandId, Workspace2CommandId, Workspace3CommandId, Workspace4CommandId, Workspace5CommandId, Workspace6CommandId };

        private static readonly string ADDIN_NAME = "SDEditVS";
        private static readonly string SUCCESS_PREFIX = "(\u2713) ";
        private static readonly string FAILURE_PREFIX = "(Failed) ";

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("bcd15dfb-5150-4bde-a3d0-520343ba88c5");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly SDEditVS _package;

        /// <summary>
        /// Files selected to apply source control commands to.
        /// </summary>
        private List<string> _selectedFiles = new List<string>();

        private EnvDTE80.TextDocumentKeyPressEvents _textDocEvents;
        //private EnvDTE.TextEditorEvents _textEditorEvents;
        private EnvDTE80.DTE2 _dte;
        // private UpdateSolutionEvents _updateSolutionEvents;
        private RunningDocTableEvents _runningDocTableEvents;

        /// <summary>
        /// Initializes a new instance of the <see cref="Commands"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private Commands(AsyncPackage package, OleMenuCommandService commandService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package = package as SDEditVS ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _dte = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2 ?? throw new ArgumentNullException(nameof(_dte));

            foreach (var cmdId in CommandIds)
            {
                var menuCommandID = new CommandID(CommandSet, cmdId);
                var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
                menuItem.Visible = true;
                commandService.AddCommand(menuItem);
            }

            foreach (var cmdId in CtxtCommandIds)
            {
                var menuCommandID = new CommandID(CommandSet, cmdId);
                var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatusCtxt);
                menuItem.Visible = true;
                commandService.AddCommand(menuItem);
            }

            foreach (var cmdId in WorkspaceCommandIds)
            {
                var menuCommandID = new CommandID(CommandSet, cmdId);
                var menuItem = new OleMenuCommand(this.ExecuteWorkspace, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatusWorkspace);
                menuItem.Visible = true;
                commandService.AddCommand(menuItem);
            }

            if (_package.AutoCheckoutOnEdit)
            {
                _textDocEvents = ((EnvDTE80.Events2)_dte.Events).get_TextDocumentKeyPressEvents(null);
                _textDocEvents.BeforeKeyPress += new _dispTextDocumentKeyPressEvents_BeforeKeyPressEventHandler(OnBeforeKeyPress);
            }
            //_textEditorEvents = ((EnvDTE80.Events2)_dte.Events).get_TextEditorEvents(null);
            //_textEditorEvents.LineChanged += new _dispTextEditorEvents_LineChangedEventHandler(OnLineChanged);

            // Subscribe to events so we can do auto-checkout
            //_updateSolutionEvents = new UpdateSolutionEvents(this);
            _runningDocTableEvents = new RunningDocTableEvents(this, _dte as DTE);


        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static Commands Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProviderAsync
        {
            get
            {
                return _package;
            }
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                return _package;
            }
        }

        private StreamWriter OutputWindow
        {
            get => _package.OutputWindow;
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in CheckoutCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new Commands(package, commandService);
        }

        /// <summary>
        /// Called before menu button is shown so we can update text and active state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var myCommand = sender as OleMenuCommand;
            if (null != myCommand)
            {
                if (_dte != null)
                {
                    // reset selection
                    _selectedFiles.Clear();

                    if (_dte.ActiveDocument != null)
                    {
                        _selectedFiles.Add(_dte.ActiveDocument.FullName);

                        string guiFileName = _dte.ActiveDocument.Name;
                        bool isReadOnly = _dte.ActiveDocument.ReadOnly;
                        // Build menu string based on command ID and whether to enable it based on file type/state
                        ConfigureCmdButton(myCommand, guiFileName, isReadOnly);
                    }
                    else
                    {
                        // Clear any cached file names in the UI
                        ConfigureCmdButton(myCommand, "", false);
                        // Invalid selection clear cached path and disable buttons
                        myCommand.Enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Called before menu button is shown so we can update text and active state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBeforeQueryStatusCtxt(object sender, EventArgs e)
        {
            Action<List<string>, string> AddSelectedFile = (selectedFile, file) =>
            {
                if (File.Exists(file))
                {
                    _selectedFiles.Add(file);
                }
            };

            ThreadHelper.ThrowIfNotOnUIThread();
            var myCommand = sender as OleMenuCommand;
            if (null != myCommand)
            {
                EnvDTE80.DTE2 applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
                if (applicationObject != null)
                {
                    // reset selection
                    _selectedFiles.Clear();

                    var selectedItems = applicationObject.SelectedItems;
                    bool isMultiSelection = selectedItems.Count > 1;

                    foreach (SelectedItem selectedFile in selectedItems)
                    {
                        // Is selected item a project?
                        if (selectedFile.Project != null)
                        {
                            AddSelectedFile(_selectedFiles, selectedFile.Project.FullName);

                            // Get .filter file, usually the first project item
                            foreach (ProjectItem item in selectedFile.Project.ProjectItems)
                            {
                                string projectItemFileName = item.FileNames[0];
                                if (projectItemFileName.EndsWith(".filters"))
                                {
                                    AddSelectedFile(_selectedFiles, projectItemFileName);
                                    break;
                                }
                            }
                        }
                        // Must be a source file
                        else if (selectedFile.ProjectItem != null)
                        {
                            // Add all paths in case there's multiple file paths associated
                            for (short fileIdx = 0; fileIdx < selectedFile.ProjectItem.FileCount; fileIdx++)
                            {
                                AddSelectedFile(_selectedFiles, selectedFile.ProjectItem.FileNames[fileIdx]);
                            }

                            // Some files have multiple files such as C# forms have a .cs and .resx files so pick these up here
                            foreach (ProjectItem projectItem in selectedFile.ProjectItem.ProjectItems)
                            {
                                AddSelectedFile(_selectedFiles, projectItem.FileNames[0]);
                            }
                        }
                        // If we still have something selected and it's not a project item or project it must be the solution (?)
                        else
                        {
                            AddSelectedFile(_selectedFiles, applicationObject.Solution.FullName);
                        }
                    }

                    if (_selectedFiles.Count > 0)
                    {
                        System.IO.FileInfo info = new System.IO.FileInfo(_selectedFiles[0]);
                        string displayName = isMultiSelection ? "(multiple files)" : info.Name;
                        // Build menu string based on command ID and whether to enable it based on file type/state
                        ConfigureCmdButton(myCommand, displayName, info.IsReadOnly);
                    }
                    else
                    {
                        // Invalid selection disable buttons
                        myCommand.Enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Get human-readable text for a command, suitable for, e.g., putting on a button.
        /// </summary>
        /// <param name="commandID">the command ID</param>
        /// <returns></returns>
        private string GetCommandText(int commandID)
        {
            switch (commandID)
            {
                case CheckoutCommandId:
                case CtxtCheckoutCommandId:
                    return "Checkout";

                case RevertIfUnchangedCommandId:
                case CtxtRevertIfUnchangedCommandId:
                    return "Revert If Unchanged";

                case RevertCommandId:
                case CtxtRevertCommandId:
                    return "Revert";

                case DiffCommandId:
                case CtxtDiffCommandId:
                    return "Diff Against Have Revision";

                case HistoryCommandId:
                case CtxtHistoryCommandId:
                    return "History";

                case TimelapseViewCommandId:
                case CtxtTimelapseViewCommandId:
                    return "Time-lapse View";

                case RevisionGraphCommandId:
                case CtxtRevisionGraphCommandId:
                    return "Revision Graph";

                case AddCommandId:
                case CtxtAddCommandId:
                    return "Mark for Add";

                case DeleteCommandId:
                case CtxtDeleteCommandId:
                    return "Mark for Delete";

                case OpenInSDVCommandId:
                case CtxtOpenInSDVCommandId:
                    return "Open in SDV";
            }

            // don't return null here or anything - the result of this should
            // always be something human-readable, as it goes in the UI.
            return string.Format("Unknown command: {0}", commandID);
        }

        /// <summary>
        /// Get human-readable description of a command that's operating on a file.
        /// </summary>
        /// <param name="commandID">the command ID</param>
        /// <param name="filePath">the path of the file it's operating on</param>
        /// <returns></returns>
        private string GetBriefCommandDescription(int commandID, string filePath)
        {
            // Try to get just the name part. Make the description shorter.
            string fileName;
            try
            {
                fileName = Path.GetFileName(filePath);
            }
            catch (Exception)
            {
                // the whole path will do in an emergency.
                fileName = filePath;
            }

            return string.Format("{0}: {1}", GetCommandText(commandID), fileName);
        }

        void ConfigureCmdButton(OleMenuCommand command, string name, bool isReadOnly)
        {
            bool useReadOnlyFlag = _package.GetUseReadOnlyFlag();

            switch (command.CommandID.ID)
            {
                case CheckoutCommandId:
                case CtxtCheckoutCommandId:
                    {
                        string text = GetCommandText(command.CommandID.ID);

                        // This is first command in the menu so rather than bloating the whole thing just display the filename at the top to the right
                        command.Text = command.Enabled ? string.Format("{0}\t\t\t{1}", text, name) : text;
                        command.Enabled = useReadOnlyFlag ? isReadOnly : true;
                    }
                    break;
                case RevertIfUnchangedCommandId:
                case CtxtRevertIfUnchangedCommandId:
                    {
                        string text = GetCommandText(command.CommandID.ID);

                        command.Text = command.Enabled ? string.Format("{0}\t\t{1}", text, name) : text;
                        command.Enabled = useReadOnlyFlag ? !isReadOnly : true;
                    }
                    break;
                case RevertCommandId:
                case CtxtRevertCommandId:
                    {
                        command.Text = GetCommandText(command.CommandID.ID);
                        command.Enabled = useReadOnlyFlag ? !isReadOnly : true;
                    }
                    break;
                case DiffCommandId:
                case CtxtDiffCommandId:
                    {
                        command.Text = GetCommandText(command.CommandID.ID);
                        command.Enabled = useReadOnlyFlag ? !isReadOnly : true;
                    }
                    break;
                case HistoryCommandId:
                case CtxtHistoryCommandId:
                    {
                        command.Text = GetCommandText(command.CommandID.ID);
                        command.Enabled = true;
                    }
                    break;
                case TimelapseViewCommandId:
                case CtxtTimelapseViewCommandId:
                    {
                        command.Text = GetCommandText(command.CommandID.ID);
                        command.Enabled = true;
                    }
                    break;
                case RevisionGraphCommandId:
                case CtxtRevisionGraphCommandId:
                    {
                        command.Text = GetCommandText(command.CommandID.ID);
                        command.Enabled = true;
                    }
                    break;
                case AddCommandId:
                case CtxtAddCommandId:
                    {
                        command.Text = GetCommandText(command.CommandID.ID);
                        command.Enabled = useReadOnlyFlag ? !isReadOnly : true;
                    }
                    break;
                case DeleteCommandId:
                case CtxtDeleteCommandId:
                    {
                        command.Text = GetCommandText(command.CommandID.ID);
                        command.Enabled = useReadOnlyFlag ? isReadOnly : true;
                    }
                    break;
                case OpenInSDVCommandId:
                case CtxtOpenInSDVCommandId:
                    {
                        command.Text = GetCommandText(command.CommandID.ID);
                        command.Enabled = true;
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_selectedFiles.Count == 0)
            {
                return;
            }

            var myCommand = sender as OleMenuCommand;
            foreach (string filePath in _selectedFiles)
            {
                ExecuteCommand(filePath, myCommand.CommandID.ID, false);
            }
        }

        /// <summary>
        /// Executes SD command for supplied command ID
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="commandId"></param>
        private void ExecuteCommand(string filePath, int commandId, bool immediate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_package.ValidateUserSettings())
            {
                return;
            }

            string fileFolder = null;
            try
            {
                fileFolder = Path.GetDirectoryName(filePath);
            }
            catch (Exception)
            {
                // If anything goes wrong, just use the default.
            }

            // I've had reports of visual studio dropping file path case which is a problem for
            // case-sensitive SD servers. To get around this grab the case-sensitive filepath.
            filePath = Misc.GetWindowsPhysicalPath(filePath);

            string globalOptions = _package.GetGlobalSDCmdLineOptions();
            string commandline = "";

            Action<Runner.RunnerResult> handler = CreateCommandRunnerResultHandler(GetCommandText(commandId)); ;

            switch (commandId)
            {
                case CheckoutCommandId:
                case CtxtCheckoutCommandId:
                    {
                        commandline = string.Format("SD {0} edit -c default \"{1}\"", globalOptions, filePath);

                        string fileName;
                        try
                        {
                            fileName = Path.GetFileName(filePath);
                        }
                        catch (Exception)
                        {
                            // the file path will do in an emergency. The point
                            // is just to create a shorter message.
                            fileName = filePath;
                        }

                        handler = (Runner.RunnerResult result) => HandleCheckOutRunnerResult(result, fileName);
                    }
                    break;
                case RevertIfUnchangedCommandId:
                case CtxtRevertIfUnchangedCommandId:
                    {
                        commandline = string.Format("SD {0} revert -a \"{1}\"", globalOptions, filePath);

                        handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                    }
                    break;
                case RevertCommandId:
                case CtxtRevertCommandId:
                    {
                        IVsUIShell uiShell = ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

                        string message = string.Format("This will discard all changes. Are you sure you wish to revert {0}?", filePath);
                        bool shouldRevert = VsShellUtilities.PromptYesNo(
                            message,
                            string.Format("{0}: {1}", ADDIN_NAME, GetCommandText(commandId)),
                            OLEMSGICON.OLEMSGICON_WARNING,
                            uiShell); ;

                        if (shouldRevert)
                        {
                            commandline = string.Format("SD {0} revert \"{1}\"", globalOptions, filePath);
                            handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                        }
                    }
                    break;
                case DiffCommandId:
                case CtxtDiffCommandId:
                    {
                        commandline = string.Format("SDvc {0} diffhave \"{1}\"", globalOptions, filePath);
                        handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                    }
                    break;
                case HistoryCommandId:
                case CtxtHistoryCommandId:
                    {
                        commandline = string.Format("SDvc {0} history \"{1}\"", globalOptions, filePath);
                        handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                    }
                    break;
                case TimelapseViewCommandId:
                case CtxtTimelapseViewCommandId:
                    {
                        commandline = string.Format("SDvc {0} timelapse \"{1}\"", globalOptions, filePath);
                        handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                    }
                    break;
                case RevisionGraphCommandId:
                case CtxtRevisionGraphCommandId:
                    {
                        commandline = string.Format("SDvc {0} revgraph \"{1}\"", globalOptions, filePath);
                        handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                    }
                    break;
                case AddCommandId:
                case CtxtAddCommandId:
                    {
                        commandline = string.Format("SD {0} add \"{1}\"", globalOptions, filePath);
                        handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                    }
                    break;
                case DeleteCommandId:
                case CtxtDeleteCommandId:
                    {
                        IVsUIShell uiShell = ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

                        string message = string.Format("This will delete the file and all changes will be lost. Are you sure you wish to delete {0}?", filePath);
                        bool shouldDelete = VsShellUtilities.PromptYesNo(
                            message,
                            string.Format("{0}: {1}", ADDIN_NAME, GetCommandText(commandId)),
                            OLEMSGICON.OLEMSGICON_WARNING,
                            uiShell);

                        if (shouldDelete)
                        {
                            commandline = string.Format("SD {0} delete \"{1}\"", globalOptions, filePath);
                            handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                        }
                    }
                    break;
                case OpenInSDVCommandId:
                case CtxtOpenInSDVCommandId:
                    {
                        commandline = string.Format("SDv {0} -s \"{1}\"", globalOptions, filePath);
                        handler = CreateCommandRunnerResultHandler(GetBriefCommandDescription(commandId, filePath));
                    }
                    break;
                default:
                    break;
            }

            if (commandline != "")
            {
                var runner = Runner.Create(commandline, fileFolder, handler, null, null);
                OutputWindow.WriteLine("{0}: started at {1}: {2}", runner.JobId, DateTime.Now, commandline);
                var runAsync = !immediate;
                Runner.Run(runner, runAsync, _package.GetCommandTimeoutSeconds());
            }
        }

        private void AutoCheckout(string filePath, bool immediate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_package.AutoCheckout)
            {
                if (_package.AutoCheckoutPrompt || (_package.IsOnBlocklist(filePath) && !_package.IsOnAllowlist(filePath)))
                {
                    IVsUIShell uiShell = ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

                    string message = string.Format("Checkout {0}?", filePath);
                    bool shouldCheckout = VsShellUtilities.PromptYesNo(
                        message,
                            string.Format("{0}: {1}", ADDIN_NAME, GetCommandText(CheckoutCommandId)),
                        OLEMSGICON.OLEMSGICON_QUERY,
                        uiShell);

                    if (!shouldCheckout)
                    {
                        return;
                    }
                }

                ExecuteCommand(filePath, CheckoutCommandId, immediate);
            }
        }

        public void EditFile(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            //OutputWindow.WriteLine("Edit File {0}", path);
            if (Misc.IsFileReadOnly(path) || !_package.GetUseReadOnlyFlag())
            {
                AutoCheckout(path, true);
            }
        }

        public void EditSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_package.AutoCheckout)
            {
                return;
            }

            //CodeTimer timer = new CodeTimer("EditSolution");

            if (_dte.Solution != null)
            {
                //OutputWindow.WriteLine("Edit Solution {0}", _dte.Solution.FullName);
                if (!_dte.Solution.Saved && (Misc.IsFileReadOnly(_dte.Solution.FullName) || !_package.GetUseReadOnlyFlag()))
                {
                    AutoCheckout(_dte.Solution.FullName, true);
                }

                foreach (Document doc in _dte.Documents)
                {
                    //OutputWindow.WriteLine("Edit Project {0}", doc.FullName);
                    if (!doc.Saved && (Misc.IsFileReadOnly(doc.FullName) || !_package.GetUseReadOnlyFlag()))
                    {
                        AutoCheckout(doc.FullName, true);
                    }
                }
            }

            //timer.Stop(OutputWindow);
        }

        private void OnBeforeKeyPress(string Keypress, EnvDTE.TextSelection Selection, bool InStatementCompletion, ref bool CancelKeypress)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_dte.ActiveDocument == null)
            {
                return;
            }
            if (_dte.ActiveDocument.ReadOnly || !_package.GetUseReadOnlyFlag())
            {
                AutoCheckout(_dte.ActiveDocument.FullName, true);
            }
        }

        private void OnLineChanged(TextPoint StartPoint, TextPoint EndPoint, int Hint)
        {
            // TODO: This gets called constantly when a text buffer has changed which makes it a performance problem
            /*
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_dte.ActiveDocument == null)
            {
                return;
            }

            if ((Hint & (int)vsTextChanged.vsTextChangedNewline) == 0 &&
                (Hint & (int)vsTextChanged.vsTextChangedMultiLine) == 0 &&
                (Hint & (int)vsTextChanged.vsTextChangedNewline) == 0 &&
                (Hint != 0))
            {
                return;
            }


            if (_dte.ActiveDocument.ReadOnly && !_dte.ActiveDocument.Saved)
            {
                AutoCheckout(_dte.ActiveDocument.FullName, true);
            }
            */
        }

        private void SetStatusBarText(string message, bool highlight)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _dte.StatusBar.Text = string.Format("{0}: {1}", ADDIN_NAME, message);
            _dte.StatusBar.Highlight(highlight);
        }

        /// <summary>
        /// Fill output window with subprocess output. Set status bar text if
        /// process timed out.
        /// </summary>
        /// <param name="result">RunnerResult for the subprocess</param>
        /// <returns>true if the process finished (status bar untouched); false if it timed out (status bar updated)</returns>
        private bool ShowRunnerResultOutput(Runner.RunnerResult result, string commandDescription)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            bool completed;

            DateTime now = DateTime.Now;
            if (result.ExitCode == null)
            {
                string message = "Timed out. Check server connection.";
                OutputWindow.WriteLine("{0}: {1}", result.JobId, message);
                SetStatusBarText(string.Format("Timed out: {0}", commandDescription), true);

                completed = false;
            }
            else
            {
                DumpRunnerResult(result.JobId, "stdout", result.Stdout);
                DumpRunnerResult(result.JobId, "stderr", result.Stderr);
                OutputWindow.WriteLine("{0}: exit code: {1} (0x{1:X})", result.JobId, (int)result.ExitCode, (int)result.ExitCode);

                completed = true;
            }

            OutputWindow.WriteLine("{0}: finished at {1}", result.JobId, now);

            return completed;
        }

        private void SetStatusBarTextForRunnerResult(Runner.RunnerResult result, string commandDescription)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (result.ExitCode == 0) SetStatusBarText(SUCCESS_PREFIX + commandDescription, false);
            else SetStatusBarText(FAILURE_PREFIX + commandDescription, true);
        }

        // TODO - presumably it's possible for this message to end up localized.
        // What's a better way of doing this?
        //
        // There's a SD -L language option, but the docs are a bit coy about
        // what exactly it's for or how you use it. ("This feature is reserved
        // for system integrators" - see
        // https://www.SD.com/manuals/cmdref/Content/CmdRef/global.options.html)
        //
        // You can query the list of other users using ``SD fstat PATH'' and
        // scanning for otherXXXN line(s). But now that's two SD invocations
        // when checking out. Maybe we should just do that.
        //
        // ``SD -z tag edit PATH'' doesn't do anything useful. -Mj and -G just
        // produce the same data, but in a structured format that's not really
        // any easier to parse from C#. (And the -Mj output isn't even valid
        // JSON! It's several mappings, back to back!)
        //
        // https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference
        private static readonly Regex AlsoOpenedByRegex = new Regex(@"^... (?:.*) - also opened by (?<user>.*)$");

        private void HandleCheckOutRunnerResult(Runner.RunnerResult result, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string commandDescription = GetBriefCommandDescription(CheckoutCommandId, filePath);

            if (ShowRunnerResultOutput(result, commandDescription))
            {
                if (result.ExitCode == 0)
                {
                    // Is there a better way of doing this? Surely there must be.
                    int numOtherUsers = 0;

                    foreach (string line in result.Stdout)
                    {
                        try
                        {
                            Match match = AlsoOpenedByRegex.Match(line);
                            if (match.Success) ++numOtherUsers;
                        }
                        catch (RegexMatchTimeoutException) { }
                    }

                    if (numOtherUsers == 0) SetStatusBarTextForRunnerResult(result, commandDescription);
                    else SetStatusBarTextForRunnerResult(result, string.Format("{0} (+{1})", commandDescription, numOtherUsers));
                }
                else SetStatusBarTextForRunnerResult(result, commandDescription);
            }
        }

        /// <summary>
        /// Wrapper for creating a RunnerResult handler that passes the
        /// appropriate command description through to
        /// ShowRunnerResultOutput/SetStatusBarTextForRunnerResult.
        /// </summary>
        /// <param name="commandDescription"></param>
        /// <returns></returns>
        private Action<Runner.RunnerResult> CreateCommandRunnerResultHandler(string commandDescription)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return (Runner.RunnerResult result) =>
            {
                if (ShowRunnerResultOutput(result, commandDescription)) SetStatusBarTextForRunnerResult(result, commandDescription);
            };
        }

        private void DumpRunnerResult(UInt64 jobId, string prefix, IEnumerable<string> lines)
        {
            foreach (string line in lines) OutputWindow.WriteLine("{0}: {1}: {2}", jobId, prefix, line);
        }

        /// <summary>
        /// Called before menu button is shown so we can update text and active state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBeforeQueryStatusWorkspace(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SDEditVS package = _package as SDEditVS;
            var myCommand = sender as OleMenuCommand;
            if (null != myCommand)
            {
                int workspaceIndex = GetWorkspaceIndexForCommandId(myCommand.CommandID.ID);
                string text = package.GetWorkspaceName(workspaceIndex);
                myCommand.Visible = (text.Length > 0);
                myCommand.Text = text;

                if (_dte.Solution != null && _dte.Solution.FileName.Length > 0)
                {
                    myCommand.Checked = (package.SelectedWorkspace == workspaceIndex);
                    myCommand.Enabled = true;
                }
                else
                {
                    myCommand.Enabled = false;
                }
            }
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void ExecuteWorkspace(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SDEditVS package = _package as SDEditVS;
            var myCommand = sender as OleMenuCommand;
            if (null != myCommand)
            {
                if (_dte.Solution != null && _dte.Solution.FileName.Length > 0)
                {
                    int workspaceId = GetWorkspaceIndexForCommandId(myCommand.CommandID.ID);
                    package.SelectedWorkspace = workspaceId;
                    myCommand.Checked = true;
                }
            }
        }

        private int GetWorkspaceIndexForCommandId(int commandId)
        {
            int index;
            switch (commandId)
            {
                case WorkspaceUseEnvironmentCommandId:
                    index = -1;
                    break;

                case Workspace1CommandId:
                    index = 0;
                    break;
                case Workspace2CommandId:
                    index = 1;
                    break;
                case Workspace3CommandId:
                    index = 2;
                    break;
                case Workspace4CommandId:
                    index = 3;
                    break;
                case Workspace5CommandId:
                    index = 4;
                    break;
                case Workspace6CommandId:
                    index = 5;
                    break;
                default:
                    throw new IndexOutOfRangeException();

            }
            return index;
        }
    }
}
