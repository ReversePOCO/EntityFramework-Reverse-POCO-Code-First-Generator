using System;
using System.Globalization;
using Efrpg;
using Efrpg.FileManagement;
using Efrpg.Generators;
using Efrpg.Licensing;
using Efrpg.Readers;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     The efrpg tool enforces the licence; the template only reports what the tool found. These pin that report to
    ///     the exact words the template used when it read ReversePOCO.txt itself, so moving the check changed nothing a
    ///     user can see.
    /// </summary>
    [TestFixture]
    [Category(Constants.CI)]
    public class LicenceReportingTests
    {
        private const string LicenceFile = @"C:\Users\someone\Documents\ReversePOCO.txt";
        private const string ObtainAt = "// Please obtain your licence file at www.ReversePOCO.co.uk, and place it in your documents folder shown above.";

        private bool _showLicenseInfo;

        [SetUp]
        public void SetUp()
        {
            _showLicenseInfo = Settings.ShowLicenseInfo;
        }

        [TearDown]
        public void TearDown()
        {
            Settings.ShowLicenseInfo = _showLicenseInfo;
        }

        [Test]
        public void Init_LicenceNotFound_SaysWhereItLookedAndFallsBackToTrial()
        {
            var output = Init(new RawLicence { Status = LicenceStatus.NotFound, File = LicenceFile }).Output;

            Assert.That(output, Does.Contain("// Licence file " + LicenceFile + " not found." + Environment.NewLine +
                                             ObtainAt + Environment.NewLine +
                                             "// Defaulting to Trial version."));
        }

        [Test]
        public void Init_LicenceExpired_SaysItHasExpiredAndFallsBackToTrial()
        {
            var output = Init(new RawLicence { Status = LicenceStatus.Expired, File = LicenceFile }).Output;

            Assert.That(output, Does.Contain("// Your licence file " + LicenceFile + " has expired." + Environment.NewLine +
                                             ObtainAt + Environment.NewLine +
                                             "// Defaulting to Trial version."));
        }

        [Test]
        public void Init_LicenceInvalid_SaysItIsNotValidAndFallsBackToTrial()
        {
            var output = Init(new RawLicence { Status = LicenceStatus.Invalid, File = LicenceFile }).Output;

            Assert.That(output, Does.Contain("// Your licence file " + LicenceFile + " is not valid." + Environment.NewLine +
                                             ObtainAt + Environment.NewLine +
                                             "// Defaulting to Trial version."));
        }

        [Test]
        public void Init_ValidLicence_ReportsNothing()
        {
            var output = Init(ValidCommercialLicence()).Output;

            Assert.That(output, Is.Empty);
        }

        [Test]
        public void Init_ValidLicence_HeaderCarriesTheLicenceDetails()
        {
            Settings.ShowLicenseInfo = true;

            var header = Init(ValidCommercialLicence()).Header;

            Assert.That(header, Does.Contain("// Registered to: Someone" + Environment.NewLine +
                                             "// Company      : Somewhere Ltd" + Environment.NewLine +
                                             "// Licence Type : Commercial" + Environment.NewLine +
                                             "// Licences     : 5" + Environment.NewLine +
                                             "// Valid until  : 31 MAR 2027"));
        }

        /// <summary>
        ///     The date must read exactly as it does in ReversePOCO.txt, whatever language the machine runs in. en-US is
        ///     here because half of all licences are sold there.
        /// </summary>
        [TestCase("fr-FR")]
        [TestCase("en-US")]
        public void Init_ValidLicence_HeaderDateIsDdMmmYyyyInEnglishWhateverTheCulture(string cultureName)
        {
            Settings.ShowLicenseInfo = true;
            var licence = ValidCommercialLicence();
            licence.ValidUntil = new DateTime(2027, 9, 30);
            var previousCulture = CultureInfo.CurrentCulture;
            string header;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                header = Init(licence).Header;
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }

            Assert.That(header, Does.Contain("// Valid until  : 30 SEP 2027"));
        }

        [Test]
        public void Init_LicenceNotFound_HeaderReportsATrial()
        {
            Settings.ShowLicenseInfo = true;

            var header = Init(new RawLicence { Status = LicenceStatus.NotFound, File = LicenceFile }).Header;

            Assert.That(header, Does.Contain("// Licence Type : Trial - for non-commercial trial use only"));
        }

        private static RawLicence ValidCommercialLicence()
        {
            return new RawLicence
            {
                Status       = LicenceStatus.Valid,
                File         = LicenceFile,
                RegisteredTo = "Someone",
                Company      = "Somewhere Ltd",
                LicenceType  = LicenceType.Commercial,
                NumLicences  = "5",
                ValidUntil   = new DateTime(2027, 3, 31)
            };
        }

        private static (string Output, string Header) Init(RawLicence licence)
        {
            var outer = new GeneratedTextTransformation();
            var generator = new GeneratorEfCore(new FileManagementService(outer));
            var result = FakeDatabaseReader.CreateResult();
            result.Licence = licence;

            generator.Init(result, string.Empty);

            return (outer.FileData.ToString(), generator.GetPreHeaderInfo());
        }
    }
}
