using UnityEngine;

namespace CG.CombatFramework
{
    [DisallowMultipleComponent]
    public sealed class GroundPlaneAimProvider : MonoBehaviour, IAimProvider
    {
        [SerializeField, Tooltip("Will default to Camera.main")] private Camera viewCamera;
        [SerializeField, Tooltip("Muzzle or hand, will default to this transform")] private Transform origin;

        private Vector3 aimPoint;
        public Transform Muzzle => origin != null ? origin : transform;
        public Vector3 Origin => Muzzle.position;
        public Vector3 AimPoint => aimPoint;

        public Vector3 Direction
        {
            get
            {
                Vector3 dir = aimPoint - Origin;
                dir.y = 0f;
                return dir.sqrMagnitude > 0.0001f ? dir.normalized : Muzzle.forward;
            }
        }

        private void Start()
        {
            if(viewCamera == null)
            {
                viewCamera = Camera.main;
            }
        }

        public void SetScreenPoint(Vector2 screenPoint)
        {
            if(viewCamera == null) return;
            var plane = new Plane(Vector3.up, Origin);
            var ray = viewCamera.ScreenPointToRay(screenPoint);
            if(plane.Raycast(ray, out float enter)) aimPoint = ray.GetPoint(enter);
        }
        
    }
}