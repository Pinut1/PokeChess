using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Datastructures
{
    ///<summary>
    /// A list data structure that can be inspected using Next and Previous. Will loop around to the start or end of list when outside of bounds
    ///</summary>
    [Serializable]
    public class TiedList<T> : IEnumerable<T>
    {
        
        public List<T> collection = new List<T>();
        
        private int _currentIndex = 0;
        ///<returns>
        /// First item in the list
        ///</returns>
        public T First => collection[0];
        ///<returns>
        /// Last item in the list
        ///</returns>
        public T Last => collection[collection.Count - 1];
        ///<returns>
        /// Amount of items in the list
        ///</returns>
        public int Count => collection.Count;
        ///<returns>
        /// Currently inspected item of the list
        ///</returns>
        public T Current => collection[_currentIndex];
        ///<returns>
        /// True if the list does not contain any items 
        ///</returns>
        public bool Empty => !collection.Any();
        

        ///<summary>
        /// Adds item to the list
        ///</summary>
        public void Add(T item)
        {
            collection.Add(item);
        }

        ///<summary>
        /// Removes item from the list
        ///</summary>
        public void Remove(T item)
        {
            if (!collection.Contains(item))
                return;
            
            collection.Remove(item);
        }

        ///<summary>
        /// Sets internal counter to 0
        ///</summary>
        public void Restart()
        {
            _currentIndex = 0;
        }

        ///<summary>
        /// Resets the internal counter and inspects the first item in the list
        ///</summary>
        public T Start()
        {
            _currentIndex = 0;
            return Next();
        }
        
        ///<summary>
        /// Returns the next item to be inspected from the list
        ///</summary>
        public T Next()
        {
            if (Empty)
                return default;
            
            var atIndex = _currentIndex;
            _currentIndex = ++_currentIndex > Count-1 ? 0 : _currentIndex;
            
            return collection[atIndex];
        }

        public T PeekNext()
        {
            return Empty ? default : collection[_currentIndex];
        }

        ///<summary>
        /// Returns the previous item to be inspected from the list
        ///</summary>
        public T Previous()
        {
            if (Empty)
                return default;

            var atIndex = _currentIndex;
            _currentIndex = --_currentIndex < 0 ? Count - 1 : _currentIndex;
            
            return collection[atIndex];;        
        }

        public IEnumerator<T> GetEnumerator()
        {
            return collection.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void RemoveAll(Predicate<T> pred)
        {
            collection.RemoveAll(pred);
        }
        
    }
}