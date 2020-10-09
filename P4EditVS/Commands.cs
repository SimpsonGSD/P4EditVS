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
using Microsoft.VisualStudio;

using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace P4EditVS
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

        public readonly int[] CommandIds = { CheckoutCommandId, RevertIfUnchangedCommandId, RevertCommandId, DiffCommandId, HistoryCommandId, RevisionGraphCommandId, TimelapseViewCommandId, AddCommandId, DeleteCommandId };
        public readonly int[] CtxtCommandIds = { CtxtCheckoutCommandId, CtxtRevertIfUnchangedCommandId, CtxtRevertCommandId, CtxtDiffCommandId, CtxtHistoryCommandId, CtxtRevisionGraphCommandId, CtxtTimelapseViewCommandId, CtxtAddCommandId, CtxtDeleteCommandId };
        public readonly int[] WorkspaceCommandIds = { WorkspaceUseEnvironmentCommandId, Workspace1CommandId, Workspace2CommandId, Workspace3CommandId, Workspace4CommandId, Workspace5CommandId, Workspace6CommandId };

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("bcd15dfb-5150-4bde-a3d0-520343ba88c5");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly P4EditVS _package;

		/// <summary>
		/// Files selected to apply source control commands to.
		/// </summary>
        private List<string> _selectedFiles = new List<string>();

        private StreamWriter _outputWindow;
		private EnvDTE80.TextDocumentKeyPressEvents _textDocEvents;
		//private EnvDTE.TextEditorEvents _textEditorEvents;
		private EnvDTE80.DTE2 _application;
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
			_package = package as P4EditVS ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
			_application = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2 ?? throw new ArgumentNullException(nameof(_application));

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
                _textDocEvents = ((EnvDTE80.Events2)_application.Events).get_TextDocumentKeyPressEvents(null);
                _textDocEvents.BeforeKeyPress += new _dispTextDocumentKeyPressEvents_BeforeKeyPressEventHandler(OnBeforeKeyPress);
            }
			//_textEditorEvents = ((EnvDTE80.Events2)_application.Events).get_TextEditorEvents(null);
			//_textEditorEvents.LineChanged += new _dispTextEditorEvents_LineChangedEventHandler(OnLineChanged);

            // Subscribe to events so we can do auto-checkout
            //_updateSolutionEvents = new UpdateSolutionEvents(this);
            _runningDocTableEvents = new RunningDocTableEvents(this, _application as DTE);
            
            // Setup output log
            var outputWindowPaneStream = new OutputWindowStream(_application, "P4EditVS");
            _outputWindow = new StreamWriter(outputWindowPaneStream);
            _outputWindow.AutoFlush = true;
            //_outputWindow.WriteLine("hello from P4EditVS\n");
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
                if (_application != null)
                {
					// reset selection
					_selectedFiles.Clear();
					
                    if (_application.ActiveDocument != null)
                    {
						_selectedFiles.Add(_application.ActiveDocument.FullName);

                        string guiFileName = _application.ActiveDocument.Name;
                        bool isReadOnly = _application.ActiveDocument.ReadOnly;
						// Build menu string based on command ID and whether to enable it based on file type/state
						ConfigureCmdButton(myCommand, guiFileName, isReadOnly);
					}
                    else
                    {
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

                    foreach(SelectedItem selectedFile in selectedItems)
                    {
                        // Is selected item a project?
                        if (selectedFile.Project != null)
                        {
							AddSelectedFile(_selectedFiles, selectedFile.Project.FullName);

							// Get .filter file, usually the first project item
                            if(selectedFile.Project.ProjectItems.Count > 0)
                            {
                                string projectItemFileName = selectedFile.Project.ProjectItems.Item(1).FileNames[0];
                                if (projectItemFileName.Contains(".filters"))
                                {
                                    AddSelectedFile(_selectedFiles, projectItemFileName);
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
                case AddCommandId:
                case CtxtAddCommandId:
                    {
                        command.Text = "Mark for Add";
                        command.Enabled = !isReadOnly;
                    }
                    break;
                case DeleteCommandId:
                case CtxtDeleteCommandId:
                    {
                        command.Text = "Mark for Delete";
                        command.Enabled = isReadOnly;
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
		/// Executes P4 command for supplied command ID
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="commandId"></param>
		private void ExecuteCommand(string filePath, int commandId, bool immediate)
		{
            ThreadHelper.ThrowIfNotOnUIThread();
			if(!_package.ValidateUserSettings())
			{
				return;
			}

			string globalOptions = _package.GetGlobalP4CmdLineOptions();
			string commandline = "";

			switch (commandId)
			{
				case CheckoutCommandId:
				case CtxtCheckoutCommandId:
					{
						commandline = string.Format("p4 {0} edit -c default \"{1}\"", globalOptions, filePath);
					}
					break;
				case RevertIfUnchangedCommandId:
				case CtxtRevertIfUnchangedCommandId:
					{
						commandline = string.Format("p4 {0} revert -a \"{1}\"", globalOptions, filePath);
					}
					break;
				case RevertCommandId:
				case CtxtRevertCommandId:
					{
						IVsUIShell uiShell = ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

						string message = string.Format("This will discard all changes. Are you sure you wish to revert {0}?", filePath);
						bool shouldRevert = VsShellUtilities.PromptYesNo(
							message,
							"P4EditVS: Revert",
							OLEMSGICON.OLEMSGICON_WARNING,
							uiShell);

						if (shouldRevert)
						{
							commandline = string.Format("p4 {0} revert \"{1}\"", globalOptions, filePath);
						}
					}
					break;
				case DiffCommandId:
				case CtxtDiffCommandId:
					{
						commandline = string.Format("p4vc {0} diffhave \"{1}\"", globalOptions, filePath);
					}
					break;
				case HistoryCommandId:
				case CtxtHistoryCommandId:
					{
						commandline = string.Format("p4vc {0} history \"{1}\"", globalOptions, filePath);
					}
					break;
				case TimelapseViewCommandId:
				case CtxtTimelapseViewCommandId:
					{
						commandline = string.Format("p4vc {0} timelapse \"{1}\"", globalOptions, filePath);
					}
					break;
				case RevisionGraphCommandId:
				case CtxtRevisionGraphCommandId:
					{
						commandline = string.Format("p4vc {0} revgraph \"{1}\"", globalOptions, filePath);
					}
					break;
                case AddCommandId:
                case CtxtAddCommandId:
                    {
						commandline = string.Format("p4 {0} add \"{1}\"", globalOptions, filePath);
                    }
                    break;
                case DeleteCommandId:
                case CtxtDeleteCommandId:
                    {
                        IVsUIShell uiShell = ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

                        string message = string.Format("This will delete the file and all changes will be lost. Are you sure you wish to delete {0}?", filePath);
                        bool shouldDelete = VsShellUtilities.PromptYesNo(
                            message,
                            "P4EditVS: Delete",
                            OLEMSGICON.OLEMSGICON_WARNING,
                            uiShell);

                        if (shouldDelete)
                        {
                            commandline = string.Format("p4 {0} delete \"{1}\"", globalOptions, filePath);
                        }
                    }
                    break;
                default:
					break;
			}

			if (commandline != "")
			{
				UInt64 jobId = Runner.Run("cmd.exe", "/C " + commandline, HandleRunnerResult, null, null, immediate);
                if(!immediate)
				    _outputWindow.WriteLine("{0}: started at {1}: {2}", jobId, DateTime.Now, commandline);

				//System.Diagnostics.Process process = new System.Diagnostics.Process();
				//System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
				//startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
				//startInfo.FileName = "cmd.exe";
				//startInfo.Arguments = "/C " + commandline;
				//process.StartInfo = startInfo;
				//process.Start();
			}
		}

		private void AutoCheckout(string filePath, bool immediate)
		{
            ThreadHelper.ThrowIfNotOnUIThread();
			if (_package.AutoCheckout)
			{
				if(_package.AutoCheckoutPrompt)
				{
					IVsUIShell uiShell = ServiceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

					string message = string.Format("Checkout {0}?", filePath);
					bool shouldCheckout = VsShellUtilities.PromptYesNo(
						message,
						"P4EditVS: Checkout",
						OLEMSGICON.OLEMSGICON_QUERY,
						uiShell);

					if(!shouldCheckout)
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
            //_outputWindow.WriteLine("Edit File {0}", path);
            if(Misc.IsFileReadOnly(path))
            {
                AutoCheckout(path, true);
            }
        }

        public void EditSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if(!_package.AutoCheckout)
            {
                return;
            }

            //CodeTimer timer = new CodeTimer("EditSolution");

            if (_application.Solution != null)
            {
                //_outputWindow.WriteLine("Edit Solution {0}", _application.Solution.FullName);
                if (!_application.Solution.Saved && Misc.IsFileReadOnly(_application.Solution.FullName))
                {
                    AutoCheckout(_application.Solution.FullName, true);
                }

                foreach(Document doc in _application.Documents)
                {
                    //_outputWindow.WriteLine("Edit Project {0}", doc.FullName);
                    if(!doc.Saved && Misc.IsFileReadOnly(doc.FullName))
                    {
                        AutoCheckout(doc.FullName, true);
                    }
                }
            }

            //timer.Stop(_outputWindow);
        }

        private void OnBeforeKeyPress(string Keypress, EnvDTE.TextSelection Selection, bool InStatementCompletion, ref bool CancelKeypress)
		{
            ThreadHelper.ThrowIfNotOnUIThread();
			if(_application.ActiveDocument == null)
			{
				return;
			}
			if (_application.ActiveDocument.ReadOnly)
			{
				AutoCheckout(_application.ActiveDocument.FullName, true);
			}
		}

        // This gets called constantly when a text buffer has changed.. 
		private void OnLineChanged(TextPoint StartPoint, TextPoint EndPoint, int Hint)
		{
            ThreadHelper.ThrowIfNotOnUIThread();
			if (_application.ActiveDocument == null)
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


			if (_application.ActiveDocument.ReadOnly && !_application.ActiveDocument.Saved)
			{
				//AutoCheckout(_application.ActiveDocument.FullName, true);
			}
		}

		private void HandleRunnerResult(Runner.RunnerResult result)
        {
            DateTime now = DateTime.Now;
            if (result.ExitCode == null)
            {
                _outputWindow.WriteLine("{0}: Timed out.", result.JobId);
            }
            else
            {
                DumpRunnerResult(result.JobId, "stdout",result.Stdout);
                DumpRunnerResult(result.JobId, "stderr",result.Stderr);
            }

            _outputWindow.WriteLine("{0}: finished at {1}", result.JobId, now);
        }

        private void DumpRunnerResult(UInt64 jobId, string prefix, string data)
        {
            if (data.Length > 0)
            {
                using (var reader = new StringReader(data))
                {
                    string line = reader.ReadLine();
                    _outputWindow.WriteLine("{0}: {1}: {2}", jobId, prefix, line);
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
            P4EditVS package = _package as P4EditVS;
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
            P4EditVS package = _package as P4EditVS;
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
