//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using Gridr.Datastructures;
using UnityEngine;


namespace Gridr.Extensions
{
    public static class ArrayExtensions
    {
        ///<summary>
        /// Populates array with a value
        ///</summary>
        public static void Populate<T>(this T[] arr, T value ) 
        {
            for ( var i = 0; i < arr.Length; i++ )
            {
                arr[i] = value;
            }
        }
    }
    public static class ListExtensions
    {

        public static void ForEach<T>(this TiedList<T> list, Action<T> action) 
        {
            foreach (var v in list.collection)
            {
                action(v);
            }
        }
        
        public static void ForEach<T>(this IEnumerable<T> list, Action<T> action) 
        {
            foreach (var v in list)
            {
                action(v);
            }
        }


    }
    public static class StackExtensions
    {
        ///<summary>
        /// Checks if Stack is empty
        ///</summary>
        ///<returns>
        /// true if the stack does not contain any items
        ///</returns>
        public static bool IsEmpty<T>(this Stack<T> stack)
        {
            return stack.Count == 0;
        }
    }
    public static class QueueExtensions
    {
        public static bool IsEmpty<T>(this Queue<T> queue)
        {
            return queue.Count == 0;
        }

        public static void StepAll<T>(this Queue<T> queue, Action<T> action)
        {
            while (!queue.IsEmpty())
            {
                action(queue.Dequeue());
            }
        }
    }

    public static class GridPositionExtensions
    {
        public static bool InRange(this GridPosition gp, GridPosition other,  int threshold)
        {
            var difference = other - gp;
            return !IsSamePosition(gp, other) && 
                   Mathf.Abs(difference.position.x) < threshold &&
                   Mathf.Abs(difference.position.y) < threshold && 
                   Mathf.Abs(difference.position.z) < threshold;
        }
        
        public static bool InRangeHard(this GridPosition gp, GridPosition other,  int threshold)
        {
            var difference = other - gp;

            return !IsSamePosition(gp, other) &&
                   Mathf.Abs(difference.position.y) < threshold &&
                   Mathf.Abs(difference.position.z) < threshold &&
                   Mathf.Abs(difference.position.y) + MathF.Abs(difference.position.x) + Mathf.Abs(difference.position.z) < threshold;
        }

        public static bool IsSamePosition(this GridPosition gp, GridPosition other)
        {
            var difference = gp - other;
            return difference.position.x == 0 && difference.position.y == 0 && difference.position.z == 0;
        }
    }
    
}