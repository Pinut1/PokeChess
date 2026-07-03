//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


namespace Gridr.Editor.Utils
{
    public static class GridVisUtil
    {
        public static int SquareGridWidth => SquareBuilderValues.Instance.gridWidth;
        public static int SquareGridHeight => SquareBuilderValues.Instance.gridHeight;
        public static float SquareCellSize => SquareBuilderValues.Instance.cellSize;

        public static int HexGridWidth => HexBuilderValues.Instance.gridWidth;
        public static int HexGridHeight => HexBuilderValues.Instance.gridHeight;
        public static float HexOuterRadius => HexBuilderValues.Instance.cellSize;

        public static Vector3 Origin => SquareBuilderValues.Instance.origin;
        
        public static float GridLineWeight => GridBuilderSettings.Instance.gridLineWeight;
        public static float EdgeLineWeight => GridBuilderSettings.Instance.edgeLineWeight;


        public static void DrawSquareGrid(ViewType viewType)
        {
            if(!GridBuilderSettings.Instance.showGrid)
                return;

            switch (viewType)
            {
                case ViewType.View2D:
                    Draw2DGrid(Origin ,SquareGridWidth, SquareGridHeight, SquareCellSize, GridLineWeight);
                    break;
                case ViewType.View3D:
                    Draw3DGrid(Origin ,SquareGridWidth, SquareGridHeight, SquareCellSize, GridLineWeight);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(viewType), viewType, null);
            }
        }
        public static void Draw3DGrid(Vector3 origin, int gridWidth, int gridHeight, float cellSize, float lineWeight = 1)
        {
            SetDrawLessEqual();
            
            for (int i = 0; i < gridWidth + 1; i++)
            {
                var lineOrigin = i * cellSize;
                var lineLength = cellSize * gridHeight;

                Vector3 p0 = new Vector3(lineOrigin, 0, 0) + origin;
                Vector3 p1 = new Vector3(lineOrigin, 0, lineLength) +origin;

                Handles.DrawAAPolyLine(lineWeight,p0, p1);
            }

            for (int i = 0; i < gridHeight + 1; i++)
            {
                var lineOrigin = i * cellSize;
                var lineLength = cellSize * gridWidth;

                Vector3 p0 = new Vector3(0, 0, lineOrigin) +origin;
                Vector3 p1 = new Vector3(lineLength, 0, lineOrigin) +origin;

                Handles.DrawAAPolyLine(lineWeight,p0, p1);
            }
        }
        public static void Draw2DGrid(Vector3 origin ,int gridWidth, int gridHeight, float cellSize, float lineWeight = 1)
        {
            SetDrawLessEqual();
            
            for (int i = 0; i < gridWidth + 1; i++)
            {
                var lineOrigin = i * cellSize;
                var lineLength = cellSize * gridHeight;

                Vector3 p0 = new Vector3(lineOrigin, 0, 0) +origin;
                Vector3 p1 = new Vector3(lineOrigin, lineLength, 0) +origin;

                Handles.DrawAAPolyLine(lineWeight, p0, p1);
            }

            for (int i = 0; i < gridHeight + 1; i++)
            {
                var lineOrigin = i * cellSize;
                var lineLength = cellSize * gridWidth;

                Vector3 p0 = new Vector3(0, lineOrigin, 0) +origin;
                Vector3 p1 = new Vector3(lineLength, lineOrigin, 0) +origin;

                Handles.DrawAAPolyLine(lineWeight,p0, p1);
            }
        }
        public static void DrawHexGrid(ViewType viewType, Vector3 origin)
        {
            if(viewType == ViewType.View2D)
               Draw2DHexGrid(HexGridWidth, HexGridHeight, origin, HexUtil2D.GetInnerRadius(HexOuterRadius), HexOuterRadius);
            else
                Draw3DHexGrid(HexGridWidth, HexGridHeight, origin, HexUtil3D.GetInnerRadius(HexOuterRadius), HexOuterRadius);
        }
        public static void Draw3DHexGrid(int gridWidth, int gridHeight, Vector3 origin, float innerRadius, float outerRadius)
        {
            for (var x = 0; x < gridWidth; x++)
            {
                for (var z = 0; z < gridHeight; z++)
                {
                    DrawHex3D(x, z, origin, innerRadius, outerRadius);
                }
            }
        }
        public static void Draw2DHexGrid(int gridWidth, int gridHeight, Vector3 origin, float innerRadius, float outerRadius)
        {
            for (var x = 0; x < gridWidth; x++)
            {
                for (var z = 0; z < gridHeight; z++)
                {
                    DrawHex2D(x, z, origin, innerRadius, outerRadius);
                }
            }
        }
        
