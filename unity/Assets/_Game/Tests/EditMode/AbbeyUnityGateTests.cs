using System.Collections.Generic;
using Abbey.EditorTools;
using NUnit.Framework;

namespace Abbey.Tests.EditMode
{
    public class AbbeyUnityGateTests
    {
        [Test]
        public void CreateReport_UsesSafeDefaults()
        {
            var report = AbbeyUnityGate.CreateReport(
                "2026-07-05T00:00:00.0000000Z", "6000.5.2f1");

            Assert.AreEqual("2026-07-05T00:00:00.0000000Z", report.generatedAt);
            Assert.AreEqual("6000.5.2f1", report.unityVersion);
            Assert.IsFalse(report.sceneBuilt);
            Assert.AreEqual("not_run", report.assetImportValidation);
            Assert.IsNotNull(report.canonicalScreenshots);
            Assert.IsNotNull(report.errors);
            Assert.IsFalse(report.passed);
        }

        [Test]
        public void FinalizeReport_PassesOnlyWhenAllRequiredStepsPassed()
        {
            var report = AbbeyUnityGate.CreateReport("now", "unity");
            report.sceneBuilt = true;
            report.assetImportValidation = "pass";
            report.canonicalScreenshots = new List<string>
            {
                "day_camp.png",
                "dusk_recall.png",
                "night_attack.png",
                "morning_after.png",
            };

            AbbeyUnityGate.FinalizeReport(report);

            Assert.IsTrue(report.passed);
        }

        [Test]
        public void FinalizeReport_FailsWhenErrorsWereRecorded()
        {
            var report = AbbeyUnityGate.CreateReport("now", "unity");
            report.sceneBuilt = true;
            report.assetImportValidation = "pass";
            report.canonicalScreenshots = new List<string>
            {
                "day_camp.png",
                "dusk_recall.png",
                "night_attack.png",
                "morning_after.png",
            };

            AbbeyUnityGate.AddError(report, "compileAndConsoleCheck: compiler error");
            AbbeyUnityGate.FinalizeReport(report);

            Assert.IsFalse(report.passed);
            Assert.AreEqual(1, report.errors.Count);
        }

        [Test]
        public void AddError_DeduplicatesMessages()
        {
            var report = AbbeyUnityGate.CreateReport("now", "unity");

            AbbeyUnityGate.AddError(report, "same");
            AbbeyUnityGate.AddError(report, "same");

            Assert.AreEqual(1, report.errors.Count);
        }
    }
}
