using System;
using System.IO;
using Community.VisualStudio.Toolkit;
using Efrpg.Gui;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace EntityFramework_Reverse_POCO_Generator
{
    /// <summary>
    ///     Right-click a .tt file to reopen the connection dialog on what that file already says, change it, and
    ///     regenerate.
    /// </summary>
    /// <remarks>
    ///     The wizard only runs once, when the file is added. Without this, a user who pressed Skip, or who mistyped
    ///     a database name, or who wants to point the same template at a different server, has no way back to the
    ///     dialog at all - the only route is to find the right line in the .tt by hand, which is precisely what the
    ///     GUI exists to avoid.
    /// </remarks>
    [Command(PackageGuids.EfrpgCommandSetString, PackageIds.ConfigureTemplateCommand)]
    internal sealed class ConfigureTemplateCommand : BaseCommand<ConfigureTemplateCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Command.Visible = SolutionSelection.IsTemplate();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            // Everything below touches the DTE object model and shows a modal window, both of which are
            // main-thread only. Switching is the supported way to say so from a Task-returning method.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var item = SolutionSelection.Item();
            var path = SolutionSelection.Path(item);

            if (path == null || !File.Exists(path))
                return;

            var settings = new TemplateSettingsFile(TemplateFileUpdater.Read(item, path));
            var dialog   = new ConnectionDialog(
                TemplateConfiguration.ReadFrom(settings, Path.GetFileNameWithoutExtension(path) + "DbContext"),
                false);

            dialog.ShowModal();

            if (!dialog.Confirmed)
                return;

            string error;
            if (!TemplateFileUpdater.Apply(item, path, dialog.Result.ApplyTo(settings), out error))
                await VS.MessageBox.ShowWarningAsync("EntityFramework Reverse POCO Generator",
                    "The .tt file was updated, but the generated code could not be refreshed automatically. " +
                    "Save the .tt file to regenerate it." + Environment.NewLine + Environment.NewLine + error);
        }
    }
}