        private static void DrawHex3D(int xIndex, int zIndex, Vector3 origin, float innerRadius, float outerRadius)
        {
            Handles.zTest = CompareFunction.LessEqual;

            Vector3 hexPosition;
            if (zIndex % 2 == 0)
                hexPosition =
                    HexUtil3D.GetWorldPositionEven(new Vector3(xIndex, 0, zIndex) + origin, innerRadius, outerRadius);
            else
                hexPosition =
                    HexUtil3D.GetWorldPositionOdd(new Vector3(xIndex, 0, zIndex) + origin, innerRadius, outerRadius);

            var hexCorners = HexUtil3D.GetCorners(hexPosition, innerRadius, outerRadius);
            DrawClosedLines(hexCorners);
        }
        private static void DrawHex2D(int xIndex, int yIndex, Vector3 origin, float innerRadius, float outerRadius)
        {
            Handles.zTest = CompareFunction.LessEqual;

            Vector3 hexPosition;
            if (yIndex % 2 == 0)
                hexPosition =
                    HexUtil2D.GetWorldPositionEven(new Vector3(xIndex, yIndex, 0) + origin, innerRadius, outerRadius);
            else
                hexPosition =
                    HexUtil2D.GetWorldPositionOdd(new Vector3(xIndex, yIndex, 0) + origin, innerRadius, outerRadius);

            var hexCorners = HexUtil2D.GetCorners(hexPosition, innerRadius, outerRadius);
            DrawClosedLines(hexCorners);
        }
        

        public static void DrawHex3D(Vector3Int cubeCoords, Vector3 origin, float outerRadius)
        {
            Vector3 hexPosition;
            var innerRadius = HexUtil3D.GetInnerRadius(outerRadius);
            var offsetCoords = HexUtil3D.CubeCoordsToOffset(cubeCoords);
            
            if (offsetCoords.z % 2 == 0)
                hexPosition = HexUtil3D.GetWorldPositionEven(offsetCoords + origin, innerRadius, outerRadius);
            else
                hexPosition = HexUtil3D.GetWorldPositionOdd(offsetCoords + origin, innerRadius, outerRadius);

            var corners = HexUtil3D.GetCorners(hexPosition, innerRadius, outerRadius);
            
            DrawClosedLines(corners);
        }
        public static void DrawHex2D(Vector3Int cubeCoords, Vector3 origin, float outerRadius)
        {
            Vector3 hexPosition;
            var innerRadius = HexUtil2D.GetInnerRadius(outerRadius);
            var offsetCoords = HexUtil2D.CubeCoordsToOffset(cubeCoords);
            
            if (offsetCoords.y % 2 == 0)
                hexPosition = HexUtil2D.GetWorldPositionEven(offsetCoords + origin, innerRadius, outerRadius);
            else
                hexPosition = HexUtil2D.GetWorldPositionOdd(offsetCoords + origin, innerRadius, outerRadius);

            var corners = HexUtil2D.GetCorners(hexPosition, innerRadius, outerRadius);
            
            DrawClosedLines(corners);
        }
        public static void DrawHex(Vector3Int cubeCoords, Vector3 origin, float outerRadius ,ViewType viewType)
        {
            SetColor(Color.green);
            if(viewType == ViewType.View2D)
                DrawHex2D(cubeCoords, origin, outerRadius);
            else
                DrawHex3D(cubeCoords, origin, outerRadius);
            SetColorToDefault();
        }
        
