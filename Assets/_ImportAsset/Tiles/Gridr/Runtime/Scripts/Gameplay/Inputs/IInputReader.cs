//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;


namespace Gridr.Gameplay
{
    public interface IInputReader
    {
        bool Primary();
        bool Secondary();
        Vector3 GetPointerWorldPositionOnPlane();
        Vector3 GetPointerScreenPosition();
    }
}