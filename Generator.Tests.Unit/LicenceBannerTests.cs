using Efrpg.Licensing;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    [TestFixture]
    [Category(Constants.CI)]
    public class LicenceBannerTests
    {
        [Test]
        public void LicenceBanner_Commercial_IsEmpty()
        {
            var banner = Efrpg.Generators.Generator.LicenceBanner(LicenceType.Commercial);

            Assert.That(banner, Is.Empty);
        }

        [Test]
        public void LicenceBanner_Trial_SaysOutputIsLimited()
        {
            var banner = Efrpg.Generators.Generator.LicenceBanner(LicenceType.Trial);

            Assert.That(banner, Has.Some.Contains("only a few tables"));
        }

        [Test]
        public void LicenceBanner_Academic_DoesNotSayOutputIsLimited()
        {
            var banner = Efrpg.Generators.Generator.LicenceBanner(LicenceType.Academic);

            Assert.That(banner, Has.None.Contains("only a few tables"));
        }

        [Test]
        public void LicenceBanner_Academic_SaysNotForCommercialUse()
        {
            var banner = Efrpg.Generators.Generator.LicenceBanner(LicenceType.Academic);

            Assert.That(banner, Has.Some.Contains("Not for commercial use"));
        }
    }
}
