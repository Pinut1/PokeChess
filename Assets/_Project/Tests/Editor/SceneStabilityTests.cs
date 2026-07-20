using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokeChess.Tests
{
    public class SceneStabilityTests
    {
        private const string NetworkScenePath = "Assets/Scenes/NetworkTest.unity";
        private const string GameScenePath = "Assets/Scenes/GameSceneTest.unity";

        [Test]
        public void RequiredScenes_AreEnabledInBuildSettings()
        {
            HashSet<string> enabledPaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToHashSet();

            Assert.That(enabledPaths, Does.Contain(NetworkScenePath));
            Assert.That(enabledPaths, Does.Contain(GameScenePath));
        }

        [Test]
        public void EnabledBuildScenes_HaveNoMissingScripts()
        {
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes.Where(scene => scene.enabled))
                {
                    Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                    List<string> missingObjects = FindObjectsWithMissingScripts(scene);

                    Assert.That(
                        missingObjects,
                        Is.Empty,
                        $"{buildScene.path} contains missing scripts: {string.Join(", ", missingObjects)}");
                }
            }
            finally
            {
                RestoreSceneSetup(originalSetup);
            }
        }

        [Test]
        public void GameScene_UsesNetworkMode_AndSingleOpponentBoardView()
        {
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                GameObject gameManager = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "GameManager");

                Assert.That(gameManager, Is.Not.Null, "GameSceneTest must contain one root GameManager.");

                MonoBehaviour[] behaviours = gameManager.GetComponents<MonoBehaviour>();
                MonoBehaviour networkManager = behaviours.SingleOrDefault(
                    behaviour => behaviour != null && behaviour.GetType().Name == "NetworkManager");
                int opponentViewCount = behaviours.Count(
                    behaviour => behaviour != null && behaviour.GetType().Name == "OpponentBoardView");

                Assert.That(networkManager, Is.Not.Null, "GameManager must contain NetworkManager.");
                Assert.That(opponentViewCount, Is.EqualTo(1), "GameManager must contain exactly one OpponentBoardView.");

                SerializedProperty soloMode = new SerializedObject(networkManager).FindProperty("_soloMode");
                Assert.That(soloMode, Is.Not.Null, "NetworkManager._soloMode serialized field was not found.");
                Assert.That(soloMode.boolValue, Is.False, "GameSceneTest must use Photon network mode.");
            }
            finally
            {
                RestoreSceneSetup(originalSetup);
            }
        }

        private static List<string> FindObjectsWithMissingScripts(Scene scene)
        {
            var result = new List<string>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (missingCount > 0)
                        result.Add($"{GetHierarchyPath(transform)} ({missingCount})");
                }
            }

            return result;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;

            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private static void RestoreSceneSetup(SceneSetup[] originalSetup)
        {
            if (originalSetup.Length > 0 && originalSetup.Any(scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
