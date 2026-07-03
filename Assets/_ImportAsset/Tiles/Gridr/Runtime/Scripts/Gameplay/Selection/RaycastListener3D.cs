//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Listens for an object using raycast
    ///</summary>
    public class RaycastListener3D<T> : IListener<T>
    {
        private readonly Camera _camera;
        private readonly LayerMask _mask;
        private readonly IInputReader _inputReader;
        
        public RaycastListener3D(Camera camera, LayerMask mask, IInputReader inputReader)
        {
            _camera = camera;
            _mask = mask;
            _inputReader = inputReader;
        }

        public T Listen()
        {
            if (IsHitObject(out var selected))
                return selected;
            
            return IsHitObjectFallback(out var selectedFallback) ? selectedFallback : default;
        }

        public IEnumerable<T> ListenAll()
        {
            if (IsHitObjects(out var selected))
                return selected;

            return IsHitObjectFallback(out var selectedFallback) ? new List<T>() {selectedFallback} : null;
        }

        private bool IsHitObject(out T selected)
        {
            selected = default;
            
            if (_inputReader.Primary())
            {
                if (IsHitUI(out selected))
                    return true;
            }
            
            var ray = _camera.ScreenPointToRay(_inputReader.GetPointerScreenPosition());
            var result = Physics.Raycast(ray.origin, ray.direction, out var raycastHit, Mathf.Infinity, _mask);
            
            return result && raycastHit.collider.TryGetComponent<T>(out selected);
        }
        
        private bool IsHitObjects(out List<T> selected)
        {
            selected = default;

            if (_inputReader.Primary())
            {
                if (IsHitUIs(out selected))
                    return true;
            }

            var ray = _camera.ScreenPointToRay(_inputReader.GetPointerScreenPosition());
            var raycastResults = Physics.RaycastAll(ray.origin, ray.direction, Mathf.Infinity, _mask);

            if(raycastResults.Length > 0)
            {
                foreach(var rayCastHit in raycastResults)
                {
                    var tComponent = rayCastHit.collider.TryGetComponent<T>(out var selectedT);
                    if (tComponent)
                    {
                        selected ??= new List<T>();
                        selected.Add(selectedT);
                    }
                }
                
                return true;
            }
            
            return false;
        }
        
        private bool IsHitUIs(out List<T> selectedUis)
        {
            selectedUis = default;
            
            var pointerEventData = new PointerEventData(EventSystem.current){ position = _inputReader.GetPointerScreenPosition()};
            var raycastResults = new List<RaycastResult>();
            
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            if(raycastResults.Count > 0)
            {
                foreach(var uiHit in raycastResults)
                {
                    var tComponent = uiHit.gameObject.TryGetComponent<T>(out var selectedUi);
                    if (tComponent)
                    {
                        selectedUis ??= new List<T>();
                        selectedUis.Add(selectedUi);
                    }
                }
                return true;
            }

            selectedUis = default;
            return false;
        }

        private bool IsHitUI(out T selectedUi)
        {
            var pointerEventData = new PointerEventData(EventSystem.current){ position = _inputReader.GetPointerScreenPosition()};
            var raycastResults = new List<RaycastResult>();
            
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            if(raycastResults.Count > 0)
            {
                foreach(var uiHit in raycastResults)
                {
                    var tComponent = uiHit.gameObject.TryGetComponent<T>(out selectedUi);
                    if (tComponent)
                        return true;
                }
            }

            selectedUi = default;
            return false;
        }

        private bool IsHitObjectFallback(out T selected)
        {
            selected = default;
            
            if (EventSystem.current == null)
                return false;
            if (EventSystem.current.currentSelectedGameObject == null)
                return false;
            
            return EventSystem.current.currentSelectedGameObject.TryGetComponent<T>(out selected);
        }

    }
}