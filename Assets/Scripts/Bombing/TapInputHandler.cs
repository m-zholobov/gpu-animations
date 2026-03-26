using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Profiling;

namespace Bombing
{
    public class TapInputHandler : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker = new("[AFewDC]TapInputHandler.Update");
        
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _hitMask;
        [SerializeField] private BombingManager _bombingManager;

        private InputAction _attackAction;

        private void OnEnable()
        {
            _attackAction = new InputAction("Attack", InputActionType.Button);
            _attackAction.AddBinding("<Mouse>/leftButton");
            _attackAction.AddBinding("<Touchscreen>/primaryTouch/tap");
            _attackAction.Enable();
        }

        private void OnDisable()
        {
            _attackAction?.Disable();
            _attackAction?.Dispose();
            _attackAction = null;
        }

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                if (!_attackAction.WasPressedThisFrame())
                    return;

                var pointer = Pointer.current;
                if (pointer == null)
                    return;

                var screenPos = pointer.position.ReadValue();
                var ray = _camera.ScreenPointToRay(screenPos);

                if (Physics.Raycast(ray, out var hit, Mathf.Infinity, _hitMask))
                    _bombingManager.RequestBomb(hit.point);
            }
        }
    }
}
