using System.Threading;
using System.Threading.Tasks;
using Efrpg.Gui;
using NUnit.Framework;

namespace Efrpg.Gui.Tests
{
    /// <summary>
    ///     The gate is the first thing a newcomer meets, and every branch of it is a machine state that is painful to
    ///     reproduce by hand - no tool, an old tool, a runtime without an SDK, a feed behind a proxy. All of them are
    ///     reachable here through the fake runner.
    /// </summary>
    [TestFixture]
    public class EfrpgToolGateTests
    {
        private const string UserProfile = @"C:\Users\test";
        private const string FallbackPath = @"C:\Users\test\.dotnet\tools\efrpg.exe";

        /// <summary>What a current tool actually prints, copied from a real run.</summary>
        private const string CurrentToolOutput = "efrpg 1.1.0\r\nwire format schema version 2";

        private static FakeProcessRunner RunnerWithSdk()
        {
            return new FakeProcessRunner()
                .Answer("dotnet", "--version", ProcessResult.Completed(0, "10.0.400", string.Empty));
        }

        private static Task<EfrpgToolStatus> Check(FakeProcessRunner runner)
        {
            return new EfrpgToolGate(runner, UserProfile).CheckAsync(CancellationToken.None);
        }

        [Test]
        public async Task CheckAsync_ToolNotInstalled_ReportsNotFoundAndTheInstallCommand()
        {
            var runner = RunnerWithSdk();

            var status = await Check(runner);

            Assert.That(status.State, Is.EqualTo(EfrpgToolState.NotFound));
            Assert.That(status.ExecutablePath, Is.Null);
            Assert.That(status.FixCommand, Is.EqualTo("dotnet tool install -g Efrpg"));
        }

        [Test]
        public async Task CheckAsync_ToolNotInstalled_TriesThePathThenTheDotnetToolsFolder()
        {
            var runner = RunnerWithSdk();

            await Check(runner);

            Assert.That(runner.Calls, Does.Contain("efrpg --version"));
            Assert.That(runner.Calls, Does.Contain(FallbackPath + " --version"));
        }

        [Test]
        public async Task CheckAsync_CurrentTool_IsReady()
        {
            var runner = RunnerWithSdk()
                .Answer("efrpg", "--version", ProcessResult.Completed(0, CurrentToolOutput, string.Empty));

            var status = await Check(runner);

            Assert.That(status.State, Is.EqualTo(EfrpgToolState.Ready));
            Assert.That(status.ToolVersion, Is.EqualTo("1.1.0"));
            Assert.That(status.SchemaVersion, Is.EqualTo(EfrpgToolGate.RequiredSchemaVersion));
            Assert.That(status.FixCommand, Is.Null);
        }

        [Test]
        public async Task CheckAsync_ToolOnThePath_IsMarkedOnPath()
        {
            var runner = RunnerWithSdk()
                .Answer("efrpg", "--version", ProcessResult.Completed(0, CurrentToolOutput, string.Empty));

            var status = await Check(runner);

            Assert.That(status.IsOnPath, Is.True);
        }

        /// <summary>
        ///     Visual Studio caches its environment at launch, so a tool the wizard installs is on disk but not on
        ///     the PATH this process inherited. Finding it only via the fallback is what tells the wizard to invoke
        ///     it by full path and to ask for a restart before the user saves the .tt.
        /// </summary>
        [Test]
        public async Task CheckAsync_ToolOnlyInTheDotnetToolsFolder_IsReadyButNotOnPath()
        {
            var runner = RunnerWithSdk()
                .Answer(FallbackPath, "--version", ProcessResult.Completed(0, CurrentToolOutput, string.Empty));

            var status = await Check(runner);

            Assert.That(status.State, Is.EqualTo(EfrpgToolState.Ready));
            Assert.That(status.IsOnPath, Is.False);
            Assert.That(status.ExecutablePath, Is.EqualTo(FallbackPath));
        }

        [Test]
        public async Task CheckAsync_ToolEmitsAnOlderSchema_IsTooOldAndOffersTheUpdateCommand()
        {
            var runner = RunnerWithSdk()
                .Answer("efrpg", "--version", ProcessResult.Completed(0, "efrpg 0.9.0\r\nwire format schema version 0", string.Empty));

            var status = await Check(runner);

            Assert.That(status.State, Is.EqualTo(EfrpgToolState.SchemaTooOld));
            Assert.That(status.SchemaVersion, Is.EqualTo(0));
            Assert.That(status.FixCommand, Is.EqualTo("dotnet tool update -g Efrpg"));
        }

