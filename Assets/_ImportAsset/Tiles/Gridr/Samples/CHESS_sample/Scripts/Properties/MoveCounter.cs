//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;

namespace Gridr.Chess
{
    public class MoveCounter : GridProperty
    {
        
        private int _count;

        public void Increment()
        {
            _count++;
        }

        public void Decrement()
        {
            _count--;
        }

        public void Decrement(GridEntity entity)
        {
            if (gameObject.GetComponent<GridEntity>() == entity)
                Decrement();
        }

        public int GetCount()
        {
            return _count;
        }
    }
}