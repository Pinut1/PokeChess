//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    public class FaceCamera : MonoBehaviour
    {
        private Transform _cameraTransform;
        private Quaternion _rotation;

        private void Start()
        {
            if (Camera.main is { })
                _cameraTransform = Camera.main.transform;

            _rotation = _cameraTransform.rotation;
        }


        private void OnValidate()
        {
            if (Camera.main is null) 
                return;
            
            _cameraTransform = Camera.main.transform;
            HandleRotation();
        }
        

        private void LateUpdate()
        {
            HandleRotation();
        }
        

        private void HandleRotation()
        {
            _rotation = _cameraTransform.rotation;
            gameObject.transform.LookAt(transform.position + _rotation * Vector3.forward,
                _rotation * Vector3.up);
        }
    }
}