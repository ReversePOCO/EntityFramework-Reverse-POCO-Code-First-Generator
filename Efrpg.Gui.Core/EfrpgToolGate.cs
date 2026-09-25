using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Efrpg.Gui
{
    /// <summary>
    ///     Decides whether the efrpg dotnet tool on this machine can serve the generator, and installs or updates it
    ///     when it cannot.
    /// </summary>
    /// <remarks>
    ///     It checks the wire format schema version, not the package version. The schema version is the thing the T4
    ///     template actually floor-checks when it parses the tool's XML, so the gate asks the same question the
    ///     generator will ask - surfaced as a dialog before anything is generated, rather than as a comment inside a
    ///     broken output file. The package version travels only so messages can name it.
    ///
    ///     The compatibility direction is asymmetric and deliberately so. The tool is installed globally and shared
    ///     by every project on the machine; the template is pinned inside each project and rarely upgraded. Newer
    ///     tool with an older template is the normal case and must keep working, so this is a floor check, never a
    ///     match.
    /// </remarks>
    public sealed class EfrpgToolGate
    {
        /// <summary>
        ///     Must equal EfrpgResultXmlReader.RequiredSchemaVersion in the Generator project. The two cannot share a
        ///     constant - the reader must stay plain source under Generator/ so BuildTT can concatenate it into the
        ///     .ttinclude, and this assembly is netstandard2.0 while that one is net48 - so
        ///     ToolGateSchemaFloorTests guards the pair instead.
        /// </summary>
        public const int RequiredSchemaVersion = 2;

        public const string ExecutableName  = "efrpg";
        public const string PackageId       = "Efrpg";
        public const string InstallCommand  = "dotnet tool install -g Efrpg";
        public const string UpdateCommand   = "dotnet tool update -g Efrpg";

        private const string Dotnet = "dotnet";

        private static readonly Regex ToolVersionPattern   = new Regex(@"^\s*efrpg\s+(?<version>\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex SchemaVersionPattern = new Regex(@"wire\s+format\s+schema\s+version\s+(?<version>\d+)", RegexOptions.IgnoreCase);

        private readonly IProcessRunner _runner;
        private readonly string _userProfileFolder;

        public EfrpgToolGate(IProcessRunner runner)
            : this(runner, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        {
        }

        public EfrpgToolGate(IProcessRunner runner, string userProfileFolder)
        {
            if (runner == null)
                throw new ArgumentNullException(nameof(runner));

            _runner            = runner;
            _userProfileFolder = userProfileFolder;
        }

        /// <summary>
        ///     Where 'dotnet tool install -g' puts the tool. Tried when the bare name is not on the PATH, because
        ///     Visual Studio caches its environment at launch: a tool installed after VS started is on disk but not
        ///     on the PATH this process inherited.
        /// </summary>
        public string FallbackExecutablePath =>
            string.IsNullOrEmpty(_userProfileFolder)
                ? null
                : Path.Combine(_userProfileFolder, ".dotnet", "tools", ExecutableName + ".exe");

        public async Task<EfrpgToolStatus> CheckAsync(CancellationToken cancellationToken)
        {
            var sdk = await CheckDotnetSdkAsync(cancellationToken).ConfigureAwait(false);

            var path    = ExecutableName;
            var version = await _runner.RunAsync(path, "--version", null, cancellationToken).ConfigureAwait(false);

            if (!version.Started && !string.IsNullOrEmpty(FallbackExecutablePath))
            {
                path    = FallbackExecutablePath;
                version = await _runner.RunAsync(path, "--version", null, cancellationToken).ConfigureAwait(false);
            }

            if (!version.Started)
                return new EfrpgToolStatus(EfrpgToolState.NotFound, null, null, 0, sdk.Present, sdk.Version, version.StandardError);

            if (version.ExitCode != 0 || string.IsNullOrWhiteSpace(version.StandardOutput))
                return new EfrpgToolStatus(EfrpgToolState.NotUsable, path, null, 0, sdk.Present, sdk.Version, version.StandardError);

            var toolVersion   = Match(ToolVersionPattern, version.StandardOutput);
            var schemaVersion = ParseInt(Match(SchemaVersionPattern, version.StandardOutput));

            // A tool built before the handshake existed prints no schema line at all, which reads back as 0 - the
            // same reading the XML reader gives a payload with no schemaVersion attribute, and correctly too old.
            var state = schemaVersion < RequiredSchemaVersion ? EfrpgToolState.SchemaTooOld : EfrpgToolState.Ready;

            return new EfrpgToolStatus(state, path, toolVersion, schemaVersion, sdk.Present, sdk.Version, version.StandardError);
        }

        public Task<ProcessResult> InstallAsync(CancellationToken cancellationToken)
        {
            return _runner.RunAsync(Dotnet, "tool install -g " + PackageId, null, cancellationToken);
        }

        public Task<ProcessResult> UpdateAsync(CancellationToken cancellationToken)
        {
            return _runner.RunAsync(Dotnet, "tool update -g " + PackageId, null, cancellationToken);
        }

        private async Task<DotnetSdk> CheckDotnetSdkAsync(CancellationToken cancellationToken)
        {
            // 'dotnet --version' prints the SDK version and fails when only a runtime is installed, which is exactly
            // the distinction that matters: 'dotnet tool install' needs the SDK.
            var result = await _runner.RunAsync(Dotnet, "--version", null, cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
                return new DotnetSdk(false, null);

            var version = result.StandardOutput.Trim();
            return new DotnetSdk(version.Length > 0, version.Length > 0 ? version : null);
        }

        private static string Match(Regex pattern, string text)
        {
            var match = pattern.Match(text ?? string.Empty);
            return match.Success ? match.Groups["version"].Value : null;
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : 0;
        }

        private struct DotnetSdk
        {
            public DotnetSdk(bool present, string version)
            {
                Present = present;
                Version = version;
            }

            public bool Present { get; }
            public string Version { get; }
        }
    }
}
