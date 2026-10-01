using System;
using System.IO;
using System.Linq;
using Community.VisualStudio.Toolkit;
using Efrpg.Gui;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace EntityFramework_Reverse_POCO_Generator
{
    /// <summary>
    ///     Upgrades one v3 template to v4 with a preview and a confirmation. Reached from the right-click command
    ///     and from the offer that appears when a v3 file is opened, so it lives in neither.
    /// </summary>
    internal static class TemplateUpgradeFlow
    {
        private const string Caption = "EntityFramework Reverse POCO Generator";

        private const string UpgradeGuide =
            "https://github.com/ReversePOCO/EntityFramework-Reverse-POCO-Code-First-Generator/wiki";

        /// <summary>
        ///     Runs the whole flow. <paramref name="item"/> may be null for a file outside any project; the update
        ///     then goes through whichever editor has it open.
        /// </summary>
        public static async Task RunAsync(ProjectItem item, string path)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (path == null || !File.Exists(path))
                return;

            var result = TemplateUpgrade.Upgrade(TemplateFileUpdater.Read(item, path));

            if (!result.Succeeded)
            {
                await VS.MessageBox.ShowWarningAsync(Caption,
                    "This template cannot be upgraded automatically:" + Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine + Environment.NewLine, result.Blockers.ToArray()) +
                    Environment.NewLine + Environment.NewLine +
                    "Upgrade it by hand using the guide at " + UpgradeGuide);
                return;
            }

            var preview = new UpgradePreviewDialog(Path.GetFileName(path), result.Changes);
            preview.ShowModal();

            if (!preview.Confirmed)
                return;

            string error;
            if (!TemplateFileUpdater.Apply(item, path, result.Text, out error))
                await VS.MessageBox.ShowWarningAsync(Caption,
                    "The .tt file was upgraded, but the generated code could not be refreshed automatically. " +
                    "Save the .tt file to regenerate it." + Environment.NewLine + Environment.NewLine + error);
        }
    }
}
