using HarmonyLib;
using Studio;
using System;
using System.Collections.Generic;
using System.Text;

namespace TreeNodeOrdering
{
    internal class TreeNodeOrderingPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Studio.Studio), "SaveScene")]
        public static void SaveScenePrefix(Studio.Studio __instance)
        {
            Dictionary<int, ObjectInfo> dictionary = new Dictionary<int, ObjectInfo>();
            foreach (TreeNodeObject treeNodeObject in __instance.treeNodeCtrl.m_TreeNodeObject)
            {
                if (treeNodeObject != null)
                {
                    if (__instance.dicInfo.TryGetValue(treeNodeObject, out ObjectCtrlInfo objectCtrlInfo)
                        && __instance.sceneInfo.dicObject.TryGetValue(objectCtrlInfo.objectInfo.dicKey, out ObjectInfo objectInfo)
                        && objectInfo == objectCtrlInfo.objectInfo)
                    {
                        if (!dictionary.ContainsKey(objectInfo.dicKey))
                        {
                            dictionary.Add(objectInfo.dicKey, objectInfo);
                        }
                        else
                        {
                            TreeNodeOrdering2.Logger.LogWarning($"Tree Node with name {treeNodeObject.textName} has a key ({objectInfo.dicKey}) that was already present in the dictionary. This should never happen!!");
                            dictionary[objectInfo.dicKey] = objectInfo;
                        }
                        TreeNodeOrdering2.Logger.LogDebug($"{treeNodeObject.textName} [{objectInfo.dicKey}]");
                    }
                }
            }
            TreeNodeOrdering2.Logger.LogDebug($"Studio Workspace Order Fix: old dict lenght={__instance.sceneInfo.dicObject.Count}, new dict lenght={dictionary.Count} (this should be the same)");
            __instance.sceneInfo.dicObject = dictionary;
        }
    }
}
