using System;
using System.IO;
using Community.VisualStudio.Toolkit;
using Efrpg.Gui;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace EntityFramework_Reverse_POCO_Generator
{
    /// <summary>
    ///     Right-click a .tt to edit every setting the generator has, then regenerate.
    /// </summary>
    /// <remarks>
    ///     This one serves people who already bought: the connection dialog gets somebody started, and this is
    ///     where they live afterwards. It deliberately does not hijack double-click or Open - the installed base
    ///     works in the text editor and expects the file to open there.
    /// </remarks>
    [Command(PackageGuids.EfrpgCommandSetString, PackageIds.SettingsEditorCommand)]
    internal sealed class SettingsEditorCommand : BaseCommand<SettingsEditorCommand>
    {
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

            var template = TemplateFileUpdater.Read(item, path);

            string error;
            var catalogue = SettingsMetadataFiles.For(template, out error);

            if (catalogue == null)
            {
                await VS.MessageBox.ShowErrorAsync("EntityFramework Reverse POCO Generator", error);
                return;
            }

            // The Add enumeration form lists the database's tables when the connection string can be resolved;
            // otherwise it takes typed names, and the dialog opens either way.
            string unresolved;
            var configuration    = TemplateConfiguration.ReadFrom(new TemplateSettingsFile(template), Path.GetFileNameWithoutExtension(path) + "DbContext");
            var connectionString = configuration.ResolveConnectionString(out unresolved);

            var dialog = new SettingsEditorDialog(Path.GetFileName(path),
                SettingsEditSession.Load(template, catalogue),
                connectionString == null
                    ? null
                    : (System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<SchemaReadResult>>)
                      (token => SchemaReading.ReadAsync(configuration.Database.Name, connectionString, token)));

            dialog.ShowModal();

            if (!dialog.Confirmed)
                return;

            if (!TemplateFileUpdater.Apply(item, path, dialog.Text, out error))
                await VS.MessageBox.ShowWarningAsync("EntityFramework Reverse POCO Generator",
                    "The .tt file was updated, but the generated code could not be refreshed automatically. " +
                    "Save the .tt file to regenerate it." + Environment.NewLine + Environment.NewLine + error);

            if (dialog.OpenSetting != null)
                await OpenAtAsync(path, SettingsEditSession.LineNumberOf(dialog.Text, dialog.OpenSetting));
        }

        /// <summary>Opens the .tt in the editor with the caret on a line, after the saved text has been written.</summary>
        private static async Task OpenAtAsync(string path, int lineNumber)
        {
            var view = await VS.Documents.OpenAsync(path);
            if (view == null || view.TextView == null || lineNumber < 1)
                return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var snapshot = view.TextView.TextSnapshot;
            if (lineNumber > snapshot.LineCount)
                return;

            var line = snapshot.GetLineFromLineNumber(lineNumber - 1);
            view.TextView.Caret.MoveTo(line.Start);
            view.TextView.ViewScroller.EnsureSpanVisible(line.Extent, Microsoft.VisualStudio.Text.Editor.EnsureSpanVisibleOptions.AlwaysCenter);
        }
    }
}
