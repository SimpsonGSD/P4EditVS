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
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace SDEditVS
{
    internal class UpdateSolutionEvents : IVsUpdateSolutionEvents
    {
        private readonly Commands _commands;
        private UInt32 _cookie;

        public UpdateSolutionEvents(Commands commands)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _commands = commands;

            // Subscribe to events
            IVsSolutionBuildManager buildManager = (IVsSolutionBuildManager)Package.GetGlobalService(typeof(SVsSolutionBuildManager)) ?? throw new ArgumentNullException(nameof(buildManager));
            buildManager.AdviseUpdateSolutionEvents(this, out _cookie);
        }

        public int UpdateSolution_Begin(ref int pfCancelUpdate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _commands.EditSolution();
            return VSConstants.S_OK;
        }
        public int UpdateSolution_Cancel() { return 0; }
        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand) { return 0; }
        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate) { return 0; }
        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy) { return 0; }
    }

    internal class RunningDocTableEvents : IVsRunningDocTableEvents3
    {
        private readonly DTE _dte;
        private readonly RunningDocumentTable _runningDocumentTable;
        private readonly Commands _commands;
        private uint _rdtCookie;

        public RunningDocTableEvents(Commands commands, DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _commands = commands;
            _dte = dte;

            IOleServiceProvider ServiceProvider = Package.GetGlobalService(typeof(IOleServiceProvider)) as IOleServiceProvider ?? throw new ArgumentNullException(nameof(ServiceProvider));

            _runningDocumentTable = new RunningDocumentTable(new ServiceProvider(ServiceProvider)) ?? throw new ArgumentNullException(nameof(_runningDocumentTable));

            _rdtCookie = _runningDocumentTable.Advise(this);
        }

        public int OnBeforeSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var documentInfo = _runningDocumentTable.GetDocumentInfo(docCookie);
            var documentPath = documentInfo.Moniker;
            _commands.EditFile(documentPath);

            return VSConstants.S_OK;
        }


        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRdtLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRdtLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents3.OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld,
            string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents2.OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld,
            string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;

        }
    }
}