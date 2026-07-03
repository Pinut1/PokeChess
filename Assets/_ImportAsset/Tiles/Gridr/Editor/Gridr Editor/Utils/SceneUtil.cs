//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


namespace Gridr.Editor.Utils
{
    public static class SceneUtil
    {
        public static void RecordChanges(Object ob)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(ob);
            EditorUtility.SetDirty(ob);
            if(ob is GameObject go)
                EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}