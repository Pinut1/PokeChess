//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Datastructures;
using UnityEngine;

namespace Gridr.Utils
{
 public static class SquareUtil2D
    {
        public static GridPosition GetGridPosition(Vector3 pos, Vector3 origin, float cellSize, int width)
        {
            var position = GetCellFromWorldPos(pos, origin, cellSize);
            var index = GetIndex(position, width);
            return new GridPosition(position.x, position.y, 0, index);
        }
        
        public static Vector3 GetWorldPosFromCellPos(int x, int y, Vector3 origin, float cellSize)
        {
            return new Vector3(x, y, 0) * cellSize + origin + new Vector3(cellSize * .5f, cellSize * .5f, 0);
        }
        
        public static Vector3 GetCellCenterFromWorldPos(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var cellCenter = new Vector3(gridCellPosition.x, gridCellPosition.y, 0) * cellSize + origin + new Vector3(cellSize * .5f, cellSize * .5f, 0);

            return cellCenter;
        }
        
        public static Vector2Int GetCellFromWorldPos(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var x = Mathf.FloorToInt((worldPosition - origin).x / cellSize);
            var y = Mathf.FloorToInt((worldPosition - origin).y / cellSize);
            return new Vector2Int(x, y);
        }
        
        private static Vector3 GetCellCorner1(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var corner = new Vector3(gridCellPosition.x, gridCellPosition.y, 0) * cellSize + origin;
            return corner;
        }
        
        private static Vector3 GetCellCorner2(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var corner = new Vector3(gridCellPosition.x, gridCellPosition.y, 0) * cellSize + origin + new Vector3(0, cellSize, 0);
            return corner;
        }
        
        private static Vector3 GetCellCorner3(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var corner = new Vector3(gridCellPosition.x, gridCellPosition.y, 0) * cellSize + origin + new Vector3(cellSize, cellSize, 0);
            return corner;
        }
        
        private static Vector3 GetCellCorner4(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var corner = new Vector3(gridCellPosition.x, gridCellPosition.y, 0) * cellSize + origin + new Vector3(cellSize, 0, 0);
            return corner;
        }
        
        public static Vector3[] GetCellCorners(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            return new Vector3[]
            {
                GetCellCorner1(worldPosition, origin, cellSize),
                GetCellCorner2(worldPosition, origin, cellSize),
                GetCellCorner3(worldPosition, origin, cellSize),
                GetCellCorner4(worldPosition, origin, cellSize)
            };
        }
        
        public static Vector3[] GetCellLineSegments(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            return new Vector3[]
            {
                GetCellCorner1(worldPosition, origin, cellSize),
                GetCellCorner2(worldPosition, origin, cellSize),
                GetCellCorner2(worldPosition, origin, cellSize),
                GetCellCorner3(worldPosition, origin, cellSize),
                GetCellCorner3(worldPosition, origin, cellSize),
                GetCellCorner4(worldPosition, origin, cellSize),
                GetCellCorner4(worldPosition, origin, cellSize),
                GetCellCorner1(worldPosition, origin, cellSize),
            };
        }
        
        public static bool IsWithinGrid(Vector3 position, int gridWidth, int gridHeight, float gridCellSize)
        {
            if (position.x > gridCellSize * gridWidth || position.x < 0)
                return false;
            if (position.y > gridCellSize * gridHeight || position.y < 0)
                return false;

            return true;
        }
        
        public static int GetIndex(Vector2Int position, int gridWidth)
        {
            return position.y * gridWidth + position.x;
        }
    }
}