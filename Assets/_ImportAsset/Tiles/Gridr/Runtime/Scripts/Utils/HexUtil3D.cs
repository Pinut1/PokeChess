//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using Gridr.Datastructures;
using UnityEngine;

namespace Gridr.Utils
{
    public static class HexUtil3D
    {
        public static float GetInnerRadius(float outerRadius)
        {
            return (outerRadius * .5f) * (float)Math.Sqrt(3f);
        }

        public static int GetIndex(Vector3Int cubeCoords, int gridWidth, int gridHeight)
        {
            var offset = CubeCoordsToOffset(cubeCoords);
            var index = offset.z * gridWidth + offset.x;
            return index;
        }

        public static GridPosition GetGridPosition(Vector3 position, Vector3 origin, float innerRadius, float outerRadius, int gridWidth, int gridHeight)
        {
            var cubeCoords = CubeCoordsFromWorldPosition(origin, position, innerRadius, outerRadius);
            var index = GetIndex(cubeCoords, gridWidth, gridHeight);
            return new GridPosition(cubeCoords, index);
        }
        
        public static Vector3[] GetCorners(Vector3 origin, float innerRadius, float outerRadius)
        {
            var corners = new Vector3[]
            {
                new Vector3(0, 0, outerRadius) + origin,
                new Vector3(innerRadius, 0, .5f * outerRadius) + origin,
                new Vector3(innerRadius, 0, -.5f * outerRadius) + origin,
                new Vector3(0, 0, -outerRadius) + origin,
                new Vector3(-innerRadius, 0, -.5f * outerRadius) + origin,
                new Vector3(-innerRadius, 0, .5f * outerRadius) + origin
            };
            return corners;
        }

        public static Vector3 GetWorldPositionEven(Vector3 hexPosition, float innerRadius, float outerRadius)
        {
            float xPos = (hexPosition.x + hexPosition.z * .5f - hexPosition.z / 2) * (innerRadius * 2);
            float yPos = 0f;
            float zPos = hexPosition.z * (outerRadius * 1.5f);
            
            return new Vector3(xPos, yPos, zPos);
        }
        public static Vector3 GetWorldPositionOdd(Vector3 hexPosition, float innerRadius, float outerRadius)
        {
            float xPos = (hexPosition.x + hexPosition.z * .5f - hexPosition.z / 2) * (innerRadius * 2) + innerRadius;
            float yPos = 0f;
            float zPos = hexPosition.z * (outerRadius * 1.5f);
            
            return new Vector3(xPos, yPos, zPos);
        }
        
        public static Vector3Int CubeCoordsFromWorldPosition(Vector3 origin, Vector3 position, float innerRadius, float outerRadius)
        {
            float x = position.x / (innerRadius * 2f) - origin.x;
            float y = -x;

            float offset = position.z / (outerRadius * 3) - origin.z *.5f;
            x -= offset;
            y -= offset;

            int ix = Mathf.RoundToInt(x);
            int iy = Mathf.RoundToInt(y);
            int iz = Mathf.RoundToInt(-x-y);

            if (ix + iy + iz != 0)
            {
                float dx = Mathf.Abs(x - ix);
                float dy = Mathf.Abs(y - iy);
                float dz = Mathf.Abs(-x-y-iz);

                if (dx > dy && dx > dz)
                    ix = -iy - iz;
                else if (dz > dy)
                    iz = -ix - iy;
            }
            
            return new Vector3Int(ix, iy, iz);
        }

        public static Vector3Int CubeCoordsToOffset(Vector3Int cubeCoords)
        {
            var col = cubeCoords.x + (cubeCoords.z - (cubeCoords.z & 1)) / 2;
            var row = cubeCoords.z;
            return new Vector3Int(col, 0, row);
        }

        public static bool IsWithinOffsetGrid(Vector3Int cubeCoords, int width, int height)
        {
            var offsetCoords = CubeCoordsToOffset(cubeCoords);

            return offsetCoords.x >= 0 && offsetCoords.x < width && offsetCoords.z >= 0 && offsetCoords.z < height;
        }
        
        public static Vector3 CalculateHexCenterFromGridCoords(int x, int y, Vector3 origin, float innerRadius, float outerRadius)
        {
            Vector3 worldPosition;
            if (y % 2 == 0)
            {
                worldPosition = new Vector3() + origin + new Vector3(x * (innerRadius*2f), 0, y  * (outerRadius + (outerRadius/2f)));
            }
            else
            {
                worldPosition = new Vector3() + origin + new Vector3(x * (innerRadius*2f)+innerRadius, 0, y * (outerRadius + (outerRadius/2f)));
            }
            return worldPosition;
        }
    }
}