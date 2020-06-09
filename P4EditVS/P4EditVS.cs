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

       //public string ServerAddress
       //{
       //    get
       //    {
       //        OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
       //        return page.ServerAddress;
       //    }
       //}

        public string ClientName
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.ClientName;
            }
        }

        public string UserName
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.UserName;
            }
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
            return string.Format("-c {0} -u {1}", ClientName, UserName);
        }

        public bool ValidateUserSettings()
        {
            if(ClientName == "")
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

            return true;
        }

        #endregion
    }

    public class OptionPageGrid : DialogPage
    {
       // private string mServerAddress = "";
       //
       // [Category("Perforce Settings")]
       // [DisplayName("Perforce Server Address")]
       // [Description("Address of Perforce server. E.g. 127.0.0.1:1666")]
       // public string ServerAddress
       // {
       //     get { return mServerAddress; }
       //     set { mServerAddress = value; }
       // }

        private string mUserName = "";

        [Category("Perforce Settings")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName
        {
            get { return mUserName; }
            set { mUserName = value; }
        }

        private string mClientName = "";

        [Category("Perforce Settings")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName
        {
            get { return mClientName; }
            set { mClientName = value; }
        }
    }


}
