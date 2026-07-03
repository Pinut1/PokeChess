//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Class to help navigate results from selection.
    ///</summary>
    public class SelectionFilter<T> : IFilter<T>
    {
        public delegate T Listen();
        public delegate IEnumerable<T> ListenAll();
        public delegate void HandleCell(Cell cell);
        public delegate void HandleEntity(GridEntity entity);
        public delegate void HandleNull();
        public delegate void HandleButton(GridButton button);
        
        private readonly Listen _listen;
        private readonly HandleCell _handleCell;
        private readonly ListenAll _listenAll;
        private readonly HandleEntity _handleEntity;
        private readonly HandleNull _handleNull;
        private readonly HandleButton _handleButton;
        
        public SelectionFilter(Listen listen, ListenAll listenAll, HandleNull handleNull, HandleEntity handleEntity = null, HandleCell handleCell = null, HandleButton handleButton = null)
        {
            _handleNull = handleNull;
            _handleEntity = handleEntity;
            _handleCell = handleCell;
            _handleButton = handleButton;
            _listen = listen;
        }

        public void Filter(bool condition, Func<IEnumerable<T>, T> prioritize)
        {
            if (!condition)
                return;
            
            FilterSelection(prioritize(_listenAll()));
        }
        public void Filter(T selectable)
        {
            FilterSelection(selectable);
        }

        public void Filter(bool condition)
        {
            if (!condition)
                return;
            
            FilterSelection(_listen());
        }

        public void Filter()
        {
            FilterSelection(_listen());
        }

        private void FilterSelection(T selectable)
        {
            switch (selectable)
            {
                case Cell cell:
                    _handleCell?.Invoke(cell);
                    break;
                case GridEntity gridEntity:
                    _handleEntity?.Invoke(gridEntity);
                    break;
                case GridButton gridButton:
                    _handleButton?.Invoke(gridButton);
                    break;
                case null:
                    _handleNull.Invoke();
                    break;
                default:
                    _handleNull.Invoke();
                    break;
            }
        }
    }
}