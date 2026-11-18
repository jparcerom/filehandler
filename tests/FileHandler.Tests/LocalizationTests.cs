using System.Globalization;
using FileHandler.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandler.Tests;

[TestClass]
public sealed class LocalizationTests
{
    [TestMethod]
    public void ResolveCode_InstallerPreferenceOverridesSystemLanguage()
    {
        Assert.AreEqual("en", AppLanguage.ResolveCode("en", "es-ES"));
        Assert.AreEqual("es", AppLanguage.ResolveCode("es", "en-US"));
    }

    [TestMethod]
    public void ResolveCode_UsesSupportedSystemLanguageOrSpanishFallback()
    {
        Assert.AreEqual("en", AppLanguage.ResolveCode(null, "en-GB"));
        Assert.AreEqual("es", AppLanguage.ResolveCode(null, "fr-FR"));
        Assert.AreEqual("es", AppLanguage.ResolveCode(null, null));
    }

    [TestMethod]
    public void EnglishResources_TranslateTheInterfaceAndCoverEverySpanishKey()
    {
        var english = CultureInfo.GetCultureInfo("en");

        Assert.AreEqual("What would you like to do?", Text.Get("HomeQuestion", english));
        Assert.AreEqual("03-March", Text.Get("MarchFolder", english));
        Assert.AreEqual("Version 2.0", Text.Format("SplashVersion", english, "2.0"));
        Assert.AreEqual(0, Text.GetMissingKeys(english).Length);
    }
}
