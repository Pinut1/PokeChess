//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Utils;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Input reader implementation using the UnityEngine.Input mouse interface
    ///</summary>
    public class MouseInputReader : IInputReader
    {
        private readonly Camera _camera;
        private readonly Vector3 _normal;
        
        public MouseInputReader(Camera camera, Vector3 normal)
        {
            _camera = camera;
            _normal = normal;
        }

        public bool Primary()
        {
            return Input.GetMouseButtonDown(0);
        }

        public bool Secondary()
        {
            return Input.GetMouseButtonDown(1);
        }

        public Vector3 GetPointerWorldPositionOnPlane()
        {
            return MouseUtil.GetMouseRayPointOnPlane(_camera, _normal);
        }

        public Vector3 GetPointerScreenPosition()
        {
            return Input.mousePosition;
        }
    }
}