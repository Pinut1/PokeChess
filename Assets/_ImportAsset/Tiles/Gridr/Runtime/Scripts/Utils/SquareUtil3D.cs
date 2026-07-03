//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Datastructures;
using UnityEngine;

namespace Gridr.Utils
{
    public static class SquareUtil3D
    {
        public static GridPosition GetGridPosition(Vector3 pos, Vector3 origin, float cellSize, int width)
        {
            var position = GetCellFromWorldPos(pos, origin, cellSize);
            var index = GetIndex(position, width);
            return new GridPosition(position.x, position.y, 0, index);
        }

        public static Vector2Int GetCellFromWorldPos(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var x = Mathf.FloorToInt((worldPosition - origin).x / cellSize);
            var z = Mathf.FloorToInt((worldPosition - origin).z / cellSize);
            return new Vector2Int(x, z);
        }

        public static Vector3 GetWorldPosFromCellPos(int x, int z, Vector3 origin, float cellSize)
        {
            return new Vector3(x, 0, z) * cellSize + origin + new Vector3(cellSize * .5f, 0, cellSize * .5f);
        }

        public static Vector3 GetCellCenterFromWorldPos(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var cellCenter = new Vector3(gridCellPosition.x, 0, gridCellPosition.y) * cellSize + origin + new Vector3(cellSize * .5f, 0, cellSize * .5f);

            return cellCenter;
        }

        private static Vector3 GetCellCorner1(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var cellCenter = new Vector3(gridCellPosition.x, 0, gridCellPosition.y) * cellSize + origin;
            return cellCenter;
        }

        private static Vector3 GetCellCorner2(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var cellCenter = new Vector3(gridCellPosition.x, 0, gridCellPosition.y) * cellSize + origin + new Vector3(0, 0, cellSize);
            return cellCenter;
        }
        
        private static Vector3 GetCellCorner3(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var cellCenter = new Vector3(gridCellPosition.x, 0, gridCellPosition.y) * cellSize + origin + new Vector3(cellSize, 0, cellSize);
            return cellCenter;
        }
        
        private static Vector3 GetCellCorner4(Vector3 worldPosition, Vector3 origin, float cellSize)
        {
            var gridCellPosition = GetCellFromWorldPos(worldPosition, origin, cellSize);
            var cellCenter = new Vector3(gridCellPosition.x, 0, gridCellPosition.y) * cellSize + origin + new Vector3(cellSize, 0, 0);
            return cellCenter;
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
        
        public static bool IsWithinGrid(Vector3 position, int xSize, int zSize, float gridCellSize)
        {
            if (position.x > gridCellSize * xSize || position.x < 0)
                return false;
            if (position.z > gridCellSize * zSize || position.z < 0)
                return false;
            return true;
        }

        public static int GetIndex(Vector2Int position, int gridWidth)
        {
            return position.y * gridWidth + position.x;
        }
    }
}