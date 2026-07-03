using System;
using System.Collections.Generic;
using System.Linq;

//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Datastructures
{
    ///<summary>
    /// Node object within a graph data structure
    ///</summary>
    public class GraphNode<T>
    {
        private readonly Dictionary<GraphNode<T>, float> _neighbors;

        ///<summary>
        /// Value associated with the node
        ///</summary>
        public T Value { get; }
        
        ///<summary>
        /// Graph index of this node
        ///</summary>
        public int Index { get; } = -1;
        
        ///<summary>
        /// Amount of edges from this node
        ///</summary>
        public int EdgesCount  => _neighbors.Count;
        
        ///<summary>
        /// Edges from this node
        ///</summary>
        public IList<GraphNode<T>> Edges => _neighbors.Keys.ToList().AsReadOnly();

        public GraphNode(T value)
        {
            Value = value;
            _neighbors = new Dictionary<GraphNode<T>, float>();
        }
        public GraphNode(T value, int index)
        {
            Value = value;
            _neighbors = new Dictionary<GraphNode<T>, float>();
            Index = index;
        }

        ///<summary>
        /// Adds an edge between this node and another node
        ///</summary>
        ///<param name="neighbor">
        /// Object to which to draw an edge
        ///</param>
        ///<param name="weight">
        /// Weight or cost of the edge
        ///</param>
        /// <returns>
        /// True if successful
        /// </returns>
        public bool AddEdge(GraphNode<T> neighbor, float weight)
        {
            if (_neighbors.ContainsKey(neighbor))
                return false;

            _neighbors.Add(neighbor, weight);
            return true;
        }

        ///<summary>
        /// Checks if an edge between this node and another node exists
        ///</summary>
        ///<param name="neighbor">
        /// Node to which to check if an edge exists
        ///</param>
        /// <returns>
        /// True if successful
        /// </returns>
        public bool HasEdge(GraphNode<T> neighbor)
        {
            if (!_neighbors.ContainsKey(neighbor))
                return false;
            
            return true;
        }

        ///<summary>
        /// Gets the weight of an edge between this node and another node
        ///</summary>
        ///<param name="neighbor">
        /// Node to which to check edge weight
        ///</param>
        /// <returns>
        /// Weight of the edge
        /// </returns>
        public float GetEdgeWeight(GraphNode<T> neighbor)
        {
            if (!_neighbors.ContainsKey(neighbor))
                throw new InvalidOperationException("Trying to retrieve weight of non-existent edge");
            
            return _neighbors[neighbor];
        }

        ///<summary>
        /// Removes edge between this node and another node if exists
        ///</summary>
        ///<param name="neighbor">
        /// Node to which edge to remove
        ///</param>
        /// <returns>
        /// True if successful
        /// </returns>
        public bool RemoveEdge(GraphNode<T> neighbor)
        {
            if (!_neighbors.ContainsKey(neighbor))
                return false;
            
            _neighbors.Remove(neighbor);
            return true;
        }

        ///<summary>
        /// Removes all edges from this node
        ///</summary>
        /// <returns>
        /// True if successful
        /// </returns>
        public bool RemoveAllNeighbors()
        {
            _neighbors.Clear();
            return true;
        }
    }
}