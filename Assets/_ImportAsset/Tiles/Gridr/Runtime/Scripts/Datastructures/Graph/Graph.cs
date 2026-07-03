using System.Collections.Generic;
using System.Linq;

//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Datastructures
{
    ///<summary>
    /// Implementation of a indexed graph data structure.
    ///</summary>
    public class Graph<T>
    {
        private readonly Dictionary<int, GraphNode<T>> _indexedNodes = new Dictionary<int, GraphNode<T>>();
        ///<summary>
        /// All the nodes within the graph
        ///</summary>
        public IList<GraphNode<T>> Nodes => _indexedNodes.Values.ToList().AsReadOnly();
        ///<summary>
        /// The amount of nodes within the graph
        ///</summary>
        public int Count => _indexedNodes.Count;
        ///<summary>
        /// The amount of edges within the graph
        ///</summary>
        public int EdgesCount => _indexedNodes.Values.Sum(node => node.EdgesCount);
        ///<summary>
        /// The indices of all nodes within the graph
        ///</summary>
        public int[] Indices => _indexedNodes.Keys.ToArray();
        
        ///<summary>
        /// Clears all nodes and neighbors from the graph
        ///</summary>
        public void Clear()
        {
            foreach (var graphNode in _indexedNodes.Values)
            {
                graphNode.RemoveAllNeighbors();
            }
            _indexedNodes.Clear();
        }
        ///<summary>
        /// Adds node to the graph
        ///</summary>
        ///<param name="value">
        /// Item to add as a graph node
        ///</param>
        ///<param name="index">
        /// Index of the node. Must be unique within the graph
        ///</param>
        /// <returns>
        /// True if successful
        /// </returns>
        public bool AddNode(T value, int index)
        {
            if (Find(index) != null)
            {
                return false;
            }

            var node = new GraphNode<T>(value, index);
            _indexedNodes.Add(index, node);
            return true;
        }
        ///<summary>
        /// Removes node from the graph if present
        ///</summary>
        ///<param name="index">
        /// Index of the node to be removed
        ///</param>
        /// <returns>
        /// True if successful
        /// </returns>
        public bool RemoveNode(int index)
        {
            var removeNode = Find(index);
            if (removeNode == null)
            {
                return false;
            }

            removeNode.RemoveAllNeighbors();
            foreach (var node in _indexedNodes.Values)
            {
                node.RemoveEdge(removeNode);
            }

            _indexedNodes.Remove(index);
            return true;
        }
        ///<summary>
        /// Removes an edge between two nodes
        ///</summary>
        ///<param name="index1">
        /// Index of the fist node
        ///</param>
        ///<param name="index2">
        /// Index of the second node
        ///</param>
        /// <returns>
        /// True if successful
        /// </returns>
        public bool RemoveEdge(int index1, int index2)
        {
            var node1 = Find(index1);
            var node2 = Find(index2);
            if (node1 == null || node2 == null)
            {
                return false;
            }

            if (!node1.Edges.Contains(node2))
            {
                return false;
            }

            node1.RemoveEdge(node2);
            node2.RemoveEdge(node1);
            return true;
        }
        ///<summary>
        /// Finds a node within the graph if present
        ///</summary>
        ///<param name="index">
        /// Index of the node being searched for
        ///</param>
        /// <returns>
        /// GraphNode with the specified index. Null if not present in the graph
        /// </returns>
        public GraphNode<T> Find(int index)
        {
            return !_indexedNodes.ContainsKey(index) ? null : _indexedNodes[index];
        }
        ///<summary>
        /// Finds a node within the graph if present
        ///</summary>
        ///<param name="node">
        /// Object being searched for
        ///</param>
        /// <returns>
        /// GraphNode equal to the specified node. Null if not present in the graph
        /// </returns>
        public GraphNode<T> Find(GraphNode<T> node)
        {
            return _indexedNodes.Values.FirstOrDefault(c => c == node);
        }
        ///<summary>
        /// Finds a collection of nodes from an IEnumerable of indices
        ///</summary>
        ///<param name="indices">
        /// IEnumerable of indices being searched for
        ///</param>
        /// <returns>
        /// GraphNodes with the specified indices. Null if not present in the graph
        /// </returns>
        public IEnumerable<GraphNode<T>> Find(IEnumerable<int> indices)
        {
            return indices.Select(Find);
        }
    }
}