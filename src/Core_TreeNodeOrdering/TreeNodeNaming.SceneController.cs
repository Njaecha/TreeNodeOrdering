using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using KKAPI;
using Studio;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using ExtensibleSaveFormat;
using MessagePack;
using TNO = TreeNodeOrdering.TreeNodeOrdering2;

namespace TreeNodeOrdering
{
    public class TreeNodeNamingSceneController : SceneCustomFunctionController
    {
        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();
            Dictionary<int, ObjectCtrlInfo> idObjectPairs = Studio.Studio.Instance.dicObjectCtrl;
            var idNamePairs = new Dictionary<int, string>();

            foreach (int id in idObjectPairs.Keys)
            {
                idNamePairs[id] = idObjectPairs[id].treeNodeObject.textName;
            }

            data.data.Add("names", idNamePairs.Count > 0 ? MessagePackSerializer.Serialize(idNamePairs) : null);

            SetExtendedData(data);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            TNO.TreeNodeNamingActive = false;

            if (operation == SceneOperationKind.Clear) return;

            PluginData data = GetExtendedData();

            if (data?.data == null) return;

            if (data.data.TryGetValue("names", out var temp) && temp != null)
            {
                var idNamePairs = MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[])temp);
                foreach (int id in idNamePairs.Keys)
                {
                    if (TNO.TreeNodeNamingFixTranslationHelper)
                    {
                        StartCoroutine(TNO.RenameDelayed(loadedItems[id], idNamePairs[id]));
                    }
                    else
                    {
                        TNO.renameItem(loadedItems[id], idNamePairs[id]);
                    }
                }
            }
            else TNO.Logger.LogError("[TreeNodeNaming] failed to obtain pluginData from OnLoad event");
        }
        protected override void OnObjectsCopied(ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
        {
            Dictionary<int, ObjectCtrlInfo> sceneObjects = Studio.Studio.Instance.dicObjectCtrl;
            foreach (int id in copiedItems.Keys)
            {
                if (copiedItems[id].kind != 6)
                    TNO.renameItem(copiedItems[id], sceneObjects[id].treeNodeObject.textName);
            }
        }
    }
}