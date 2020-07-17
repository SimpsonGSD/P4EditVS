using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using EnvDTE;

namespace P4EditVS
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class Commands
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

        public readonly int[] CommandIds = { CheckoutCommandId, RevertIfUnchangedCommandId, RevertCommandId, DiffCommandId, HistoryCommandId, RevisionGraphCommandId, TimelapseViewCommandId };
        public readonly int[] CtxtCommandIds = { CtxtCheckoutCommandId, CtxtRevertIfUnchangedCommandId, CtxtRevertCommandId, CtxtDiffCommandId, CtxtHistoryCommandId, CtxtRevisionGraphCommandId, CtxtTimelapseViewCommandId };
        public readonly int[] WorkspaceCommandIds = { WorkspaceUseEnvironmentCommandId, Workspace1CommandId, Workspace2CommandId, Workspace3CommandId, Workspace4CommandId, Workspace5CommandId, Workspace6CommandId };

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("bcd15dfb-5150-4bde-a3d0-520343ba88c5");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private string mCachedFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="Commands"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private Commands(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

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
                return this.package;
            }
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
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
                EnvDTE80.DTE2 applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
                if (applicationObject != null)
                {
                    string guiFileName = "";
                    bool isReadOnly = false;
                    bool validSelection = true;

                    if (applicationObject.ActiveDocument != null)
                    {
                        mCachedFilePath = applicationObject.ActiveDocument.FullName;
                        guiFileName = applicationObject.ActiveDocument.Name;
                        isReadOnly = applicationObject.ActiveDocument.ReadOnly;
                    }
                    else
                    {
                        validSelection = false;
                    }

                    if (validSelection)
                    {
                        // Build menu string based on command ID and whether to enable it based on file type/state
                        ConfigureCmdButton(myCommand, guiFileName, isReadOnly);
                    }
                    else
                    {
                        // Invalid selection clear cached path and disable buttons
                        mCachedFilePath = "";
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
            ThreadHelper.ThrowIfNotOnUIThread();
            var myCommand = sender as OleMenuCommand;
            if (null != myCommand)
            {
                EnvDTE80.DTE2 applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
                if (applicationObject != null)
                {
                    string guiFileName = "";
                    bool isReadOnly = false;
                    bool validSelection = true;

                    var selectedItems = applicationObject.SelectedItems;
                    if (selectedItems.Count > 0)
                    {
                        // Only 1 selected item supported
                        var selectedFile = selectedItems.Item(1);

                        // Is selected item a project?
                        if (selectedFile.Project != null)
                        {
                            mCachedFilePath = selectedFile.Project.FullName;
                        }
                        else if (selectedFile.ProjectItem != null)
                        {
                            if (selectedFile.ProjectItem.FileCount == 1)
                            {
                                mCachedFilePath = selectedFile.ProjectItem.FileNames[0];
                            }
                            else
                            {
                                validSelection = false;
                            }
                        }
                        // If we still have something selected and it's not a project item or project it must be the solution (?)
                        else
                        {
                            mCachedFilePath = applicationObject.Solution.FullName;
                        }
                    }

                    if (validSelection)
                    {
                        System.IO.FileInfo info = new System.IO.FileInfo(mCachedFilePath);
                        isReadOnly = info.IsReadOnly;
                        guiFileName = info.Name;
                        // Build menu string based on command ID and whether to enable it based on file type/state
                        ConfigureCmdButton(myCommand, guiFileName, isReadOnly);
                    }
                    else
                    {
                        // Invalid selection clear cached path and disable buttons
                        mCachedFilePath = "";
                        myCommand.Enabled = false;
                    }
                }
            }
        }

        void ConfigureCmdButton(OleMenuCommand command, string name, bool isReadOnly)
        {
            switch (command.CommandID.ID)
            {
                case CheckoutCommandId:
                case CtxtCheckoutCommandId:
                    {
                        // This is first command in the menu so rather than bloating the whole thing just display the filename at the top to the right
                        command.Enabled = isReadOnly;
                        command.Text = command.Enabled ? string.Format("Checkout\t\t\t{0}", name) : "Checkout";
                    }
                    break;
                case RevertIfUnchangedCommandId:
                case CtxtRevertIfUnchangedCommandId:
                    {
                        command.Enabled = !isReadOnly;
                        command.Text = command.Enabled ? string.Format("Revert If Unchanged\t\t{0}", name) : "Revert If Unchanged";
                    }
                    break;
                case RevertCommandId:
                case CtxtRevertCommandId:
                    {
                        command.Text = "Revert";
                        command.Enabled = !isReadOnly;
                    }
                    break;
                case DiffCommandId:
                case CtxtDiffCommandId:
                    {
                        command.Text = "Diff Against Have Revision";
                        command.Enabled = !isReadOnly;
                    }
                    break;
                case HistoryCommandId:
                case CtxtHistoryCommandId:
                    {
                        command.Text = "History";
                        command.Enabled = true;
                    }
                    break;
                case TimelapseViewCommandId:
                case CtxtTimelapseViewCommandId:
                    {
                        command.Text = "Time-lapse View";
                        command.Enabled = true;
                    }
                    break;
                case RevisionGraphCommandId:
                case CtxtRevisionGraphCommandId:
                    {
                        command.Text = "Revision Graph";
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
            P4EditVS package = this.package as P4EditVS;
            if (!package.ValidateUserSettings() || mCachedFilePath == "")
            {
                return;
            }

            var myCommand = sender as OleMenuCommand;

            string globalOptions = package.GetGlobalP4CmdLineOptions();

            EnvDTE80.DTE2 applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            if (applicationObject != null)
            {
                string commandline = "";

                switch (myCommand.CommandID.ID)
                {
                    case CheckoutCommandId:
                    case CtxtCheckoutCommandId:
                        {
                            commandline = string.Format("p4 {0} edit -c default {1}", globalOptions, mCachedFilePath);
                        }
                        break;
                    case RevertIfUnchangedCommandId:
                    case CtxtRevertIfUnchangedCommandId:
                        {
                            commandline = string.Format("p4 {0} revert -a {1}", globalOptions, mCachedFilePath);
                        }
                        break;
                    case RevertCommandId:
                    case CtxtRevertCommandId:
                        {
                            IVsUIShell uiShell = ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

                            string message = string.Format("This will discard all changes. Are you sure you wish to revert {0}?", mCachedFilePath);
                            bool shouldRevert = VsShellUtilities.PromptYesNo(
                                message,
                                "P4EditVS: Revert",
                                OLEMSGICON.OLEMSGICON_WARNING,
                                uiShell);

                            if (shouldRevert)
                            {
                                commandline = string.Format("p4 {0} revert {1}", globalOptions, mCachedFilePath);
                            }
                        }
                        break;
                    case DiffCommandId:
                    case CtxtDiffCommandId:
                        {
                            commandline = string.Format("p4vc {0} diffhave {1}", globalOptions, mCachedFilePath);
                        }
                        break;
                    case HistoryCommandId:
                    case CtxtHistoryCommandId:
                        {
                            commandline = string.Format("p4vc {0} history {1}", globalOptions, mCachedFilePath);
                        }
                        break;
                    case TimelapseViewCommandId:
                    case CtxtTimelapseViewCommandId:
                        {
                            commandline = string.Format("p4vc {0} timelapse {1}", globalOptions, mCachedFilePath);
                        }
                        break;
                    case RevisionGraphCommandId:
                    case CtxtRevisionGraphCommandId:
                        {
                            commandline = string.Format("p4vc {0} revgraph {1}", globalOptions, mCachedFilePath);
                        }
                        break;
                    default:
                        break;
                }

                if (commandline != "")
                {
                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                    startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/C " + commandline;
                    process.StartInfo = startInfo;
                    process.Start();
                }
            }
        }

        /// <summary>
        /// Called before menu button is shown so we can update text and active state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBeforeQueryStatusWorkspace(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            P4EditVS package = this.package as P4EditVS;
            var myCommand = sender as OleMenuCommand;
            if (null != myCommand)
            {
                int workspaceIndex = GetWorkspaceIndexForCommandId(myCommand.CommandID.ID);
                myCommand.Checked = (package.SelectedWorkspace == workspaceIndex);
                string text = package.GetWorkspaceName(workspaceIndex);
                myCommand.Enabled = (text != "");
                myCommand.Visible = myCommand.Enabled;
                myCommand.Text = text;
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
            P4EditVS package = this.package as P4EditVS;
            var myCommand = sender as OleMenuCommand;
            if (null != myCommand)
            {
                int workspaceId = GetWorkspaceIndexForCommandId(myCommand.CommandID.ID);
                package.SelectedWorkspace = workspaceId;
                myCommand.Checked = true;
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
