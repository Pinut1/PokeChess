//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


using System;

namespace Gridr.Pathfinding
{

    ///<summary>
    /// Object carrying data for Dijkstra Pathfinding
    ///</summary>
    public struct DijkstraTuple : IComparable
    {
        public int Index { get; }
        public float CostToNode { get; }

        public DijkstraTuple(int index, float costToNode)
        {
            Index = index;
            CostToNode = costToNode;
        }

        public int CompareTo(object obj)
        {
            return obj switch
            {
                null => 1,
                DijkstraTuple node when CostToNode < node.CostToNode => -1,
                DijkstraTuple node when Math.Abs(CostToNode - node.CostToNode) < float.Epsilon => 0,
                DijkstraTuple node => 1,
                _ => throw new ArgumentException("Unable to compare to object")
            };
        }
    }
}