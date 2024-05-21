using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TreeNodeOrdering
{
    internal class TreeNodeOrderingUI : MonoBehaviour
    {
        private Material mat = new Material(Shader.Find("Hidden/Internal-Colored"));


        public TreeNodeOrdering2.DropType CurrentDrawType;
        public float? RefY = null;
        public float LeftX = 3200;
        public float RightX = 3400;
        private float TopY { get => TreeNodeOrdering2.treeNodeCtrl.transform.Find("Viewport/Content").position.y; }
        private float NodeHeight { get => TreeNodeOrdering2.nodeHeight * TreeNodeOrdering2.scaleFactor; }
        private float NodeSpacing { get => TreeNodeOrdering2.spacing * TreeNodeOrdering2.scaleFactor; }

        private float DrawY
        {
            get
            {
                switch (CurrentDrawType)
                {
                    case TreeNodeOrdering2.DropType.InsertAbove:
                    case TreeNodeOrdering2.DropType.InsertAndParentAbove:
                        return TopY + (RefY ?? 0) * TreeNodeOrdering2.scaleFactor + NodeSpacing / 2;
                    case TreeNodeOrdering2.DropType.InsertBelow:
                    case TreeNodeOrdering2.DropType.InsertAndParentBelow:
                        return TopY + (RefY ?? 0) * TreeNodeOrdering2.scaleFactor - NodeSpacing / 2 - NodeHeight;
                    case TreeNodeOrdering2.DropType.Parent:
                        return TopY + (RefY ?? 0) * TreeNodeOrdering2.scaleFactor - NodeHeight / 2;
                    default:
                        return TopY;
                }
            }
        }

        void OnPostRender()
        {
            if (RefY.HasValue)
            {
                // init GL
                GL.PushMatrix();
                mat.SetPass(0);
                GL.LoadOrtho();

                switch (CurrentDrawType)
                {
                    case TreeNodeOrdering2.DropType.InsertAbove:
                    case TreeNodeOrdering2.DropType.InsertBelow:
                        DrawInsertLine(false);
                        break;
                    case TreeNodeOrdering2.DropType.InsertAndParentAbove:
                    case TreeNodeOrdering2.DropType.InsertAndParentBelow:
                        DrawInsertLine(true);
                        break;
                    case TreeNodeOrdering2.DropType.Parent:
                        DrawParentLine();
                        break;
                    default:
                        break;
                }

                // end GL
                GL.PopMatrix();
            }
        }

        private void DrawInsertLine(bool andParent)
        {
            GL.Begin(GL.QUADS);
            GL.Color(andParent ? Color.magenta : Color.yellow);

            GL.Vertex(t(new Vector2(LeftX - NodeSpacing, DrawY - NodeHeight / 2)));
            GL.Vertex(t(new Vector2(LeftX - NodeSpacing, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(LeftX, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(LeftX, DrawY - NodeHeight / 2)));

            GL.Vertex(t(new Vector2(LeftX, DrawY - NodeSpacing / 2)));
            GL.Vertex(t(new Vector2(LeftX, DrawY + NodeSpacing / 2)));
            GL.Vertex(t(new Vector2(RightX, DrawY + NodeSpacing / 2)));
            GL.Vertex(t(new Vector2(RightX, DrawY - NodeSpacing / 2)));

            GL.Vertex(t(new Vector2(RightX, DrawY - NodeHeight / 2)));
            GL.Vertex(t(new Vector2(RightX, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(RightX + NodeSpacing, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(RightX + NodeSpacing, DrawY - NodeHeight / 2)));

            GL.End();
        }

        private void DrawParentLine()
        {
            GL.Begin(GL.QUADS);
            GL.Color(Color.magenta);

            GL.Vertex(t(new Vector2(LeftX - NodeSpacing, DrawY - NodeHeight / 2)));
            GL.Vertex(t(new Vector2(LeftX - NodeSpacing, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(LeftX, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(LeftX, DrawY - NodeHeight / 2)));

            GL.Vertex(t(new Vector2(LeftX - NodeSpacing, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(LeftX - NodeSpacing, DrawY + NodeHeight / 2 + NodeSpacing)));
            GL.Vertex(t(new Vector2(RightX + NodeSpacing, DrawY + NodeHeight / 2 + NodeSpacing)));
            GL.Vertex(t(new Vector2(RightX + NodeSpacing, DrawY + NodeHeight / 2)));

            GL.Vertex(t(new Vector2(RightX, DrawY - NodeHeight / 2)));
            GL.Vertex(t(new Vector2(RightX, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(RightX + NodeSpacing, DrawY + NodeHeight / 2)));
            GL.Vertex(t(new Vector2(RightX + NodeSpacing, DrawY - NodeHeight / 2)));

            GL.Vertex(t(new Vector2(LeftX - NodeSpacing, DrawY - NodeHeight / 2 - NodeSpacing)));
            GL.Vertex(t(new Vector2(LeftX - NodeSpacing, DrawY - NodeHeight / 2)));
            GL.Vertex(t(new Vector2(RightX + NodeSpacing, DrawY - NodeHeight / 2)));
            GL.Vertex(t(new Vector2(RightX + NodeSpacing, DrawY - NodeHeight / 2 - NodeSpacing)));

            GL.End();
        }

        private Vector3 t(Vector2 screenCoords)
        {
            return new Vector3(screenCoords.x / Screen.width, screenCoords.y / Screen.height, 0);
        }
    }
}
