//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;

namespace Gridr.Datastructures
{
    public class GridSet<T>
    {
        public HashSet<T> set = new ();
        public Action<T> onAdded;
        public Action<T> onRemoved;

        public void Clear()
        {
            set.Clear();
        }
        
        public void Add(T item)
        {
            if(set.Add(item))
                onAdded?.Invoke(item);
        }

        public void Remove(T item)
        {
            if (!set.Contains(item))
                return;
            
            set.Remove(item);
            onRemoved?.Invoke(item);

        }
    }
}