using System.IO;
using MindAttic.Overworld.Online;
using MindAttic.Overworld.Runtime;
using Unity.Multiplayer;
using Unity.Multiplayer.Editor;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MindAttic.Overworld.Editor
{
    /// <summary>
    /// Networks the main scene on the Unity.Multiplayer kit (U.OVW-T46), in code like the rest of the scene: it writes
    /// the two network prefabs (<see cref="OverworldPlayer"/>, every connection's Player Prefab, and
    /// <see cref="OverworldWorldSync"/>, keyed <see cref="OverworldWorldSync.Key"/>) and their <see cref="NetPrefabList"/>,
    /// runs the kit's own Add Front End (a NetworkManager with Unity Transport, a <see cref="NetBootstrap"/> and the
    /// <c>NetFrontEnd</c>), and adds <see cref="OverworldOnline"/>, which keeps local play first. The bootstrap never serves
    /// by itself in a headless process (<c>serveWhenHeadless</c> off) nor paces one at launch
    /// (<c>applyHeadlessSettings</c> off), so batch-mode runs of the scene (the scripted captures) play locally exactly as
    /// before; a dedicated server is launched with <c>-mode server</c>.
    /// </summary>
    public static class OnlineBuilder
    {
        public const string Folder = "Assets/Unity.Rea.Overworld/Resources/Online";
        public const string PlayerPrefabPath = Folder + "/OverworldPlayer.prefab";
        public const string WorldSyncPrefabPath = Folder + "/OverworldWorldSync.prefab";
        public const string PrefabListPath = "Assets/Unity.Rea.Overworld/Online/OverworldNetPrefabs.asset";

        /// <summary>Resources paths the tests load the prefabs from.</summary>
        public const string PlayerResource = "Online/OverworldPlayer";
        public const string WorldSyncResource = "Online/OverworldWorldSync";

        /// <summary>Writes the network prefabs and their list, and returns the list.</summary>
        public static NetPrefabList BuildPrefabs()
        {
            Directory.CreateDirectory(Folder);

            var player = new GameObject("OverworldPlayer");
            player.AddComponent<NetworkObject>();
            player.AddComponent<OverworldPlayer>();
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);

            var sync = new GameObject("OverworldWorldSync");
            sync.AddComponent<NetworkObject>();
            sync.AddComponent<NetKey>();
            sync.AddComponent<OverworldWorldSync>();
            var syncPrefab = PrefabUtility.SaveAsPrefabAsset(sync, WorldSyncPrefabPath);
            Object.DestroyImmediate(sync);

            var list = AssetDatabase.LoadAssetAtPath<NetPrefabList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetPrefabList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }

            list.Add(playerPrefab);
            list.Add(syncPrefab);
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }

        /// <summary>Networks <paramref name="scene"/> (loaded, being built) around <paramref name="bootstrap"/>.</summary>
        public static OverworldOnline Create(Scene scene, OverworldBootstrap bootstrap)
        {
            var list = BuildPrefabs();
            var result = NetFrontEndSetup.Run(scene, new NetSceneSetupOptions { PrefabList = list, RegisterProjectPrefabs = false });
            if (!result.Succeeded)
                throw new System.InvalidOperationException("OnlineBuilder: the kit's Add Front End failed: " + result.Message);

            var root = result.Scene.Root;
            result.Scene.Manager.NetworkConfig.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            EditorUtility.SetDirty(result.Scene.Manager);

            var net = new SerializedObject(result.Scene.Bootstrap);
            net.FindProperty("serveWhenHeadless").boolValue = false;
            net.FindProperty("autoStart").boolValue = false;
            // Not at launch: a headless local run keeps its frame pacing; OverworldOnline paces a headless server once its
            // session starts instead.
            net.FindProperty("applyHeadlessSettings").boolValue = false;
            net.ApplyModifiedPropertiesWithoutUndo();

            var online = root.AddComponent<OverworldOnline>();
            online.FrontEnd = (Unity.Multiplayer.UI.NetFrontEnd)result.FrontEnd;
            online.Game = bootstrap;
            online.WorldSyncPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldSyncPrefabPath);
            return online;
        }

        /// <summary>Whether <paramref name="root"/> is the networked root <see cref="Create"/> makes, wired to <paramref name="bootstrap"/>.</summary>
        public static bool IsCurrent(GameObject root, OverworldBootstrap bootstrap)
        {
            if (root == null || root.name != NetSceneSetup.RootName) return false;
            var online = root.GetComponent<OverworldOnline>();
            return online != null && online.FrontEnd != null && online.Game == bootstrap && online.WorldSyncPrefab != null
                && root.GetComponent<NetBootstrap>() != null && root.GetComponent<NetworkManager>() != null
                && File.Exists(PlayerPrefabPath) && File.Exists(WorldSyncPrefabPath) && File.Exists(PrefabListPath);
        }
    }
}
