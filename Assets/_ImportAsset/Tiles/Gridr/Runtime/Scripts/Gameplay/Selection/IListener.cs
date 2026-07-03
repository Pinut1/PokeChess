//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;

namespace Gridr.Gameplay
{
    public interface IListener<out T>
    {
        public T Listen();
        public IEnumerable<T> ListenAll();

    }
}