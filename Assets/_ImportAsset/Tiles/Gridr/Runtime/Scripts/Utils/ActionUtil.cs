//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


using Gridr.Gameplay;

namespace Gridr.Utils
{
    public static class ActionUtil
    {
        public static T GetAction<T>(GridEntity entity) where T : GridAction
        {
            if (entity == null)
                return null;
            return (T)entity.FindAction(typeof(T));
        }

    }
}