        /// <summary>
        ///     A schema 1 tool sends the whole schema whatever the licence says, so it must be refused rather than used.
        /// </summary>
        [Test]
        public async Task CheckAsync_ToolThatDoesNotEnforceTheLicence_IsTooOld()
        {
            var runner = RunnerWithSdk()
                .Answer("efrpg", "--version", ProcessResult.Completed(0, "efrpg 1.0.2\r\nwire format schema version 1", string.Empty));

            var status = await Check(runner);

            Assert.That(status.State, Is.EqualTo(EfrpgToolState.SchemaTooOld));
        }

        /// <summary>
        ///     A tool built before the version handshake existed prints no schema line at all. That has to read as
        ///     zero and be rejected, exactly as a payload with no schemaVersion attribute is rejected by the reader.
        /// </summary>
        [Test]
        public async Task CheckAsync_ToolPredatingTheHandshake_ReadsAsSchemaZeroAndIsTooOld()
        {
            var runner = RunnerWithSdk()
                .Answer("efrpg", "--version", ProcessResult.Completed(0, "efrpg 0.1.0-alpha", string.Empty));

            var status = await Check(runner);

            Assert.That(status.SchemaVersion, Is.EqualTo(0));
            Assert.That(status.State, Is.EqualTo(EfrpgToolState.SchemaTooOld));
        }

        [Test]
        public async Task CheckAsync_ToolFailsToRun_IsNotUsableAndKeepsItsStderr()
        {
            var runner = RunnerWithSdk()
                .Answer("efrpg", "--version", ProcessResult.Completed(1, string.Empty, "The application to execute does not exist"));

            var status = await Check(runner);

            Assert.That(status.State, Is.EqualTo(EfrpgToolState.NotUsable));
            Assert.That(status.Diagnostics, Is.EqualTo("The application to execute does not exist"));
        }

        /// <summary>
        ///     'dotnet tool install' needs the SDK, not just a runtime, so a machine can have a working dotnet and
        ///     still be unable to install anything. The gate must say so rather than offering a button that fails.
        /// </summary>
        [Test]
        public async Task CheckAsync_RuntimeButNoSdk_ReportsTheSdkMissing()
        {
            var runner = new FakeProcessRunner()
                .Answer("dotnet", "--version", ProcessResult.Completed(1, string.Empty, "A compatible .NET SDK was not found"));

            var status = await Check(runner);

            Assert.That(status.DotnetSdkPresent, Is.False);
            Assert.That(status.DotnetSdkVersion, Is.Null);
        }

        [Test]
        public async Task CheckAsync_SdkPresent_ReportsItsVersion()
        {
            var status = await Check(RunnerWithSdk());

            Assert.That(status.DotnetSdkPresent, Is.True);
            Assert.That(status.DotnetSdkVersion, Is.EqualTo("10.0.400"));
        }

        [Test]
        public async Task InstallAsync_RunsTheCommandTheDialogDisplays()
        {
            var runner = new FakeProcessRunner()
                .Answer("dotnet", "tool install -g Efrpg", ProcessResult.Completed(0, "You can invoke the tool using efrpg", string.Empty));

            var result = await new EfrpgToolGate(runner, UserProfile).InstallAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(runner.Calls, Does.Contain("dotnet " + EfrpgToolGate.InstallCommand.Substring("dotnet ".Length)));
        }

        [Test]
        public async Task UpdateAsync_RunsTheCommandTheDialogDisplays()
        {
            var runner = new FakeProcessRunner()
                .Answer("dotnet", "tool update -g Efrpg", ProcessResult.Completed(0, "Tool was successfully updated", string.Empty));

            var result = await new EfrpgToolGate(runner, UserProfile).UpdateAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(runner.Calls, Does.Contain("dotnet " + EfrpgToolGate.UpdateCommand.Substring("dotnet ".Length)));
        }

        /// <summary>
        ///     The whole point of "Copy command" as an escape hatch is the developer behind a proxy or on an internal
        ///     feed. When the install fails for them, what NuGet said is the only useful thing on screen, so it must
        ///     survive intact rather than being replaced by a friendly summary.
        /// </summary>
        [Test]
        public async Task InstallAsync_WhenTheFeedIsUnreachable_SurfacesStderrVerbatim()
        {
            const string nugetError = "error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json.";
            var runner = new FakeProcessRunner()
                .Answer("dotnet", "tool install -g Efrpg", ProcessResult.Completed(1, string.Empty, nugetError));

            var result = await new EfrpgToolGate(runner, UserProfile).InstallAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.StandardError, Is.EqualTo(nugetError));
        }
    }
}
