//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;


namespace Gridr.Gameplay
{
    public abstract class GridDisplay : MonoBehaviour
    {
        public abstract void ActivateHighlight();
        public abstract void DeactivateHighlight();
        public abstract void ActivatePath();
        public abstract void DeactivatePath();
        public abstract void DeactivateAll();
        public abstract void ActivateInRange();
        public abstract void DeactivateInRange();

    }
}