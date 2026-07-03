//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEditor;
using UnityEngine;


namespace Gridr.Editor
{
    public static class EditorMouseUtil
    {
        public static Vector3 GetMousePointOnPlane(Vector3 origin, Vector3 normal)
        {
            var mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var worldPlane = new Plane(normal, origin);
            if (worldPlane.Raycast(mouseRay, out float distance))
            {
                return mouseRay.GetPoint(distance);
            }

            return new Vector3(-1, -1, -1);
        }
    }
}