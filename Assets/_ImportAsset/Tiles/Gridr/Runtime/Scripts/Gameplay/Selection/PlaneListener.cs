//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Listens for a Vector3 position using a raycast and a plane.
    ///</summary>
    public class PlaneListener : IListener<Vector3> 
    {
        private readonly Camera _camera;
        private readonly Vector3 _normal;
        private readonly Vector3 _inPoint;
        
        private readonly IInputReader _inputReader;
        
        public PlaneListener(Vector3 inPoint, Vector3 normal, Camera camera, IInputReader inputReader)
        {
            _inPoint = inPoint;
            _normal = normal;
            _camera = camera;
            _inputReader = inputReader;
        }

        public Vector3 Listen()
        {
            return GetPointerPositionOnPlane(out var selected) ? selected : default;
        }

        public IEnumerable<Vector3> ListenAll()
        {
            throw new System.NotImplementedException();
        }

        private bool GetPointerPositionOnPlane(out Vector3 selected)
        {
            selected = default;
            
            var ray = _camera.ScreenPointToRay(_inputReader.GetPointerScreenPosition());
            var worldPlane = new Plane(_normal, _inPoint);
            
            if (!worldPlane.Raycast(ray, out var distance)) 
                return false;

            
            selected = ray.GetPoint(distance);
            return true;
        }        
    }
}