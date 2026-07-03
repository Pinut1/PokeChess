    using System;
    using UnityEngine;

    namespace Urpixel
    {
        [RequireComponent(typeof(Camera))]
        public class UrpixelCamera : UrpixelSnap
        {
            public static event Action OnChangeCamera = delegate { };

            public Transform Target;
            public Vector3 TargetOffset;
            public float TargetDistance;

            private Camera _cam;
            private Quaternion _rotation;
            private float _size;

            private void Start()
            {
                _rotation = transform.rotation;
                _cam = GetComponent<Camera>();
                SetupSnap();
                OnChangeCamera += SetupSnap;
            }

            private void OnDestroy() => OnChangeCamera -= SetupSnap;

            protected override void Update() { }

            protected void LateUpdate()
            {
                if (Target != null)
                    FocusTarget();

                base.Update();

                if (HasChanged())
                {
                    _rotation = transform.rotation;
                    _size = _cam.orthographicSize;
                    OnChangeCamera();
                }
            }


            private void FocusTarget()
            {
                Vector3 lookBackDir = transform.forward * -1;
                transform.position = Target.position + lookBackDir * TargetDistance + TargetOffset;
            }

            private bool HasChanged()
            {
                bool changedRotation = transform.rotation != _rotation;
                bool SameSize = Mathf.Approximately(_cam.orthographicSize, _size);

                return changedRotation || !SameSize;
            }
        }
    }