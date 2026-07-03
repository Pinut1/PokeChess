using System;
using System.Collections.Generic;

//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Datastructures
{
    ///<summary>
    /// A linked list sorted using IComparable
    ///</summary>
    public class SortedLinkedList<T> : LinkedList<T> where T:IComparable
    {
        public void Add(T item)
        {
            if (First == null)
            {
                AddFirst(new LinkedListNode<T>(item));
            }
            else if (First.Value.CompareTo(item) >= 0)
            {
                AddFirst(new LinkedListNode<T>(item));
            }
            else
            {
                LinkedListNode<T> previousNode = null;
                LinkedListNode<T> currentNode = First;
                while (currentNode != null &&
                       currentNode.Value.CompareTo(item) < 0)
                {
                    previousNode = currentNode;
                    currentNode = currentNode.Next;
                }

                if (previousNode == null)
                    return;
            
                AddAfter(previousNode, new LinkedListNode<T>(item));
            }
        }
    }
}
