/* NOT USED ANYMORE
 * SEE TreeNodeOrdering2

using BepInEx;
using BepInEx.Logging;
using KKAPI;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vectrosity;
namespace TreeNodeOrdering
{
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency("com.joan6694.illusionplugins.kksus", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.jim60105.kks.studiosaveworkspaceorderfix", "21.09.28.0")]
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("CharaStudio")]
    public class TreeNodeOrdering : BaseUnityPlugin
    {
        public const string PluginName = "TreeNodeOrdering";
        public const string GUID = "org.njaecha.plugins.treenodeordering";
        public const string Version = "1.0.0";

        internal new static ManualLogSource Logger;

        internal TreeNodeCtrl treeNodeCtrl;
        internal ScrollRect scrollRect;

        private bool dragging = false;

        private VectorLine insertLine;
        private int oldLineY = 0;
        private int dropPosition = 0;
        private int draggedObjectPosition;
        private int draggedObjectChildDictionaryKey;
        private OCIChar draggedObjectParentCharacter;
        private TreeNodeObject draggedObject;
        private float draggedObjectYMouseDelta;

        //ui values
        private float spacing = 0;
        private float nodeHeight = 0;
        private float scaleFactor = 0;

        void Awake()
        {
            TreeNodeOrdering.Logger = base.Logger;
            KKAPI.Studio.StudioAPI.StudioLoadedChanged += registerCtrls;
        }

        private void registerCtrls(object sender, EventArgs e)
        {
            treeNodeCtrl = Singleton<Studio.Studio>.Instance.treeNodeCtrl;
            spacing = 0;
            nodeHeight = 0;
            scaleFactor = 0;
            VectorLine.Destroy(ref insertLine);
            insertLine = null;
            scrollRect = GameObject.Find("StudioScene/Canvas Object List/Image Bar/Scroll View").GetComponent<ScrollRect>();
        }

        bool doUIDrag = false;

        void Update()
        {
            if (treeNodeCtrl != null)
            {
                // click on a Node
                if (Input.GetMouseButtonDown(0) && isMouseOverRectTransform(treeNodeCtrl.m_ObjectRoot.GetComponent<RectTransform>()))
                {
                    if (GameObject.Find("VectorCanvas").GetComponent<Canvas>().sortingOrder != 5) GameObject.Find("VectorCanvas").GetComponent<Canvas>().sortingOrder = 5;
                    StartCoroutine(moveCheckDelayProcess(0.3f));
                    var tu = getHoveredTreeNodeObject();

                    // check if TreeNodeObject is okay to drag
                    List<int> legalKinds = new List<int>() { 0, 1, 3, 5, 7 }; // Character: 0 | Item: 1 | Folder: 3 | Camera: 5 | Text: 7
                    ObjectInfo oi = objectInfoFromTreeNode(tu.treeNode);
                    if (oi == null) return;
                    if (!legalKinds.Contains(oi.kind)) return;

                    draggedObject = tu.treeNode;
                    draggedObjectPosition = tu.listIndex;
                    draggedObjectYMouseDelta = draggedObject.rectNode.position.y - Input.mousePosition.y;

                    // check if dragged object is child of a character
                    if (draggedObject.parent != null && draggedObject.parent.parent != null)
                    {
                        if (draggedObject.parent.enableChangeParent == false && draggedObject.parent.parent.enableChangeParent == false)
                        {
                            TreeNodeObject charaTreeNodeObject = draggedObject.parent.parent.parent;
                            if(Studio.Studio.Instance.dicInfo.TryGetValue(charaTreeNodeObject, out ObjectCtrlInfo oci) && oci is OCIChar)
                            {
                                draggedObjectChildDictionaryKey = ((OCIChar)oci).dicAccessoryPoint[draggedObject.parent];
                                draggedObjectParentCharacter = (OCIChar)oci;
                            }

                        }
                    }

                    // set UI values
                    if (spacing == 0 || scaleFactor == 0 || nodeHeight == 0)
                    {
                        spacing = GameObject.Find("StudioScene/Canvas Object List/Image Bar/Scroll View/Viewport/Content").GetComponent<GUITree.TreeRoot>().spacing;
                        nodeHeight = draggedObject.gameObject.GetComponent<GUITree.TreeNode>().preferredHeight;
                        scaleFactor = GameObject.Find("StudioScene/Canvas Object List").GetComponent<Canvas>().scaleFactor;
                    }
                }
                // release
                if (Input.GetMouseButtonUp(0))
                {
                    if (dragging)
                    {
                        drop();
                        draggedObjectChildDictionaryKey = -1;
                        draggedObjectParentCharacter = null;
                        dragging = false;
                        scrollRect.vertical = true;
                        draggedObject = null;
                    }
                    StopAllCoroutines();
                }
                // while dragging
                if (Input.GetMouseButton(0) && dragging)
                {
                    drag();
                    if (doUIDrag) dragUI();
                }
            }
        }

        private void dragUI()
        {
            if (!dragging || draggedObject is null) return;
            draggedObject.rectNode.position = new Vector3(draggedObject.rectNode.position.x, Input.mousePosition.y + draggedObjectYMouseDelta, draggedObject.rectNode.position.z);
        }

        // the user has click for a certain time to start the dragging process
        IEnumerator moveCheckDelayProcess(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (Input.GetMouseButton(0) && isMouseOverRectTransform(treeNodeCtrl.m_ObjectRoot.GetComponent<RectTransform>()))
            {
                dragging = true;
                // prevent dragging of the TreeView while dragging a node
                scrollRect.vertical = false;
            }
        }

        /// <summary>
        /// Check if the mouse is within a rectangle with a RectTransform
        /// </summary>
        /// <param name="rectTransform">RectTransform of the rectangle</param>
        /// <returns>True if mouse is within the rectangle</returns>
        private bool isMouseOverRectTransform(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            if (new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y).Contains(Input.mousePosition))
            {
                return true;
            }
            return false;
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
                if (isMouseOverRectTransform(tno.gameObject.GetComponent<RectTransform>()))
                {
                    return new TreeNodeWrapper(tno, i, treeNodeObjects);
                }
                if (tno.childCount > 0)
                {
                    TreeNodeWrapper tu = getHoveredTreeNodeObject(tno.child);
                    if (tu.treeNode != null) return tu;
                }
            }
            return new TreeNodeWrapper(null, 0, null);
        }

        /// <summary>
        /// calculates the lowest Y value the insertLine should go
        /// </summary>
        /// <param name="list">List within which user is dragging</param>
        /// <returns>the Y value</returns>
        private int getLowestY(List<TreeNodeObject>list)
        {
            int y = 0;
            if (list[list.Count - 1].childCount > 0 && list[list.Count-1].child[0].isActiveAndEnabled)
                y = getLowestY(list[list.Count - 1].child);
            else
            {
                Vector3[] corners = new Vector3[4];
                list[list.Count - 1].gameObject.GetComponent<RectTransform>().GetWorldCorners(corners);
                y = (int)corners[0].y;
            }
            return y;
        }

        /// <summary>
        /// handles displaying the insertLine and setting dropPosition
        /// </summary>
        private void drag()
        {
            // get the hoverd TreeNodeObject
            TreeNodeWrapper tu = getHoveredTreeNodeObject();
            TreeNodeObject tno = tu.treeNode;
            TreeNodeObject followTno = null;
            TreeNodeObject prevTno = null;
            int position = tu.listIndex;

            if (tno == null) return;

            // check if hovered tno is the dragged object
            if (tno == draggedObject)
            {
                dropPosition = position;
                if (insertLine != null)
                {
                    VectorLine.Destroy(ref insertLine);
                    insertLine = null;
                }
                return;
            }

            //check if drag is legal
            if (draggedObject.parent == null)
            {
                if (!treeNodeCtrl.m_TreeNodeObject.Contains(tno)) return;
            }
            else
            {
                if (!draggedObject.parent.child.Contains(tno)) return;
            }

            // get TreeNodeObjects above and below the hovered one
            if (tu.list.Count != position + 1)
                followTno = tu.list[position + 1];
            if (position > 0)
                prevTno = tu.list[position - 1];

            Vector3[] corners = new Vector3[4]; //corners of area which contains the nodes
            treeNodeCtrl.m_ObjectRoot.GetComponent<RectTransform>().GetWorldCorners(corners);

            Vector3[] corners2 = new Vector3[4]; //corners of the hovered node
            tno.m_TransSelect.GetWorldCorners(corners2);

            Vector3[] corners3 = new Vector3[4]; //corners of the node below (used for lower half)

            // calculated lineY
            int lineY = 0;
            bool below = false;
            // hovering lower half of a node (line should go below)
            if (new Rect(corners2[0].x, corners2[0].y, corners2[2].x - corners2[0].x, (corners2[2].y - corners2[0].y) / 2).Contains(Input.mousePosition))
            {
                if (followTno != null)
                {
                    if (followTno == draggedObject) return;
                    followTno.m_TransSelect.GetWorldCorners(corners3);
                    lineY = (int)(corners3[1].y + spacing * scaleFactor / 2);
                }
                else
                {
                    lineY = (int)(getLowestY(tu.list) - spacing * scaleFactor / 2);
                }
                below = true;
            }
            // hovering upper half of a node (line should go above)
            else if (new Rect(corners2[0].x, corners2[0].y + (corners2[2].y - corners2[0].y) / 2, corners2[2].x - corners2[0].x, (corners2[2].y - corners2[0].y) / 2).Contains(Input.mousePosition))
            {
                if (prevTno != null)
                    if (prevTno == draggedObject) return;
                lineY = (int)(corners2[1].y + spacing * scaleFactor / 2);
                below = false;
            }

            // calculate lineX values
            int lineLeftX = (int)corners[0].x + (int)(corners2[0].x - corners[0].x)/2;
            int lineRightX = (int)corners[2].x + (int)(corners2[0].x - corners[0].x) / 2;

            // return if lineY value is 0 or below the TreeView
            if (lineY == 0) return;

            Vector3[] corners4 = new Vector3[4];
            scrollRect.verticalScrollbar.GetComponent<RectTransform>().GetWorldCorners(corners4);
            if (lineY < (corners4[0].y - 1 - scaleFactor*spacing/2)) return;

            // display insertLine
            if (insertLine == null)
            {
                List<Vector2> linePoints = new List<Vector2>() { 
                    new Vector2(lineLeftX, lineY),
                    new Vector2(lineRightX, lineY) 
                };
                insertLine = new VectorLine("TreeNodeOrderingInsertLine", linePoints, 4f);
                insertLine.color = Color.yellow;
                insertLine.layer = 10;
                insertLine.Draw();
            }
            if (oldLineY == 0)
            {
                oldLineY = lineY;
                insertLine.points2[0] = new Vector2(lineLeftX, lineY);
                insertLine.points2[1] = new Vector2(lineRightX, lineY);
                insertLine.Draw();
            }
            if (oldLineY != lineY) 
            { 
                insertLine.points2[0] = new Vector2(lineRightX, lineY);
                insertLine.points2[1] = new Vector2(lineLeftX, lineY);
                insertLine.Draw();
                oldLineY = lineY;
            }

            // set drop position
            if (below) dropPosition = position + 1;
            else dropPosition = position;
        }

        /// <summary>
        /// handles dropping the draggedObject to the dropPosition in its list
        /// </summary>
        private void drop()
        {
            VectorLine.Destroy(ref insertLine);
            insertLine = null;

            // return if the new position is the same as the old
            if (dropPosition == draggedObjectPosition + 1 || dropPosition == draggedObjectPosition) return;

            if (draggedObject.parent == null)
            {
                List<TreeNodeObject> newNodes = reorderList(treeNodeCtrl.m_TreeNodeObject);
                treeNodeCtrl.m_TreeNodeObject = newNodes;
            }
            else
            {
                List<TreeNodeObject> newNodes = reorderList(draggedObject.parent.m_child);
                draggedObject.parent.m_child = newNodes;
                // ObjectInfo reordering
                ObjectInfo oi = objectInfoFromTreeNode(draggedObject.parent);
                if (oi != null)
                {
                    if (oi is OIItemInfo)
                        ((OIItemInfo)oi).child = reoderList(((OIItemInfo)oi).child);
                    else if (oi is OIFolderInfo)
                        ((OIFolderInfo)oi).child = reoderList(((OIFolderInfo)oi).child);
                }
                else if (draggedObjectChildDictionaryKey != -1 && draggedObjectParentCharacter != null)
                {
                    ((OICharInfo)draggedObjectParentCharacter.objectInfo).child[draggedObjectChildDictionaryKey] = reoderList(((OICharInfo)draggedObjectParentCharacter.objectInfo).child[draggedObjectChildDictionaryKey]);  
                }
            }

            treeNodeCtrl.RefreshHierachy();
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.joan6694.illusionplugins.kksus"))
                HSUS.Features.OptimizeNEO.WorkspaceCtrl_Awake_Patches._treeNodeList = treeNodeCtrl.m_TreeNodeObject;
        }

        /// <summary>
        /// reorders the TreeNodeObjects according to the dropPosition
        /// </summary>
        /// <param name="list">List of TreeNodeObjects to reorder</param>
        /// <returns>reorderd list</returns>
        private List<TreeNodeObject> reorderList(List<TreeNodeObject> list)
        {
            List<TreeNodeObject> newNodes = new List<TreeNodeObject>();
            bool dropAfter = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (dropPosition < draggedObjectPosition)
                {
                    if (i != draggedObjectPosition) newNodes.Add(list[i]);
                    else newNodes.Insert(dropPosition, draggedObject);
                }
                else if (dropPosition > draggedObjectPosition)
                {
                    if (i != draggedObjectPosition) newNodes.Add(list[i]);
                    else dropAfter = true;
                }
            }
            if (dropAfter) newNodes.Insert(dropPosition - 1, draggedObject);

            return newNodes;
        }

        /// <summary>
        /// reorders the ObjectInfos according to the dropPosition
        /// </summary>
        /// <param name="list">List of ObjectInfos to reorder</param>
        /// <returns>reorderd list</returns>
        private List<ObjectInfo> reoderList(List<ObjectInfo> list)
        {
            List<ObjectInfo> newNodes = new List<ObjectInfo>();
            bool dropAfter = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (dropPosition < draggedObjectPosition)
                {
                    if (i != draggedObjectPosition) newNodes.Add(list[i]);
                    else newNodes.Insert(dropPosition, objectInfoFromTreeNode(draggedObject));
                }
                else if (dropPosition > draggedObjectPosition)
                {
                    if (i != draggedObjectPosition) newNodes.Add(list[i]);
                    else dropAfter = true;
                }
            }
            if (dropAfter) newNodes.Insert(dropPosition - 1, objectInfoFromTreeNode(draggedObject));

            return newNodes;
        }

        /// <summary>
        /// get the ObjectInfo accosiated to a TreeNodeObject if any
        /// </summary>
        /// <param name="tno">TreeNodeObject</param>
        /// <returns>accosiated ObjectInfo</returns>
        public ObjectInfo objectInfoFromTreeNode(TreeNodeObject tno)
        {
            if (tno == null) return null;
            if (!Studio.Studio.Instance.dicInfo.ContainsKey(tno)) return null;
            return Studio.Studio.Instance.dicInfo[tno].objectInfo;
        }
    }
}
*/