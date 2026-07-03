//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


namespace Gridr.Gameplay
{
    ///<summary>
    /// Listens for an object using raycasts and result collider
    ///</summary>
    public class RaycastListener2D<T> : IListener<T>
    {
        
        private readonly Camera _camera;
        private readonly LayerMask _mask;
        private readonly IInputReader _inputReader;

        private readonly IListener<Vector3> _fallBackListener;
        private readonly Func<Vector3, T> _getSelectable;
        private readonly Func<Vector3, IEnumerable<T>> _getSelectables;
        
        
        public RaycastListener2D(Camera camera, LayerMask mask, IInputReader inputReader)
        {
            _camera = camera;
            _mask = mask;
            _inputReader = inputReader;
        }
        
        public RaycastListener2D(Camera camera, LayerMask mask, IInputReader inputReader, IListener<Vector3> fallBackListener, Func<Vector3, T> getSelectableStrategy)
        {
            _camera = camera;
            _mask = mask;
            _inputReader = inputReader;
            _fallBackListener = fallBackListener;
            _getSelectable = getSelectableStrategy;

            HandleCamera();
        }
        
        public RaycastListener2D(Camera camera, LayerMask mask, IInputReader inputReader, IListener<Vector3> fallBackListener, Func<Vector3, T> getSelectableStrategy, Func<Vector3, IEnumerable<T>> getSelectablesStrategy)
        {
            _camera = camera;
            _mask = mask;
            _inputReader = inputReader;
            _fallBackListener = fallBackListener;
            _getSelectable = getSelectableStrategy;
            _getSelectables = getSelectablesStrategy;

            HandleCamera();
        }

        private void HandleCamera()
        {
            if (!_camera.orthographic)
            {
                _camera.orthographic = true;
            }
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
            
            return IsHitObjectsFallback(out var selectedFallback) ? selectedFallback : default;
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
            var raycastResults = Physics2D.RaycastAll(ray.origin, ray.direction, Mathf.Infinity, _mask);

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

        private bool IsHitObject(out T selected)
        {
            selected = default;

            if (_inputReader.Primary())
            {
                if (IsHitUI(out selected))
                    return true;
            }

            var ray = _camera.ScreenPointToRay(_inputReader.GetPointerScreenPosition());
            var result = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, _mask);
            
            return result.collider != null && result.collider.TryGetComponent<T>(out selected);
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
        
        private bool IsHitUIs(out List<T> selectedUis)
        {
            var pointerEventData = new PointerEventData(EventSystem.current){ position = _inputReader.GetPointerScreenPosition()};
            var raycastResults = new List<RaycastResult>();
            selectedUis = new List<T>();
            
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            if(raycastResults.Count > 0)
            {
                foreach(var uiHit in raycastResults)
                {
                    var tComponent = uiHit.gameObject.TryGetComponent<T>(out var selectedUi);
                    if (tComponent)
                        selectedUis.Add(selectedUi);
                }
                return true;
            }

            selectedUis = default;
            return false;
        }

        private bool IsHitObjectFallback(out T selected)
        {
            selected = default;

            if (_getSelectable == null || _fallBackListener == null)
                return false;
            
            selected = _getSelectable(_fallBackListener.Listen());
            
            return selected != null;
        }
        
        private bool IsHitObjectsFallback(out IEnumerable<T> selected)
        {
            selected = default;

            if (_getSelectables == null || _fallBackListener == null)
                return false;
            
            selected = _getSelectables(_fallBackListener.Listen());
            
            return selected != null;
        }
    }

}