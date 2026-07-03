//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using Gridr.Editor.Utils;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEditor;
using UnityEngine;

namespace Gridr.Editor.Builder
{
    public static class BuilderUtil
    {

        public static void CleanPosition(Vector3 pos, InstanceHandler instanceHandler)
        {
            var objectToRemove = instanceHandler.GetAndRemoveAt(pos);
            
            if(objectToRemove != null)
                Undo.DestroyObjectImmediate(objectToRemove);
            
            instanceHandler.Refresh();
        }
        
        public static void CleanGridPosition(GridPosition gridPosition, InstanceHandler instanceHandler)
        {
            var objectToRemove = instanceHandler.GetAndRemoveAt(gridPosition);
            
            if(objectToRemove != null)
                Undo.DestroyObjectImmediate(objectToRemove);
            
            instanceHandler.Refresh();
        }
        
        public static void SetGridPositionsHex(InstanceHandler instanceHandler, Vector3 origin, float innerRadius, float outerRadius, int width, int height, bool is2d)
        {
            foreach (var cell in instanceHandler.allObjects)
            {
                GridPosition gridPosition;
                if(is2d)
                    gridPosition = HexUtil2D.GetGridPosition(origin, cell.transform.position, innerRadius, outerRadius, width, height);
                else
                    gridPosition = HexUtil3D.GetGridPosition(origin, cell.transform.position, innerRadius, outerRadius, width, height);

                var hexagon = cell.GetComponent<Cell>();
                if (hexagon == null)
                    continue;

                hexagon.gridPosition = gridPosition;
                SceneUtil.RecordChanges(hexagon);
            }
        }
        
        public static (int, int) GetApproxWidthAndHeightHex(IEnumerable<GameObject> allObjects)
        {
            int recordWidth = 0;
            int recordHeight = 0;
            foreach (var hex in allObjects)
            {
                var hexagon = hex.GetComponent<Cell>();
                if (hexagon.gridPosition.position.x > recordWidth)
                    recordWidth = hexagon.gridPosition.position.x;
                if (hexagon.gridPosition.position.z > recordHeight)
                    recordHeight = hexagon.gridPosition.position.z;
            }

            return (recordWidth + 1, recordHeight + 1);
        }
        
        
        public static (int, int) GetApproxWidthAndHeightSquare(IEnumerable<GameObject> allObjects)
        {
            if (allObjects == null || !allObjects.Any())
                return (1, 1);
            
            var recWidth = allObjects.Select(c => c.GetComponent<Cell>()).Max(c => c.gridPosition.position.x);
            var recHeight    = allObjects.Select(c => c.GetComponent<Cell>()).Max(c => c.gridPosition.position.y);

            return (recWidth + 1, recHeight + 1);
        }
        
    }
}