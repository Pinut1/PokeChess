using UnityEngine;

namespace Urpixel
{
    public class UrpixelSnap : MonoBehaviour
    {
        private static Camera _camera;
        private Vector3 _snappedPosition;
        private static Quaternion _toWorldCoordinates;
        private static Quaternion _toLocalCoordinates;
        private static float _pixelSize;
        
        protected static void SetupSnap()
        {
            _pixelSize = 2 * _camera.orthographicSize / Screen.height;
            _toLocalCoordinates = _camera.transform.rotation;
            _toWorldCoordinates = Quaternion.Inverse(_toLocalCoordinates);
        }
        
        protected virtual void OnEnable()
        {
            if (_camera == null)
                _camera = Camera.main;
        }
        
        protected virtual void Update()
        {
            _snappedPosition = _toWorldCoordinates * (transform.position);
            
            _snappedPosition.x = Mathf.Round(_snappedPosition.x / _pixelSize) * _pixelSize;
            _snappedPosition.y = Mathf.Round(_snappedPosition.y / _pixelSize) * _pixelSize;
            _snappedPosition.z = Mathf.Round(_snappedPosition.z / _pixelSize) * _pixelSize;
            
            transform.position = _toLocalCoordinates * _snappedPosition;
        }
    }
}