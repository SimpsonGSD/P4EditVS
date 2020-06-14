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

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("bcd15dfb-5150-4bde-a3d0-520343ba88c5");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private string activeFile;

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

            // Checkout
            {
                var menuCommandID = new CommandID(CommandSet, CheckoutCommandId);
                var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
                commandService.AddCommand(menuItem);
            }
            // Revert if unchanged
            {
                var menuCommandID = new CommandID(CommandSet, RevertIfUnchangedCommandId);
                var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
                menuItem.Text = "Revert If Unchanged";
                menuItem.Visible = true;
                commandService.AddCommand(menuItem);
            }
            // Revert
            {
                var menuCommandID = new CommandID(CommandSet, RevertCommandId);
                var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
                menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
                menuItem.Text = "Revert";
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
                    bool isProjectFile = false;

                    var selectedItems = applicationObject.SelectedItems;
                    if (selectedItems.Count > 0)
                    {
                        // Only 1 selected item supported
                        var selectedFile = selectedItems.Item(1);
                        
                        // Is selected item a project?
                        if (selectedFile.Project != null)
                        {
                            guiFileName = selectedFile.Project.Name;
                            activeFile = selectedFile.Project.FullName;
                            isProjectFile = true;
                        }
                        else if (selectedFile.ProjectItem != null)
                        {
                            guiFileName = selectedFile.ProjectItem.Name;
                            activeFile = selectedFile.ProjectItem.Document.FullName;
                        }
                        // If we still have something selected and it's not a project item or project it must be the solution (?)
                        else
                        {
                            guiFileName = applicationObject.Solution.FileName;
                            activeFile = applicationObject.Solution.FullName;
                        }
                    }
                    else
                    {
                        if (applicationObject.ActiveDocument == null)
                        {
                            myCommand.Enabled = false;
                            return;
                        }

                        // Cache the file name here so it doesn't change between now and executing the command (if that can even happen)
                        activeFile = applicationObject.ActiveDocument.FullName;
                        guiFileName = applicationObject.ActiveDocument.Name;
                    }

                    // Build menu string based on command ID and whether to enable it based on file type/state
                    switch (myCommand.CommandID.ID)
                    {
                        case CheckoutCommandId:
                            {
                                myCommand.Text = string.Format("Checkout {0}", guiFileName);
                                myCommand.Enabled = applicationObject.ActiveDocument.ReadOnly || isProjectFile;
                            }
                            break;
                        case RevertIfUnchangedCommandId:
                            {
                                myCommand.Text = string.Format("Revert If Unchanged {0}", guiFileName);
                                myCommand.Enabled = !applicationObject.ActiveDocument.ReadOnly || isProjectFile;
                            }
                            break;
                        case RevertCommandId:
                            {
                                myCommand.Text = string.Format("Revert {0}", guiFileName);
                                myCommand.Enabled = !applicationObject.ActiveDocument.ReadOnly || isProjectFile;
                            }
                            break;
                        default:
                            break;
                    }
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
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            P4EditVS package = this.package as P4EditVS;
            if(!package.ValidateUserSettings() || activeFile == "")
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
                        {
                            commandline = string.Format("p4 {0} edit -c default {1}", globalOptions, activeFile);
                        }
                        break;
                    case RevertIfUnchangedCommandId:
                        {
                            commandline = string.Format("p4 {0} revert -a {1}", globalOptions, activeFile);
                        }
                        break;
                    case RevertCommandId:
                        {
                            IVsUIShell uiShell = ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

                            bool shouldRevert = VsShellUtilities.PromptYesNo(
                                "Are you sure you wish to revert and discard changes?",
                                "P4EditVS: Revert",
                                OLEMSGICON.OLEMSGICON_WARNING,
                                uiShell);

                            if(shouldRevert)
                            {
                                commandline = string.Format("p4 {0} revert {1}", globalOptions, activeFile);
                            }
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
    }
}
