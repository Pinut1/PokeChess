//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using Gridr.Gameplay;
using UnityEngine;


namespace Gridr.Editor.Builder
{
    public class InstanceHandler
    {
        public int Count => allObjects.Count;

        public GameObject activeObject;
        public GameObject objectHiddenForPreview;

        public HashSet<GameObject> allObjects = new HashSet<GameObject>();

        public void SetActiveObject(GameObject go)
        {
            activeObject = go;
        }

        public void ClearActiveObject()
        {
            activeObject = null;
        }
        

        public void SetObjectHiddenForPreview(GameObject go)
        {
            objectHiddenForPreview = go;
            objectHiddenForPreview.SetActive(false);
        }

        public void ClearObjectHiddenForPreview()
        {
            objectHiddenForPreview = null;
        }

        public void AddToAllObjects(GameObject go)
        {
            if (go != null)
                allObjects.Add(go);
        }

        public void RemoveFromAllObjects(GameObject go)
        {
            if (allObjects.Contains(go))
                allObjects.Add(go);
        }

        public void SetAllObjectsToActive()
        {
            Refresh();
            foreach (var gameObject in allObjects)
            {
                gameObject.SetActive(true);
            }
        }

        public void Refresh()
        {
            allObjects.RemoveWhere(c => c == null);
        }

        public GameObject GetAt(Vector3 pos)
        {
            return allObjects.FirstOrDefault(go => go.transform.position == pos);
        }
        public GameObject GetAt(GridPosition gridPosition)
        {
            var allCells = allObjects.Select(c => c.GetComponent<Cell>());
            var getObject = allCells.FirstOrDefault(c => c.gridPosition == gridPosition);
            
            return getObject == null ? null : getObject.gameObject;
        }
        

        public GameObject GetAndRemoveAt(Vector3 pos)
        {
            var getObject = allObjects.FirstOrDefault(go => go.transform.position == pos);
            if (getObject == null)
                return null;

            RemoveFromAllObjects(getObject);
            return getObject;
        }
        
        public GameObject GetAndRemoveAt(GridPosition pos)
        {
            var allCells = allObjects.Select(c => c.GetComponent<Cell>());
            if (allCells.Any()) return null;
            {
                var getObject = allCells.FirstOrDefault(c => c.gridPosition == pos);
                if (getObject == null)
                    return null;

                RemoveFromAllObjects(getObject.gameObject);
                return getObject.gameObject;
            }

        }
        

        public void HandleHiddenObject(GridPosition gridPosition)
        {
            var instanceAtPosition = GetAt(gridPosition);

            if (objectHiddenForPreview != null)
                objectHiddenForPreview.SetActive(true);
            if (instanceAtPosition != null)
                SetObjectHiddenForPreview(instanceAtPosition);
        }
        
        public void HandleHiddenObject(Vector3 previewPosition)
        {
            var instanceAtPosition = GetAt(previewPosition);

            if (objectHiddenForPreview != null)
                objectHiddenForPreview.SetActive(true);
            if (instanceAtPosition != null)
                SetObjectHiddenForPreview(instanceAtPosition);
        }
    }
}