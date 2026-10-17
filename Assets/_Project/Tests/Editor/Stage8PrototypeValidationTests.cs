using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace RainyApartmentVR.Tests
{
    public sealed class Stage8PrototypeValidationTests
    {
        [Test]
        public void 完整原型通过一键总体验收()
        {
            Type validator = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("RainyApartmentVR.Editor.PrototypeValidator"))
                .FirstOrDefault(type => type != null);
            Assert.That(validator, Is.Not.Null, "找不到原型总体验收器。");

            MethodInfo collectIssues = validator.GetMethod("CollectIssues", BindingFlags.Public | BindingFlags.Static);
            Assert.That(collectIssues, Is.Not.Null);
            string[] issues = (string[])collectIssues.Invoke(null, null);
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }
    }
}
