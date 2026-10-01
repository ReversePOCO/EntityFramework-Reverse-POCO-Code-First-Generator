using System;
using System.IO;
using Community.VisualStudio.Toolkit;
using Efrpg.Gui;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace EntityFramework_Reverse_POCO_Generator
{
    /// <summary>
    ///     Right-click a .tt to choose which tables, views, stored procedures and functions it generates.
    /// </summary>
    /// <remarks>
    ///     The connection string has to exist first: a template still carrying the placeholder cannot be read at
    ///     all, and the error the tool would give is less useful than being sent to the dialog that fixes it.
    /// </remarks>
    [Command(PackageGuids.EfrpgCommandSetString, PackageIds.ChooseObjectsCommand)]
    internal sealed class ChooseObjectsCommand : BaseCommand<ChooseObjectsCommand>
    {
        private const string Caption = "EntityFramework Reverse POCO Generator";

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Command.Visible = SolutionSelection.IsTemplate();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var item = SolutionSelection.Item();
            var path = SolutionSelection.Path(item);

            if (path == null || !File.Exists(path))
                return;

            var text          = TemplateFileUpdater.Read(item, path);
            var settings      = new TemplateSettingsFile(text);
            var configuration = TemplateConfiguration.ReadFrom(settings, Path.GetFileNameWithoutExtension(path) + "DbContext");

            // The literal, or what the template's own code produces when that code is an environment variable or
            // a file. Anything else - the placeholder, a variable this cannot evaluate - is a reason, not a read.
            string problem;
            var connectionString = configuration.ResolveConnectionString(out problem);

            if (connectionString == null)
            {
                await VS.MessageBox.ShowWarningAsync(Caption, problem);
                return;
            }

            var document = TemplateFilterDocument.Parse(text);
            if (document.RefusalReason != null)
            {
                await VS.MessageBox.ShowWarningAsync(Caption, document.RefusalReason);
                return;
            }

            var dialog = new ObjectPickerDialog(Path.GetFileName(path), document, null,
                token => SchemaReading.ReadAsync(configuration.Database.Name, connectionString, token),
                false);

            dialog.ShowModal();

            if (!dialog.Confirmed)
                return;

            string error;
            if (!TemplateFileUpdater.Apply(item, path, dialog.Text, out error))
                await VS.MessageBox.ShowWarningAsync(Caption,
                    "The .tt file was updated, but the generated code could not be refreshed automatically. " +
                    "Save the .tt file to regenerate it." + Environment.NewLine + Environment.NewLine + error);
        }
    }
}
