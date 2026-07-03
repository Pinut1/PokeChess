using UnityEngine;

namespace Urpixel
{
    public class UrpixelLocalSnap : UrpixelSnap
    {
        [SerializeField] private Vector3 localPosition;
        
        protected override void Update()
        {
            transform.localPosition = localPosition;
            base.Update();
        }
    }
}