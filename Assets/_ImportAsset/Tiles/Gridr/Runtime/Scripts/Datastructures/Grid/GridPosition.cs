using System;
using UnityEngine;

//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Datastructures
{
    ///<summary>
    /// A position on the grid
    ///</summary>
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {

        ///<summary>
        /// Position on the grid measured in units of cells on corresponding axis.
        ///</summary>
        public Vector3Int position;
        ///<summary>
        /// The index of the position. 
        ///</summary>
        public int index;

        public GridPosition(int x, int y, int z, int index)
        {
            position = new Vector3Int(x, y, z);
            this.index = index;
        }

        public GridPosition(Vector3Int position, int index)
        {
            this.position = position;
            this.index = index;
        }

        public bool Equals(GridPosition other)
        {
            return other.position.x == position.x && other.position.y == position.y && other.position.z == position.z;
        }
        
        public static bool operator ==(GridPosition a, GridPosition b)
        {
            return a.Equals(b);
        }
        
        public static bool operator !=(GridPosition a, GridPosition b)
        {
            return !a.Equals(b);
        }
        public static GridPosition operator -(GridPosition a, GridPosition b)
        {
            return new GridPosition(a.position.x - b.position.x, a.position.y - b.position.y, a.position.z - b.position.z, a.index - b.index);
        }
        public static GridPosition operator +(GridPosition a, GridPosition b)
        {
            return new GridPosition(a.position.x + b.position.x, a.position.y + b.position.y, a.position.z + b.position.z, a.index + b.index);
        }


        public override string ToString()
        {
            return $"{position.x}, {position.y}, {position.z} : {index}";
        }
        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return (position.GetHashCode() * 397) ^ index;
            }
        }
    }
}