        private static void DrawClosedLines(Vector3[] corners)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                if (i == 0)
                    Handles.DrawAAPolyLine(GridLineWeight ,corners[i], corners[corners.Length - 1]);
                else
                    Handles.DrawAAPolyLine(GridLineWeight,corners[i - 1], corners[i]);
            }

        }
        public static void DrawDottedSquareOnGrid3D(Vector2Int cell, Vector3 origin , float cellSize)
        {
            var cellCenter = SquareUtil3D.GetWorldPosFromCellPos(cell.x, cell.y, origin, cellSize);
            var lineSegments = SquareUtil3D.GetCellLineSegments(cellCenter, origin, cellSize);

            Handles.DrawDottedLines(lineSegments, 2);
        }
        public static void DrawSquareOnGrid3D(Vector2Int cell, Vector3 origin, float cellSize)
        {
            var cellCenter = SquareUtil3D.GetWorldPosFromCellPos(cell.x, cell.y, origin, cellSize);
            var cellCorners = SquareUtil3D.GetCellLineSegments(cellCenter, origin, cellSize);
            
            Handles.DrawLines(cellCorners);
        }
        public static void DrawDottedSquareOnGrid2D(Vector2Int cell, Vector3 origin, float cellSize)
        {
            var cellCenter = SquareUtil2D.GetWorldPosFromCellPos(cell.x, cell.y, origin, cellSize);
            var lineSegments = SquareUtil2D.GetCellLineSegments(cellCenter, origin, cellSize);

            Handles.DrawDottedLines(lineSegments, 2);
        }
        public static void DrawSquareOnGrid2D(Vector2Int cell, Vector3 origin, float cellSize)
        {
            var cellCenter = SquareUtil2D.GetWorldPosFromCellPos(cell.x, cell.y, origin, cellSize);
            var lineSegments = SquareUtil2D.GetCellLineSegments(cellCenter, origin, cellSize);
            
            Handles.DrawLines(lineSegments);
        }
        public static void DrawPositions(IEnumerable<Cell> cells, Vector3 offset)
        {
            foreach (var cell in cells)
            {
                Handles.Label(cell.transform.position + offset, cell.gridPosition.ToString(), GUIUtil.GetDefaultTextStyle());
            }
        }
        public static void DrawIndices(IEnumerable<Cell> cells, Vector3 offset)
        {
            foreach (var cell in cells)
            {
                if(cell == null)
                    continue;
                Handles.Label(cell.transform.position + offset, cell.gridPosition.index.ToString(), GUIUtil.GetDefaultTextStyle());
            }
        }
        public static void DrawGraphEdges(Graph<Cell> graph, ViewType viewType)
        {
            if(!GridBuilderSettings.Instance.showGraphEdges)
                return;
            if (graph == null)
                return;
            if(!graph.Nodes.Any())
                return;

            foreach (var node in graph.Nodes)
            {
                if(node.Value == null)
                    continue;
                
                foreach (var neighbor in node.Edges)
                {
                    SetColor(Color.green);
                    Handles.DrawAAPolyLine(EdgeLineWeight, new []
                    {
                        node.Value.gameObject.transform.position + GridBuilderSettings.Instance.graphEdgesOffset,
                        neighbor.Value.gameObject.transform.position + GridBuilderSettings.Instance.graphEdgesOffset
                    });
                }
            }
            SetColorToDefault();
        }
        public static void DrawRing(Vector3 position, Vector3 normal, float radius)
        {
            Handles.DrawSolidDisc(position, normal, radius);
        }
        public static void DrawSolidDisc(Vector3 position, Vector3 normal, float radius)
        {
            Handles.DrawSolidDisc(position, normal, radius);
        }
        public static void SetColor(Color color)
        {
            Handles.color = color;
        }
        public static void SetColorToDefault()
        {
            Handles.color = Color.white;
        }
        public static void SetDrawAlways()
        {
            Handles.zTest = CompareFunction.Always;
        }
        public static void SetDrawBehind()
        {
            Handles.zTest = CompareFunction.LessEqual;
        }
        public static void SetDrawInFront()
        {
            Handles.zTest = CompareFunction.GreaterEqual;
        }
        public static void SetDrawLessEqual()
        {
            Handles.zTest = CompareFunction.LessEqual;

        }
        public static void SetSceneTo2D()
        {
            if (SceneView.sceneViews.Count == 0)
            {
                Debug.LogWarning("No open scenes views. Unable to set mode to 2D");
                return;
            }
            SceneView sceneView = SceneView.sceneViews[0] as SceneView;
            if (sceneView != null)
                sceneView.in2DMode = true;
        }
        public static void SetSceneTo3D()
        {
            if (SceneView.sceneViews.Count == 0)
            {
                Debug.LogWarning("No open scenes views. Unable to set mode to 3D");
                return;
            }
            SceneView sceneView = SceneView.sceneViews[0] as SceneView;
            if (sceneView != null)
                sceneView.in2DMode = false;
        }
        public static void SetSceneViewType(ViewType viewType)
        {
            if (viewType == ViewType.View2D)
            {
                SetSceneTo2D();
            }
            else
            {
                SetSceneTo3D();
            }
        }
        
    }
}