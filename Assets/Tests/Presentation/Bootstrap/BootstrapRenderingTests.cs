using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Uckers.Tests.Presentation.Bootstrap
{
    public class BootstrapRenderingTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void CreatesCameraAndLight_WhenBootstrapperRuns()
        {
            var host = new GameObject("BootstrapperHost");
            var bootstrapper = host.AddComponent<Bootstrapper>();

            bootstrapper.EditorEnsureCameraAndLightFallback();

            var camerasAfterFirst = Object.FindObjectsOfType<Camera>();
            Assert.That(camerasAfterFirst.Length, Is.EqualTo(1), "Expected a single camera after first ensure call.");

            int lightsAfterFirst = GetDirectionalLightCount();
            Assert.That(lightsAfterFirst, Is.EqualTo(1), "Expected a single directional light after first ensure call.");

            bootstrapper.EditorEnsureCameraAndLightFallback();

            var camerasAfterSecond = Object.FindObjectsOfType<Camera>();
            Assert.That(camerasAfterSecond.Length, Is.EqualTo(1), "Ensure call should be idempotent for cameras.");

            int lightsAfterSecond = GetDirectionalLightCount();
            Assert.That(lightsAfterSecond, Is.EqualTo(1), "Ensure call should be idempotent for lights.");
        }

        [Test]
        public void AutoBootstrap_CreatesBootstrapper_WhenNonePresent()
        {
            Assert.That(FindBootstrapper(), Is.Null);

            Bootstrapper.AutoBootstrap();

            Assert.That(FindBootstrapper(), Is.Not.Null);
        }

        private static int GetDirectionalLightCount()
        {
#if UNITY_2023_1_OR_NEWER
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var lights = Object.FindObjectsOfType<Light>();
#endif
            int count = 0;
            foreach (var light in lights)
            {
                if (light != null && light.type == LightType.Directional)
                {
                    count++;
                }
            }

            return count;
        }

        private static Bootstrapper FindBootstrapper()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<Bootstrapper>();
#else
            return Object.FindObjectOfType<Bootstrapper>();
#endif
        }
    }
}
