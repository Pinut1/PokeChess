//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Utils
{
    public static class MouseUtil
    {
        public  static Vector3 GetMouseRayPointOnPlane(Camera camera ,Vector3 normal)
        {
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            var worldPlane = new Plane(normal, Vector3.zero);
            
            if (worldPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            return new Vector3(-1, -1, -1);
        }
    }
}