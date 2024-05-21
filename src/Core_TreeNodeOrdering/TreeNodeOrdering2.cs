using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using KKAPI;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TreeNodeOrdering
{
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
#if KKS
    [BepInDependency("com.joan6694.illusionplugins.kksus", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.jim60105.kks.studiosaveworkspaceorderfix", "21.09.28.0")]
#elif KK
    [BepInDependency("com.joan6694.illusionplugins.kkus", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.jim60105.kk.studiosaveworkspaceorderfix", "20.08.05.0")]
#endif
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("CharaStudio")]
    public class TreeNodeOrdering2 : BaseUnityPlugin
    {
        public const string PluginName = "TreeNodeOrdering";
        public const string GUID = "org.njaecha.plugins.treenodeordering";
        public const string Version = "2.0.0";

        internal new static ManualLogSource Logger;

        internal static TreeNodeCtrl treeNodeCtrl;
        internal static ScrollRect scrollRect;

        static readonly int[] legalKinds = { 0, 1, 3, 5, 7 };

        internal static float spacing = 0;
        internal static float nodeHeight = 0;
        internal static float scaleFactor = 0;

        private static bool currentlyDragging = false;

        // line rendering:
        private static RenderTexture rTex;
        private static Camera rCam;
        private static TreeNodeOrderingUI UI;

        /// <summary>
        /// Fired when the user starts dragging a TreeNodeObject
        /// </summary>
        public static EventHandler<StartDragEventArgs> Drag;
        /// <summary>
        /// Fired when the user drops a TreeNodeObject
        /// </summary>
        public static EventHandler<DropEventArgs> Drop;

        private static ConfigEntry<float> pressTimeConfig;

        void Awake()
        {
            TreeNodeOrdering2.Logger = base.Logger;
            KKAPI.Studio.StudioAPI.StudioLoadedChanged += registerCtrls;
            Drag += StartDrag;
            Drag += UpdateUISizeValues;
            Drop += HandleDrop;

            pressTimeConfig = Config.Bind("Values", "P&H Time", 0.3f, new ConfigDescription("The time in second you have to press and hold the TreeNodeObject to start dragging", new AcceptableValueRange<float>(0.1f, 1.0f)));
        }


        void Start()
        {
            rTex = new RenderTexture(Screen.width, Screen.height, 32);
            // Create a new camera
            GameObject renderCameraObject = new GameObject("TreeNodeOrdering_UI_Camera");
            renderCameraObject.transform.SetParent(this.transform);
            rCam = renderCameraObject.AddComponent<Camera>();
            rCam.targetTexture = rTex;
            rCam.clearFlags = CameraClearFlags.SolidColor;
            rCam.backgroundColor = Color.clear;
            rCam.cullingMask = 0;
            UI = rCam.GetOrAddComponent<TreeNodeOrderingUI>();
        }

        void OnGUI()
        { 
            if (rTex != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rTex);
        }


        private void registerCtrls(object sender, EventArgs e)
        {
            treeNodeCtrl = Singleton<Studio.Studio>.Instance.treeNodeCtrl;
            scrollRect = GameObject.Find("StudioScene/Canvas Object List/Image Bar/Scroll View").GetComponent<ScrollRect>();
        }

        void Update()
        {
            if (treeNodeCtrl != null) // init okay
            {
                // drag event
                if (Input.GetMouseButtonDown(0) && treeNodeCtrl.m_ObjectRoot.GetComponent<RectTransform>().ContainsMouse())
                {
                    StartCoroutine(moveCheckDelayProcess(pressTimeConfig.Value, Input.mousePosition));
                }


                // drop event
                if (Input.GetMouseButtonUp(0))
                {
                    if (currentlyDragging)
                    {
                        currentlyDragging = false;
                    }
                }
            }
        }

        public void UpdateUISizeValues(object sender, StartDragEventArgs e)
        {
            spacing = GameObject.Find("StudioScene/Canvas Object List/Image Bar/Scroll View/Viewport/Content").GetComponent<GUITree.TreeRoot>().spacing;
            nodeHeight = e.dragging[0].gameObject.GetComponent<GUITree.TreeNode>().preferredHeight;
            scaleFactor = GameObject.Find("StudioScene/Canvas Object List").GetComponent<Canvas>().scaleFactor;
        }

        // the user has click for a certain time to start the dragging process
        IEnumerator moveCheckDelayProcess(float delay, Vector3 initialMousePosition)
        {
            yield return new WaitForSeconds(delay);
            if (Input.GetMouseButton(0) 
                && treeNodeCtrl.m_ObjectRoot.GetComponent<RectTransform>().ContainsMouse() 
                && (Input.mousePosition - initialMousePosition).sqrMagnitude < 100 // mouse wasnt moved away
                )
            {
                // fire drag event
                TreeNodeWrapper hovered = getHoveredTreeNodeObject();
                if (hovered == null || hovered.treeNode == null || !hovered.treeNode.TryGetObjectInfo(out ObjectInfo info) || !legalKinds.Contains(info.kind)) yield break;
                List<TreeNodeObject> list;
                if (treeNodeCtrl.selectNode == hovered.treeNode) list = treeNodeCtrl.selectNodes.ToList();
                else list = new List<TreeNodeObject>() { hovered.treeNode };
                Drag.Invoke(this, new StartDragEventArgs(list));
            }
        }

        private static GameObject draggingElement = null;
        private Vector2 draggingMouseDelta = Vector2.zero;
        private Dictionary<TreeNodeObject, bool> wasOpened = new Dictionary<TreeNodeObject, bool>();

        private void StartDrag(object sender, StartDragEventArgs e)
        {
            for (int i = 0; i < e.dragging.Count; i++)
            {
                TreeNodeObject tno = e.dragging[i];
                if (tno.childCount > 0 && tno.treeState == TreeNodeObject.TreeState.Open)
                {
                    tno.SetTreeState(TreeNodeObject.TreeState.Close);
                    wasOpened.Add(tno, true);
                }
                else wasOpened.Add(tno, false);
                if (i == 0)
                {
                    // create drag copy
                    Transform org = tno.transform.Find("Select Button");
                    Button copy = Instantiate(org).GetComponent<Button>();
                    (copy.transform as RectTransform).Copy(org as RectTransform);
                    copy.onClick = new Button.ButtonClickedEvent(); // nothing event
                    draggingElement = copy.gameObject;
                    currentlyDragging = true;
                    draggingMouseDelta = org.transform.position - Input.mousePosition;
                    (copy.transform as RectTransform).Rotate(new Vector3(0, 0, 4)); // funny effect

                }
                tno.transform.Find("Select Button")?.gameObject.SetActive(false);
                tno.transform.Find("Visible")?.gameObject.SetActive(false);
                tno.transform.Find("Open Tree")?.gameObject.SetActive(false);
            }
            // close if has children


            scrollRect.vertical = false; // disable scrolling
             
            StartCoroutine(DoDragging(e));

        }

        float testFactor = 0.001f;

        IEnumerator DoDragging(StartDragEventArgs e)
        {
            int hoveredIndex = 0;
            DropType hoveredDropType = DropType.Nothing;
            TreeNodeObject hoveredParent = null;

            while (currentlyDragging)
            {
                RectTransform rt = draggingElement.transform as RectTransform;

                rt.position = new Vector3(Input.mousePosition.x + draggingMouseDelta.x, Input.mousePosition.y + draggingMouseDelta.y, rt.position.z);
                #region Hover Logic
                TreeNodeWrapper hovered = getHoveredTreeNodeObject();
                if (hovered != null && hovered.treeNode != null)
                {
                    RectTransform rect = hovered.treeNode.transform as RectTransform;
                    if (
                        e.dragging.Contains(hovered.treeNode) // hovering a dragged treenode
                        || (!hovered.treeNode.enableAddChild && !hovered.treeNode.enableChangeParent) // hovering TNO that does not allow children being moved itself
                        // || hoveringOwnChild(e.dragging, hovered.treeNode)
                    )
                    {
                        UI.RefY = null;
                        hoveredDropType = DropType.Nothing;
                    }
                    else if (!hovered.treeNode.enableChangeParent && hovered.treeNode.enableAddChild) // hovering TNO that can't be moved but allows children
                    {
                        UI.LeftX = rect.position.x;
                        UI.RightX = rect.position.x + rect.WorldSize().x;
                        UI.RefY = rect.anchoredPosition.y;
                        hoveredParent = hovered.treeNode.parent;
                        UI.CurrentDrawType = hoveredDropType = DropType.Parent;
                        hoveredIndex = hovered.listIndex;
                    }
                    else if (rect.ContainsMouse())
                    {
                        UI.LeftX = rect.position.x;
                        UI.RightX = rect.position.x + rect.WorldSize().x;
                        UI.RefY = rect.anchoredPosition.y;

                        hoveredParent = hovered.treeNode.parent;

                        List<TreeNodeObject> list = hoveredParent == null ? treeNodeCtrl.m_TreeNodeObject : hovered.treeNode.parent.child;
                        bool isOnSameLevel = list.Contains(e.dragging[0]);
                        bool hoveringNeighborAbove = isOnSameLevel && e.Index > 0 && hovered.treeNode == list[e.Index - 1];
                        bool hoveringNeighborBelow = isOnSameLevel && e.Index < list.Count - 1 && hovered.treeNode == list[e.Index + 1];

                        if (!hoveringNeighborBelow && Input.mousePosition.y > rect.position.y - (nodeHeight * scaleFactor) / 5) // upper fifth
                        {
                            UI.CurrentDrawType = hoveredDropType = isOnSameLevel ? DropType.InsertAbove : DropType.InsertAndParentAbove;
                        }
                        else if (!hoveringNeighborAbove && hovered.treeNode.childCount == 0 && Input.mousePosition.y < rect.position.y - (nodeHeight * scaleFactor) / 5 * 4) // lower fifth
                        {
                            UI.CurrentDrawType = hoveredDropType = isOnSameLevel ? DropType.InsertBelow : DropType.InsertAndParentBelow;
                        }
                        else if (!(e.dragging[0].parent != null && hovered.treeNode == e.dragging[0].parent) && hovered.treeNode.enableAddChild) // middle fifths
                        {
                            UI.CurrentDrawType = hoveredDropType = DropType.Parent;
                        }
                        else
                        {
                            UI.RefY = null;
                            hoveredDropType = DropType.Nothing;
                        }
                        hoveredIndex = hovered.listIndex;
                    }
                }


                #endregion
                yield return null;
            }
            // invoke drop event
            Drop.Invoke(this, new DropEventArgs(e.dragging, hoveredDropType, hoveredIndex, hoveredParent));

        }

        private bool hoveringOwnChild(TreeNodeObject dragging, TreeNodeObject lookAt)
        {
            // recursively walk up parents until reaching the base level or finding the dragging object
            if (lookAt.parent == null) return false;
            if (lookAt.parent == dragging) return true;
            return hoveringOwnChild(dragging, lookAt.parent); 
        }

        private void HandleReodering(bool above, DropEventArgs e, TreeNodeObject destination)
        {
            if (e.newParent == null)
            {
                treeNodeCtrl.m_TreeNodeObject.Remove(e.dragging[0]);
                treeNodeCtrl.m_TreeNodeObject.Insert(treeNodeCtrl.m_TreeNodeObject.IndexOf(destination) + (above ? 0 : 1), e.dragging[0]);
                for (int i = 1; i < e.dragging.Count; i++)
                {
                    treeNodeCtrl.m_TreeNodeObject.Remove(e.dragging[i]);
                    treeNodeCtrl.m_TreeNodeObject.Insert(treeNodeCtrl.m_TreeNodeObject.IndexOf(e.dragging[i-1]) + 1, e.dragging[i]);
                }
            }
            else
            {
                e.newParent.m_child.Remove(e.dragging[0]);
                e.newParent.m_child.Insert(e.newParent.m_child.IndexOf(destination) + (above ? 0 : 1), e.dragging[0]);
                for (int i = 1; i < e.dragging.Count; i++)
                {
                    e.newParent.m_child.Remove(e.dragging[i]);
                    e.newParent.m_child.Insert(e.newParent.m_child.IndexOf(e.dragging[i - 1]) + 1, e.dragging[i]);
                }

                // test for character
                int? CharaDictKey = null;
                OCIChar ParentCharacter = null;

                if (!destination.enableChangeParent && destination.parent != null && !destination.parent.enableChangeParent)
                {
                    TreeNodeObject charaTreeNodeObject = destination.parent.parent;
                    if (Studio.Studio.Instance.dicInfo.TryGetValue(charaTreeNodeObject, out ObjectCtrlInfo oci) && oci is OCIChar)
                    {
                        CharaDictKey = ((OCIChar)oci).dicAccessoryPoint[destination];
                        ParentCharacter = (OCIChar)oci;
                    }
                }

                // ObjectInfo reodering
                if (e.newParent.TryGetObjectInfo(out ObjectInfo parentInfo) && e.dragging[0].TryGetObjectInfo(out ObjectInfo draggingInfo) && destination.TryGetObjectInfo(out ObjectInfo destinationInfo))
                {
                    if (parentInfo is OIItemInfo itemInfo) // parent is item
                    {
                        itemInfo.child.Remove(draggingInfo);
                        itemInfo.child.Insert(itemInfo.child.IndexOf(destinationInfo) + (above ? 0 : 1), draggingInfo);
                        for (int i = 1; i < e.dragging.Count; i++)
                        {
                            if (e.dragging[i].TryGetObjectInfo(out ObjectInfo secondary) && e.dragging[i-1].TryGetObjectInfo(out ObjectInfo secondaryDestinationInfo))
                            {
                                itemInfo.child.Remove(secondary);
                                itemInfo.child.Insert(itemInfo.child.IndexOf(secondaryDestinationInfo) + 1, secondary);
                            }
                        }
                    }
                    else if (parentInfo is OIFolderInfo folderInfo) // parent is folder
                    {
                        folderInfo.child.Remove(draggingInfo);
                        folderInfo.child.Insert(folderInfo.child.IndexOf(destinationInfo) + (above ? 0 : 1), draggingInfo);
                        for (int i = 1; i < e.dragging.Count; i++)
                        {
                            if (e.dragging[i].TryGetObjectInfo(out ObjectInfo secondary) && e.dragging[i - 1].TryGetObjectInfo(out ObjectInfo secondaryDestinationInfo))
                            {
                                folderInfo.child.Remove(secondary);
                                folderInfo.child.Insert(folderInfo.child.IndexOf(secondaryDestinationInfo) + 1, secondary);
                            }
                        }
                    }
                }
                // parent is characer
                else if (CharaDictKey.HasValue && ParentCharacter != null && e.dragging[0].TryGetObjectInfo(out ObjectInfo draggingInfo2) && destination.TryGetObjectInfo(out ObjectInfo destinationInfo2))
                {
                    OICharInfo chaInfo = ParentCharacter.objectInfo as OICharInfo;
                    int Key = CharaDictKey.Value;
                    chaInfo.child[Key].Remove(draggingInfo2);
                    chaInfo.child[Key].Insert(chaInfo.child[Key].IndexOf(destinationInfo2) + (above ? 0 : 1), draggingInfo2);
                    for (int i = 1; i < e.dragging.Count; i++)
                    {
                        if (e.dragging[i].TryGetObjectInfo(out ObjectInfo secondary) && e.dragging[i - 1].TryGetObjectInfo(out ObjectInfo secondaryDestinationInfo))
                        {
                            chaInfo.child[Key].Remove(secondary);
                            chaInfo.child[Key].Insert(chaInfo.child[Key].IndexOf(secondaryDestinationInfo) + 1, secondary);
                        }
                    }
                }
            }
        }

        private void HandleDrop(object sender, DropEventArgs e)
        {
            scrollRect.vertical = true; // enable scrolling

            UI.RefY = null;

            TreeNodeObject tno = e.dragging[0];

            foreach(TreeNodeObject node in e.dragging)
            {
                node.transform.Find("Select Button")?.gameObject.SetActive(true);
                node.transform.Find("Visible")?.gameObject.SetActive(true);
                if (node.childCount > 0) node.transform.Find("Open Tree")?.gameObject.SetActive(true);
                if (wasOpened.ContainsKey(node) && wasOpened[node]) node.SetTreeState(TreeNodeObject.TreeState.Open);
            }
            wasOpened.Clear();

            #region handle drop

            List<TreeNodeObject> preList = e.newParent == null ? treeNodeCtrl.m_TreeNodeObject : e.newParent.child;
            TreeNodeObject destination = preList[e.dropIndex];



            switch (e.dropType)
            {
                case DropType.InsertAbove:
                case DropType.InsertAndParentAbove:
                    foreach (TreeNodeObject node in e.dragging)
                    {
                        if (node.parent != destination.parent) treeNodeCtrl.SetParent(node, destination.parent);
                    }
                    HandleReodering(true, e, destination);
                    break;
                case DropType.InsertBelow:
                case DropType.InsertAndParentBelow:
                    foreach (TreeNodeObject node in e.dragging)
                    {
                        if (node.parent != destination.parent) treeNodeCtrl.SetParent(node, destination.parent);
                    }
                    HandleReodering(false, e, destination);
                    break;
                case DropType.Parent:
                    foreach(TreeNodeObject node in e.dragging) treeNodeCtrl.SetParent(node, destination);
                    break;
                case DropType.Nothing:
                    break;
            }

           

            #endregion
            treeNodeCtrl.RefreshHierachy();
#if KKS
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.joan6694.illusionplugins.kksus")) HSUS_Fix();
#elif KK
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.joan6694.illusionplugins.kkus")) HSUS_Fix();
#endif

            Destroy(draggingElement);
            draggingElement = null;
        }

        private void HSUS_Fix()
        {
            HSUS.Features.OptimizeNEO.WorkspaceCtrl_Awake_Patches._treeNodeList = treeNodeCtrl.m_TreeNodeObject;
        }


        /// <summary>
        /// Get the hovered TreeNodeObject, if any, from all TreeNodeObjects
        /// </summary>
        /// <returns>The TreeNodeObject; its position in the list it is contained in; the list it is contained in</returns>
        private TreeNodeWrapper getHoveredTreeNodeObject()
        {
            return getHoveredTreeNodeObject(treeNodeCtrl.m_TreeNodeObject);
        }

        /// <summary>
        /// Get the hovered TreeNodeObject, if any, from all list of TreeNodeObjects and its children
        /// </summary>
        /// <param name="treeNodeObjects">List of TreeNodeObject to search within</param>
        /// <returns>The TreeNodeObject; its position in the list it is contained in; the list it is contained in</returns>
        private TreeNodeWrapper getHoveredTreeNodeObject(List<TreeNodeObject> treeNodeObjects)
        {
            for (int i = 0; i < treeNodeObjects.Count; i++)
            {
                TreeNodeObject tno = treeNodeObjects[i];
                if (!tno.isActiveAndEnabled) continue;
                // if (tno == draggedObject) continue;
                if (tno.gameObject.GetComponent<RectTransform>().ContainsMouse())
                {
                    return new TreeNodeWrapper(tno, i, treeNodeObjects);
                }
                if (tno.childCount > 0)
                {
                    TreeNodeWrapper tu = getHoveredTreeNodeObject(tno.child);
                    if (tu != null && tu.treeNode != null) return tu;
                }
            }
            return null;
        }

        private class TreeNodeWrapper
        {
            public TreeNodeObject treeNode { get; private set; }
            public int listIndex { get; private set; }
            public List<TreeNodeObject> list { get; private set; }

            public TreeNodeWrapper(TreeNodeObject treeNode, int listIndex, List<TreeNodeObject> list)
            {
                this.treeNode = treeNode;
                this.listIndex = listIndex;
                this.list = list;
            }

            public override string ToString()
            {
                return $"[{listIndex}] {treeNode?.textName}";
            }
        }

        public class StartDragEventArgs
        {
            public readonly List<TreeNodeObject> parentList;
            public readonly int Index;
            public List<TreeNodeObject> dragging;

            public StartDragEventArgs(List<TreeNodeObject> dragging)
            {
                this.dragging = dragging;
                TreeNodeObject draggingObject = dragging[0]; // root dragging object
                
                // not character and on root level
                if (draggingObject.parent == null)
                {
                    parentList = treeNodeCtrl.m_TreeNodeObject;
                    Index = parentList.IndexOf(draggingObject);
                    //Logger.LogDebug($"Registered as root level [{draggingObject.textName}]");
                }
                // not character child and has parent
                else 
                {
                    parentList = draggingObject.parent.child;
                    Index = parentList.IndexOf(draggingObject);
                    //Logger.LogDebug($"Registered as child of parent [{draggingObject.textName}]");
                }
            }
        }

        public class DropEventArgs
        {
            public readonly List<TreeNodeObject> dragging;
            public readonly DropType dropType;
            public readonly int dropIndex;
            public readonly TreeNodeObject newParent;

            public DropEventArgs(List<TreeNodeObject> dragging, DropType dropType, int dropIndex, TreeNodeObject newParent = null)
            {
                this.dragging = dragging;
                this.dropType = dropType;
                this.dropIndex = dropIndex;
                this.newParent = newParent;
            }
        }

        public enum DropType
        {
            InsertAbove,
            InsertBelow,
            InsertAndParentAbove,
            InsertAndParentBelow,
            Parent,
            Nothing
        }
    }

    public static class Extensions
    {
        public static bool TryGetObjectInfo(this TreeNodeObject tno, out ObjectInfo objectInfo)
        {
            if (!Studio.Studio.Instance.dicInfo.ContainsKey(tno))
            {
                objectInfo = null;
                return false;
            }
            else
            {
                objectInfo = Studio.Studio.Instance.dicInfo[tno].objectInfo;
                return true;
            }
        }

        public static bool ContainsMouse(this RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            if (new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y).Contains(Input.mousePosition))
            {
                return true;
            }
            return false;
        }

        public static Vector2 WorldSize(this RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return new Vector2(corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        public static void Copy(this RectTransform self, RectTransform copyFrom)
        {
            RectTransform parent = copyFrom.parent as RectTransform;
            self.SetParent(parent.parent, true);
            self.localScale = copyFrom.localScale;
            self.anchorMin = copyFrom.anchorMin;
            self.anchorMax = copyFrom.anchorMax;
            self.offsetMin = copyFrom.offsetMin;
            self.offsetMax = copyFrom.offsetMax;
            self.anchoredPosition = copyFrom.anchoredPosition;
        }
    }
}
