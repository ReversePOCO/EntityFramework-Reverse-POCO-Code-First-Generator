using System;
using System.Globalization;
using Efrpg.Licensing;
using Efrpg.Readers;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    [TestFixture]
    [Category(Constants.CI)]
    public class EfrpgResultXmlReaderLicenceTests
    {
        [Test]
        public void Read_LicenceElement_ReadsEveryField()
        {
            var licence = EfrpgResultXmlReader.Read(Payload(
                "<Licence status=\"Valid\" file=\"C:\\Docs\\ReversePOCO.txt\" registeredTo=\"Someone\" company=\"Somewhere Ltd\" " +
                "licenceType=\"Commercial\" numLicences=\"5\" validUntil=\"31 MAR 2027\" />")).Licence;

            Assert.Multiple(() =>
            {
                Assert.That(licence.Status, Is.EqualTo(LicenceStatus.Valid));
                Assert.That(licence.File, Is.EqualTo(@"C:\Docs\ReversePOCO.txt"));
                Assert.That(licence.RegisteredTo, Is.EqualTo("Someone"));
                Assert.That(licence.Company, Is.EqualTo("Somewhere Ltd"));
                Assert.That(licence.LicenceType, Is.EqualTo(LicenceType.Commercial));
                Assert.That(licence.NumLicences, Is.EqualTo("5"));
                Assert.That(licence.ValidUntil, Is.EqualTo(new DateTime(2027, 3, 31)));
            });
        }

        // en-US is here because half of all licences are sold there.
        [TestCase("fr-FR")]
        [TestCase("en-US")]
        public void Read_ValidUntil_ParsesTheEnglishMonthWhateverTheCulture(string cultureName)
        {
            var previousCulture = CultureInfo.CurrentCulture;
            RawLicence licence;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                licence = EfrpgResultXmlReader.Read(Payload("<Licence status=\"Valid\" licenceType=\"Commercial\" validUntil=\"30 SEP 2027\" />")).Licence;
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }

            Assert.That(licence.ValidUntil, Is.EqualTo(new DateTime(2027, 9, 30)));
        }

        [Test]
        public void Read_StatusANewerToolInvented_ReadsAsInvalid()
        {
            var licence = EfrpgResultXmlReader.Read(Payload("<Licence status=\"Revoked\" licenceType=\"Commercial\" />")).Licence;

            Assert.That(licence.Status, Is.EqualTo(LicenceStatus.Invalid));
        }

        [Test]
        public void Read_LicenceTypeANewerToolInvented_ReadsAsTrial()
        {
            var licence = EfrpgResultXmlReader.Read(Payload("<Licence status=\"Valid\" licenceType=\"Enterprise\" />")).Licence;

            Assert.That(licence.LicenceType, Is.EqualTo(LicenceType.Trial));
        }

        [Test]
        public void Read_NoLicenceElement_ReadsAsATrialWithNoFileFound()
        {
            var licence = EfrpgResultXmlReader.Read(Payload(string.Empty)).Licence;

            Assert.Multiple(() =>
            {
                Assert.That(licence.Status, Is.EqualTo(LicenceStatus.NotFound));
                Assert.That(licence.LicenceType, Is.EqualTo(LicenceType.Trial));
            });
        }

        /// <summary>
        ///     A schema 1 tool sends the whole schema whatever the licence says, so it must be refused rather than
        ///     trusted, or upgrading the template would quietly turn off the trial limits.
        /// </summary>
        [Test]
        public void Read_ToolThatDoesNotEnforceTheLicence_IsRejectedAsTooOld()
        {
            var payload = "<EfrpgResult schemaVersion=\"1\" toolVersion=\"1.0.2\"><Tables /></EfrpgResult>";

            Assert.That(() => EfrpgResultXmlReader.Read(payload), Throws.Exception.With.Message.Contains("too old"));
        }

        private static string Payload(string licenceElement)
        {
            return "<EfrpgResult schemaVersion=\"2\" toolVersion=\"1.1.0\">" + licenceElement + "<Tables /></EfrpgResult>";
        }
    }
}
