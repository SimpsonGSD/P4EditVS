using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;
using System.ComponentModel;
using EnvDTE;
using EnvDTE80;
using System.IO;
using System.Collections.Generic;

// The documentation for IVsPersistSolutionOpts is... not good. There's some use
// here:
// https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs

namespace SDEditVS
{
	/// <summary>
	/// Values saved to the .suo file.
	/// </summary>
	/// <remarks>
	/// Since it's convenient, this stuff is serialized using the XmlSerializer.
	/// So anything in here needs to be compatible with that.
	///
	/// If the suo doesn't contain a SolutionOptions, the default values here
	/// will be used. 
	/// </remarks>
	[System.Obsolete("Use SolutionSettings instead")]
	public class SolutionOptions
    {
        // Index of workspace to use.
        public int WorkspaceIndex = -1;

        // For debugging purposes, when you're staring at a mostly
        // incomprehensible hex dump of the .suo file.
        public string Now = DateTime.Now.ToString();
    }

    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    //[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.None)]
    [Guid(SDEditVS.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionPageGrid), "SDEditVS", "Settings", 0, 0, true)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class SDEditVS : AsyncPackage, IVsPersistSolutionOpts
    {
        private const string SolutionUserOptionsKey = "SDEditVS";

        /// <summary>
        /// SDEditVS GUID string.
        /// </summary>
        public const string PackageGuidString = "d6a4db63-698d-4d16-bbc0-944fe52f83db";

        [System.Obsolete("Use SelectedWorkspace instead")]
        private int _selectedWorkspace = -1;

        public SolutionSettings SolutionSettings;
        private EnvDTE80.DTE2 _dte;
        private EnvDTE.SolutionEvents _solutionEvents;
        public StreamWriter OutputWindow;
        private string _saveDataDirectory;

        public int SelectedWorkspace
        {
            get => SolutionSettings.SelectedWorkspace;
            set
            {
                if (SolutionSettings.SelectedWorkspace != value)
                {
                    SolutionSettings.SelectedWorkspace = value;
                    SolutionSettings.SaveAsync();
                }
            }
        }

        public string ClientName
        {
            get
            {
                return GetWorkspaceName(SelectedWorkspace);
            }
        }

        public string UserName
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                switch (SelectedWorkspace)
                {
                    case 0:
                        return page.UserName;
                    case 1:
                        return page.UserName2;
                    case 2:
                        return page.UserName3;
                    case 3:
                        return page.UserName4;
                    case 4:
                        return page.UserName5;
                    case 5:
                        return page.UserName6;
                }
                throw new IndexOutOfRangeException();
            }
        }

        public string Server
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                switch (SelectedWorkspace)
                {
                    case 0:
                        return page.Server;
                    case 1:
                        return page.Server2;
                    case 2:
                        return page.Server3;
                    case 3:
                        return page.Server4;
                    case 4:
                        return page.Server5;
                    case 5:
                        return page.Server6;
                }
                throw new IndexOutOfRangeException();
            }
        }

        public bool AutoCheckout
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.AutoCheckout || page.AutoCheckoutOnEdit;
            }
        }

        public bool AutoCheckoutPrompt
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.AutoCheckoutPrompt;
            }
        }

        public bool AutoCheckoutOnEdit
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.AutoCheckoutOnEdit;
            }
        }

        private bool _hasAnyAllowlists = false;
        private HashSet<string> _checkoutPromptAllowlistDirectories;
        private HashSet<string> _checkoutPromptAllowlistFilePaths;
        private void EnsureAllowlistsArePopulated()
        {
            if(_checkoutPromptAllowlistDirectories == null)
            {
                _checkoutPromptAllowlistDirectories = new HashSet<string>();
                _checkoutPromptAllowlistFilePaths = new HashSet<string>();
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                if(string.IsNullOrEmpty(page.CheckoutPromptAllowlist))
                {
                    return;
                }
                _hasAnyAllowlists = true;
                foreach (string path in page.CheckoutPromptAllowlist.Split(','))
                {
                    var attr = File.GetAttributes(path);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        _checkoutPromptAllowlistDirectories.Add(path.NormalizeDirectoryPath());
                    }
                    else
                    {
                        _checkoutPromptAllowlistFilePaths.Add(path.NormalizeFilePath());
                    }
                }
            }
        }

        public bool IsOnAllowlist(string filePath)
        {
            EnsureAllowlistsArePopulated();
            if (!_hasAnyAllowlists)
            {
                return false;
            }
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            filePath = filePath.NormalizeFilePath();
            if (_checkoutPromptAllowlistFilePaths.Contains(filePath))
            {
                return true;
            }
            if(_checkoutPromptAllowlistDirectories.Count < 1)
            {
                return false;
            }
            string filePathDirectory = filePath.Substring(0, filePath.LastIndexOf('\\') + 1);
            // Maybe we'll get lucky and it's directly in the directory we care about
            if (_checkoutPromptAllowlistDirectories.Contains(filePathDirectory))
            {
                return true;
            }
            return IsInPathList(_checkoutPromptAllowlistDirectories, filePathDirectory);
        }

        private bool _hasAnyBlocklists = false;
        private HashSet<string> _checkoutPromptBlocklistDirectories;
        private HashSet<string> _checkoutPromptBlocklistFilePaths;
        private void EnsureBlocklistsArePopulated()
        {
            if(_checkoutPromptBlocklistDirectories == null)
            {
                _checkoutPromptBlocklistDirectories = new HashSet<string>();
                _checkoutPromptBlocklistFilePaths = new HashSet<string>();
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                if (string.IsNullOrEmpty(page.CheckoutPromptAllowlist))
                {
                    return;
                }
                _hasAnyBlocklists = true;
                foreach (string path in page.CheckoutPromptBlocklist.Split(','))
                {
                    var attr = File.GetAttributes(path);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        _checkoutPromptBlocklistDirectories.Add(path.NormalizeDirectoryPath());
                    }
                    else
                    {
                        _checkoutPromptBlocklistFilePaths.Add(path.NormalizeFilePath());
                    }
                }
            }
        }

        public bool IsOnBlocklist(string filePath)
        {
            EnsureBlocklistsArePopulated();
            if (!_hasAnyBlocklists)
            {
                return false;
            }
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            filePath = filePath.NormalizeFilePath();
            if(_checkoutPromptBlocklistFilePaths.Contains(filePath))
            {
                return true;
            }
            if (_checkoutPromptBlocklistDirectories.Count < 1)
            {
                return false;
            }
            string filePathDirectory = filePath.Substring(0, filePath.LastIndexOf('\\') + 1);
            // Maybe we'll get lucky and it's directly in the directory we care about
            if (_checkoutPromptBlocklistDirectories.Contains(filePathDirectory))
            {
                return true;
            }
            return IsInPathList(_checkoutPromptBlocklistDirectories, filePathDirectory);
        }

        private bool IsInPathList(HashSet<string> paths, string filePathDirectory)
        {
            foreach (string path in paths)
            {
                if(filePathDirectory.IsSubPathOf(path))
                {
                    return true;
                }
            }
            return false;
        }

        public string GetWorkspaceName(int index)
        {
            OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
            switch (index)
            {
                case -1:
                    if (page.AllowEnvironment) return "(Use Environment)";
                    else return "";

                case 0:
                    return page.ClientName;
                case 1:
                    return page.ClientName2;
                case 2:
                    return page.ClientName3;
                case 3:
                    return page.ClientName4;
                case 4:
                    return page.ClientName5;
                case 5:
                    return page.ClientName6;
            }
            throw new IndexOutOfRangeException();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SDEditVS"/> class.
        /// </summary>
        public SDEditVS()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
            Trace.WriteLine(string.Format("Hello from SDEditVS"));

            // %APPDATA%/Roaming/SDEditVS
            _saveDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\SDEditVS\\";
            // Ensure save data directory exists
            if (!Directory.Exists(_saveDataDirectory))
            {
                Directory.CreateDirectory(_saveDataDirectory);
            }
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _dte = await GetServiceAsync(typeof(DTE)) as EnvDTE80.DTE2 ?? throw new ArgumentNullException(nameof(EnvDTE80.DTE2));

            // Legacy 
#pragma warning disable 618
            _selectedWorkspace = GetOptionsPage().AllowEnvironment ? -1 : 0; // Pick correct default workspace
#pragma warning restore 618

            // Setup output log
            var outputWindowPaneStream = new OutputWindowStream(_dte, "SDEditVS");
            OutputWindow = new StreamWriter(outputWindowPaneStream);
            OutputWindow.AutoFlush = true;
            //OutputWindow.WriteLine("hello from SDEditVS\n");

            // A solution may have been opened before we are initialised
            if (_dte.Solution != null && _dte.Solution.FullName.Length > 0)
            {
                OnSolutionOpened();
            }

            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.Opened += new _dispSolutionEvents_OpenedEventHandler(OnSolutionOpened);
            _solutionEvents.BeforeClosing += new _dispSolutionEvents_BeforeClosingEventHandler(OnSolutionClosing);
            _solutionEvents.AfterClosing += new _dispSolutionEvents_AfterClosingEventHandler(OnSolutionClosed);


            await Commands.InitializeAsync(this);
        }

        private OptionPageGrid GetOptionsPage()
        {
            return (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
        }

        private void OnSolutionOpened()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (SolutionSettings != null)
            {
                // Save settings to prevent losing data
                SolutionSettings.SaveAsync();
            }

            SolutionSettings = new SolutionSettings(_saveDataDirectory, _dte.Solution.FileName);
            if (!SolutionSettings.DoesExist())
            {
                OutputWindow.WriteLine("Solution settings {0} not found", SolutionSettings.PathAndFileName);

                // If solution settings don't exist then create, set default values and save it.
                SolutionSettings.Create();

                // Initialise default values here
                // This is initialised using legacy variable to ensure previous value from .suo file is read
#pragma warning disable 618
                SolutionSettings.SelectedWorkspace = _selectedWorkspace;
#pragma warning restore 618

                SolutionSettings.SaveAsync();
            }
            else
            {
                OutputWindow.WriteLine("Solution settings {0} loaded", SolutionSettings.PathAndFileName);
                SolutionSettings.Load();
            }
        }

        private void OnSolutionClosing()
        {
            if (SolutionSettings != null)
            {
                // Save settings to prevent losing data
                SolutionSettings.SaveAsync();
            }
        }

        private void OnSolutionClosed()
        {
            SolutionSettings = null;
        }

        public string GetGlobalSDCmdLineOptions()
        {
            if (SelectedWorkspace == -1)
            {
                return "";
            }
            else
            {
                // 
                return string.Format("-c {0} -u {1} -p {2}", ClientName, UserName, Server);
            }
        }

        public bool ValidateUserSettings()
        {
            if (ClientName == "")
            {
                VsShellUtilities.ShowMessageBox(
                this,
                "Client name is empty. This must be set under Tools->Options->SDEditVS->Settings",
                "Invalid Settings",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                return false;
            }

            // If Allow Use Environment is disabled, and the environment is the
            // selected workspace, the previous check will fail.
            //
            // So if things got this far, and the environment is the selected
            // workspace, it's all good. User name and server quite unnecessary.
            if (SelectedWorkspace == -1)
            {
                return true;
            }

            if (UserName == "")
            {
                VsShellUtilities.ShowMessageBox(
                this,
                "User name is empty. This must be set under Tools->Options->SDEditVS->Settings",
                "Invalid Settings",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                return false;
            }

            if (Server == "")
            {
                VsShellUtilities.ShowMessageBox(
                this,
                "Server is empty. This must be set under Tools->Options->SDEditVS->Settings",
                "Invalid Settings",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                return false;
            }

            return true;
        }

        public float GetCommandTimeoutSeconds()
        {
            return GetOptionsPage().CommandTimeoutSeconds;
        }

        public bool GetUseReadOnlyFlag()
        {
            return GetOptionsPage().UseReadOnlyFlag;
        }


        #region Visual Studio suo interface

        /// <summary>
        /// Set the current settings from a SolutionOptions object.
        /// </summary>
        /// <remarks>
        /// The SolutionOptions is just whatever was in the .suo file.
        /// </remarks>
        /// <param name="options"></param>
        [System.Obsolete("Use SolutionSettings instead")]
		private void SetSolutionOptions(SolutionOptions options)
		{
			if (options.WorkspaceIndex >= -1 && options.WorkspaceIndex <= 6) _selectedWorkspace = options.WorkspaceIndex;
		}

        //
        // Summary:
        //     Saves user options for a given solution.
        //
        // Parameters:
        //   pPersistence:
        //     [in] Pointer to the Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence
        //     interface on which the VSPackage should call its Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence.SavePackageUserOpts(Microsoft.VisualStudio.Shell.Interop.IVsPersistSolutionOpts,System.String)
        //     method for each stream name it wants to write to the user options file.
        //
        // Returns:
        //     If the method succeeds, it returns Microsoft.VisualStudio.VSConstants.S_OK. If
        //     it fails, it returns an error code.
        /* NO LONGER USED, LEFT IN FOR REFERENCE
		public int SaveUserOptions(IVsSolutionPersistence pPersistence)
        {
            Trace.WriteLine(String.Format("SDEditVS SaveUserOptions"));

            ThreadHelper.ThrowIfNotOnUIThread();

            // https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs#L300
            //
            // This function gets called by the shell when the SUO file is
            // saved. The provider calls the shell back to let it know which
            // options keys it will use in the suo file. The shell will create a
            // stream for the section of interest, and will call back the
            // provider on IVsPersistSolutionProps.WriteUserOptions() to save
            // specific options under the specified key.

            pPersistence.SavePackageUserOpts(this, SolutionUserOptionsKey);
            return VSConstants.S_OK;
        }
        */

		//
		// Summary:
		//     Loads user options for a given solution.
		//
		// Parameters:
		//   pPersistence:
		//     [in] Pointer to the Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence
		//     interface on which the VSPackage should call its Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence.LoadPackageUserOpts(Microsoft.VisualStudio.Shell.Interop.IVsPersistSolutionOpts,System.String)
		//     method for each stream name it wants to read from the user options (.opt) file.
		//
		//   grfLoadOpts:
		//     [in] User options whose value is taken from the Microsoft.VisualStudio.Shell.Interop.__VSLOADUSEROPTS
		//     DWORD.
		//
		// Returns:
		//     If the method succeeds, it returns Microsoft.VisualStudio.VSConstants.S_OK. If
		//     it fails, it returns an error code.
		[System.Obsolete(".suo has been removed, use SolutionSettings instead. This is only used to carry existing settings over to new SolutioSettings.")]
		public int LoadUserOptions(IVsSolutionPersistence pPersistence, [ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSLOADUSEROPTS")] uint grfLoadOpts)
        {
            Trace.WriteLine(String.Format("SDEditVS LoadUserOptions (grfLoadOpts={0})", grfLoadOpts));

            ThreadHelper.ThrowIfNotOnUIThread();

            // https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs#L359
            //
            // Note this can be during opening a new solution, or may be during
            // merging of 2 solutions. The provider calls the shell back to let
            // it know which options keys from the suo file were written by this
            // provider. If the shell will find in the suo file a section that
            // belong to this package, it will create a stream, and will call
            // back the provider on IVsPersistSolutionProps.ReadUserOptions() to
            // read specific options under that option key.

            pPersistence.LoadPackageUserOpts(this, SolutionUserOptionsKey);
            return VSConstants.S_OK;
        }

		//
		// Summary:
		//     Writes user options for a given solution.
		//
		// Parameters:
		//   pOptionsStream:
		//     [in] Pointer to the IStream interface to which the VSPackage should write the
		//     user-specific options.
		//
		//   pszKey:
		//     [in] Name of the stream, as provided by the VSPackage by means of the method
		//     Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence.SavePackageUserOpts(Microsoft.VisualStudio.Shell.Interop.IVsPersistSolutionOpts,System.String).
		//
		// Returns:
		//     If the method succeeds, it returns Microsoft.VisualStudio.VSConstants.S_OK. If
		//     it fails, it returns an error code.
        /* NO LONGER USED, LEFT IN FOR REFERENCE
		public int WriteUserOptions(IStream pOptionsStream, [ComAliasName("Microsoft.VisualStudio.OLE.Interop.LPCOLESTR")] string pszKey)
        {
            Trace.WriteLine(String.Format("SDEditVS WriteUserOptions (key=\"{0}\")", pszKey));

            // https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs#L318
            //
            // This function gets called by the shell to let the package write
            // user options under the specified key. The key was declared in
            // SaveUserOptions(), when the shell started saving the suo file.
            var stream = new DataStreamFromComStream(pOptionsStream);

			var options = new SolutionOptions();
			options.WorkspaceIndex = SelectedWorkspace;

			Misc.WriteXml(stream, options);

            return VSConstants.S_OK;
        }
        */

        //
        // Summary:
        //     Reads user options for a given solution.
        //
        // Parameters:
        //   pOptionsStream:
        //     [in] Pointer to the IStream interface from which the VSPackage should read the
        //     user-specific options.
        //
        //   pszKey:
        //     [in] Name of the stream, as provided by the VSPackage by means of the method
        //     Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence.LoadPackageUserOpts(Microsoft.VisualStudio.Shell.Interop.IVsPersistSolutionOpts,System.String).
        //
        // Returns:
        //     If the method succeeds, it returns Microsoft.VisualStudio.VSConstants.S_OK. If
        //     it fails, it returns an error code.
        [System.Obsolete(".suo has been removed, use SolutionSettings instead. This is only used to carry existing settings over to new SolutioSettings.")]
		public int ReadUserOptions(IStream pOptionsStream, [ComAliasName("Microsoft.VisualStudio.OLE.Interop.LPCOLESTR")] string pszKey)
        {
            Trace.WriteLine(String.Format("SDEditVS ReadUserOptions (key=\"{0}\")", pszKey));

            ThreadHelper.ThrowIfNotOnUIThread();

            // https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs#L376
            //
            // This function is called by the shell if the
            // _strSolutionUserOptionsKey section declared in LoadUserOptions()
            // as being written by this package has been found in the suo file. 
            // Note this can be during opening a new solution, or may be during
            // merging of 2 solutions. A good source control provider may need
            // to persist this data until OnAfterOpenSolution or
            // OnAfterMergeSolution is called

            var stream = new DataStreamFromComStream(pOptionsStream);

            var options = new SolutionOptions();

            bool createdDefault = false;
            if (stream.Length > 0) options = Misc.ReadXmlOrCreateDefault<SolutionOptions>(stream, out createdDefault);

            // If allow environment is not in use set it to the first workspace so some workspace is selected
            if(createdDefault)
                options.WorkspaceIndex = GetOptionsPage().AllowEnvironment ? -1 : 0;

            SetSolutionOptions(options);

            return VSConstants.S_OK;
        }

        #endregion
    }

    public class OptionPageGrid : DialogPage
    {
        // This seems like a terrible way to do it

        private bool _allowEnvironment = true;

        [Category("Options")]
        [DisplayName("Allow Environment")]
        [Description("Allow use of environment for workspace/connection settings. (See SDv, Connection > Environment Settings...; or see \"SD set\")")]
        public bool AllowEnvironment
        {
            get { return _allowEnvironment; }
            set { _allowEnvironment = value; }
        }

		private bool _autoCheckout = true;

		[Category("Options")]
		[DisplayName("Auto-Checkout Enabled")]
		[Description("Automatically checks out files on save/build. Not recommended for slow networks as this will block Visual Studio.")]
		public bool AutoCheckout
		{
			get { return _autoCheckout; }
			set { _autoCheckout = value; }
		}

        private bool _autoCheckoutOnEdit = false;

        [Category("Options")]
        [DisplayName("Auto-Checkout On Edit")]
        [Description("Automatically checks out files when edited, does not work projects or solutions. Disabled by default as it is more expensive than doing it on save/build. (requires restart)")]
        public bool AutoCheckoutOnEdit
        {
            get { return _autoCheckoutOnEdit; }
            set { _autoCheckoutOnEdit = value; }
        }

        private bool _autoCheckoutPrompt = false;

		[Category("Options")]
		[DisplayName("Prompt Before Auto-Checkout")]
		[Description("Prompts message to automatically check out files on build and save. Auto-checkout must be enabled.")]
		public bool AutoCheckoutPrompt
		{
			get { return _autoCheckoutPrompt; }
			set { _autoCheckoutPrompt = value; }
		}

        private string _checkoutPromptBlocklist = "";

        [Category("Options")]
        [DisplayName("Checkout Prompt Blocklist")]
        [Description("(Restart VS after changing) Comma separated list of full path directories or files that require a prompt before checkout even when Prompt Before Auto-Checkout is False.")]
        public string CheckoutPromptBlocklist
        {
            get { return _checkoutPromptBlocklist; }
            set { _checkoutPromptBlocklist = value; }
        }

        private string _checkoutPromptAllowlist = "";

        [Category("Options")]
        [DisplayName("Checkout Prompt Allowlist")]
        [Description("(Restart VS after changing) Exceptions to the Checkout Prompt Blocklist. Comma separated list of full path directories or files that won't require a prompt before checkout unless Prompt Before Auto-Checkout is True.")]
        public string CheckoutPromptAllowlist
        {
            get { return _checkoutPromptAllowlist; }
            set { _checkoutPromptAllowlist = value; }
        }

        private float _commandTimeoutSeconds = 10.0f;

        [Category("Options")]
        [DisplayName("Timeout (seconds)")]
        [Description("Timeout before SDEditVS stops waiting for the SD command to complete. 0.0 <= is equivalent to no timeout.")]
        public float CommandTimeoutSeconds
        {
            get { return _commandTimeoutSeconds; }
            set { _commandTimeoutSeconds = value; }
        }

        private bool _useReadOnlyFlag = true;

        [Category("Options")]
        [DisplayName("Use Read-Only File Flag")]
        [Description("SDEditVS will use the read-only file flag as a fast way to determine if a file is already checked out. Disable this option if you use the Allwrite workspace option or always want the commands enabled regardless of file state. It is not recommended, for performance overhead, to disable this when using auto-checkout as SDEditVS will issue a SD command for every save file request whether it is checked out or not.")]
        public bool UseReadOnlyFlag
        {
            get { return _useReadOnlyFlag; }
            set { _useReadOnlyFlag = value; }
        }

        private string _userName = "";
        private string _clientName = "";
        private string _server = "";

        [Category("Workspace 1")]
        [DisplayName("SD User Name")]
        [Description("User name")]
        public string UserName
        {
            get { return _userName; }
            set { _userName = value.Trim(); }
        }

        [Category("Workspace 1")]
        [DisplayName("SD Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName
        {
            get { return _clientName; }
            set { _clientName = value.Trim(); }
        }

        [Category("Workspace 1")]
        [DisplayName("SD Server")]
        [Description("e.g. localhost:1666")]
        public string Server
        {
            get { return _server; }
            set { _server = value.Trim(); }
        }

        private string _userName2 = "";
        private string _clientName2 = "";
        private string _server2 = "";

        [Category("Workspace 2")]
        [DisplayName("SD User Name")]
        [Description("User name")]
        public string UserName2
        {
            get { return _userName2; }
            set { _userName2 = value.Trim(); }
        }

        [Category("Workspace 2")]
        [DisplayName("SD Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName2
        {
            get { return _clientName2; }
            set { _clientName2 = value.Trim(); }
        }

        [Category("Workspace 2")]
        [DisplayName("SD Server")]
        [Description("e.g. localhost:1666")]
        public string Server2
        {
            get { return _server2; }
            set { _server2 = value.Trim(); }
        }

        private string _userName3 = "";
        private string _clientName3 = "";
        private string _server3 = "";

        [Category("Workspace 3")]
        [DisplayName("SD User Name")]
        [Description("User name")]
        public string UserName3
        {
            get { return _userName3; }
            set { _userName3 = value.Trim(); }
        }

        [Category("Workspace 3")]
        [DisplayName("SD Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName3
        {
            get { return _clientName3; }
            set { _clientName3 = value.Trim(); }
        }

        [Category("Workspace 3")]
        [DisplayName("SD Server")]
        [Description("e.g. localhost:1666")]
        public string Server3
        {
            get { return _server3; }
            set { _server3 = value.Trim(); }
        }

        private string _userName4 = "";
        private string _clientName4 = "";
        private string _server4 = "";

        [Category("Workspace 4")]
        [DisplayName("SD User Name")]
        [Description("User name")]
        public string UserName4
        {
            get { return _userName4; }
            set { _userName4 = value.Trim(); }
        }

        [Category("Workspace 4")]
        [DisplayName("SD Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName4
        {
            get { return _clientName4; }
            set { _clientName4 = value.Trim(); }
        }

        [Category("Workspace 4")]
        [DisplayName("SD Server")]
        [Description("e.g. localhost:1666")]
        public string Server4
        {
            get { return _server4; }
            set { _server4 = value.Trim(); }
        }

        private string _userName5 = "";
        private string _clientName5 = "";
        private string _server5 = "";

        [Category("Workspace 5")]
        [DisplayName("SD User Name")]
        [Description("User name")]
        public string UserName5
        {
            get { return _userName5; }
            set { _userName5 = value.Trim(); }
        }

        [Category("Workspace 5")]
        [DisplayName("SD Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName5
        {
            get { return _clientName5; }
            set { _clientName5 = value.Trim(); }
        }

        [Category("Workspace 5")]
        [DisplayName("SD Server")]
        [Description("e.g. localhost:1666")]
        public string Server5
        {
            get { return _server5; }
            set { _server5 = value.Trim(); }
        }

        private string _userName6 = "";
        private string _clientName6 = "";
        private string _server6 = "";

        [Category("Workspace 6")]
        [DisplayName("SD User Name")]
        [Description("User name")]
        public string UserName6
        {
            get { return _userName6; }
            set { _userName6 = value.Trim(); }
        }

        [Category("Workspace 6")]
        [DisplayName("SD Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName6
        {
            get { return _clientName6; }
            set { _clientName6 = value.Trim(); }
        }

        [Category("Workspace 6")]
        [DisplayName("SD Server")]
        [Description("e.g. localhost:1666")]
        public string Server6
        {
            get { return _server6; }
            set { _server6 = value.Trim(); }
        }

    }
}
