//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using UnityEngine;


namespace Gridr.Gameplay
{
    public abstract class GridButton : MonoBehaviour, ISelectable
    {
        [SerializeField] protected int selectionPriority = 0;
        public bool interactible = true;
        
        public Action onClick;
        public abstract State Select();
        public abstract void Deselect();
        public abstract int GetPriority();
        public void OnClick() => onClick?.Invoke();

    }
}