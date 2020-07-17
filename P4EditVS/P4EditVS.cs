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

namespace P4EditVS
{
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
    //[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(P4EditVS.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionPageGrid), "P4EditVS", "Settings", 0, 0, true)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class P4EditVS : AsyncPackage
    {
        /// <summary>
        /// P4EditVS GUID string.
        /// </summary>
        public const string PackageGuidString = "d6a4db63-698d-4d16-bbc0-944fe52f83db";

        private int mSelectedWorkspace = 0;
        public int SelectedWorkspace { get => mSelectedWorkspace; set => mSelectedWorkspace = value; }

        public string ClientName
        {
            get
            {
                return GetWorkspaceName(mSelectedWorkspace);
            }
        }

        public string UserName
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                // This is truly awful
                switch (mSelectedWorkspace)
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
                // This is truly awful
                switch (mSelectedWorkspace)
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

        public string GetWorkspaceName(int index)
        {
            OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
            // This is truly awful
            switch (index)
            {
                case -1:
                    if (page.AllowEnvironment) return "(Use environment)";
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
        /// Initializes a new instance of the <see cref="P4EditVS"/> class.
        /// </summary>
        public P4EditVS()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

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
            await Commands.InitializeAsync(this);
        }

        public string GetGlobalP4CmdLineOptions()
        {
            if (mSelectedWorkspace == -1)
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
                "Client name is empty. This must be set under Tools->Options->P4EditVS->Settings",
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
            if (mSelectedWorkspace == -1)
            {
                return true;
            }

            if (UserName == "")
            {
                VsShellUtilities.ShowMessageBox(
                this,
                "User name is empty. This must be set under Tools->Options->P4EditVS->Settings",
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
                "Server is empty. This must be set under Tools->Options->P4EditVS->Settings",
                "Invalid Settings",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                return false;
            }

            return true;
        }

        #endregion
    }

    public class OptionPageGrid : DialogPage
    {
        // This seems like a terrible way to do it

        private bool mAllowEnvironment = false;

        [Category("Environment")]
        [DisplayName("Allow Environment")]
        [Description("Allow use of environment for workspace/connection settings. (See p4v, Connection > Environment Settings...; or see \"p4 set\")")]
        public bool AllowEnvironment
        {
            get { return mAllowEnvironment; }
            set { mAllowEnvironment = value; }
        }

        private string mUserName = "";
        private string mClientName = "";
        private string mServer = "";

        [Category("Workspace 1")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName
        {
            get { return mUserName; }
            set { mUserName = value.Trim(); }
        }

        [Category("Workspace 1")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName
        {
            get { return mClientName; }
            set { mClientName = value.Trim(); }
        }

        [Category("Workspace 1")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server
        {
            get { return mServer; }
            set { mServer = value.Trim(); }
        }

        private string mUserName2 = "";
        private string mClientName2 = "";
        private string mServer2 = "";

        [Category("Workspace 2")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName2
        {
            get { return mUserName2; }
            set { mUserName2 = value.Trim(); }
        }

        [Category("Workspace 2")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName2
        {
            get { return mClientName2; }
            set { mClientName2 = value.Trim(); }
        }

        [Category("Workspace 2")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server2
        {
            get { return mServer2; }
            set { mServer2 = value.Trim(); }
        }

        private string mUserName3 = "";
        private string mClientName3 = "";
        private string mServer3 = "";

        [Category("Workspace 3")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName3
        {
            get { return mUserName3; }
            set { mUserName3 = value.Trim(); }
        }

        [Category("Workspace 3")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName3
        {
            get { return mClientName3; }
            set { mClientName3 = value.Trim(); }
        }

        [Category("Workspace 3")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server3
        {
            get { return mServer3; }
            set { mServer3 = value.Trim(); }
        }

        private string mUserName4 = "";
        private string mClientName4 = "";
        private string mServer4 = "";

        [Category("Workspace 4")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName4
        {
            get { return mUserName4; }
            set { mUserName4 = value.Trim(); }
        }

        [Category("Workspace 4")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName4
        {
            get { return mClientName4; }
            set { mClientName4 = value.Trim(); }
        }

        [Category("Workspace 4")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server4
        {
            get { return mServer4; }
            set { mServer4 = value.Trim(); }
        }

        private string mUserName5 = "";
        private string mClientName5 = "";
        private string mServer5 = "";

        [Category("Workspace 5")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName5
        {
            get { return mUserName5; }
            set { mUserName5 = value.Trim(); }
        }

        [Category("Workspace 5")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName5
        {
            get { return mClientName5; }
            set { mClientName5 = value.Trim(); }
        }

        [Category("Workspace 5")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server5
        {
            get { return mServer5; }
            set { mServer5 = value.Trim(); }
        }

        private string mUserName6 = "";
        private string mClientName6 = "";
        private string mServer6 = "";

        [Category("Workspace 6")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName6
        {
            get { return mUserName6; }
            set { mUserName6 = value.Trim(); }
        }

        [Category("Workspace 6")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName6
        {
            get { return mClientName6; }
            set { mClientName6 = value.Trim(); }
        }

        [Category("Workspace 6")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server6
        {
            get { return mServer6; }
            set { mServer6 = value.Trim(); }
        }

    }
}
