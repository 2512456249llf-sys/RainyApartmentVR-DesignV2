using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RainyApartmentVR.Tests
{
    public sealed class Stage1EnvironmentTests
    {
        [Test]
        public void Unity版本是六点三长期支持版()
        {
            StringAssert.StartsWith("6000.3.", Application.unityVersion);
        }

        [Test]
        public void 项目专用目录已经建立()
        {
            Assert.That(Directory.Exists("Assets/_Project"), Is.True);
            Assert.That(Directory.Exists("Assets/_Project/Scenes"), Is.True);
            Assert.That(Directory.Exists("Assets/_Project/Scripts/Runtime"), Is.True);
            Assert.That(Directory.Exists("Assets/_Project/Scripts/Editor"), Is.True);
            Assert.That(Directory.Exists("Assets/_Project/Materials"), Is.True);
            Assert.That(Directory.Exists("Assets/_Project/Prefabs"), Is.True);
            Assert.That(Directory.Exists("Assets/_Project/Audio"), Is.True);
            Assert.That(Directory.Exists("Assets/_Project/Settings"), Is.True);
        }
    }
}

