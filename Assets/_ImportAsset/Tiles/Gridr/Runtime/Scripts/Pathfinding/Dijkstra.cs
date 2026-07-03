//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using Gridr.Extensions;


namespace Gridr.Pathfinding
{
    public class Dijkstra<T>
    {
        ///<summary>
        /// Calculates distance from a node in the graph to every other node
        ///</summary>
        ///<param name="graph">
        /// Graph of nodes
        ///</param>
        ///<param name="n">
        /// Size of the graph in number of nodes
        ///</param>
        ///<param name="startIndex">
        /// The index of the starting node
        ///</param>
        ///<returns>
        /// Array of values representing the distance to that node by index of the array
        ///</returns>
        public float[] GetDistances(Graph<T> graph, int n, int startIndex)
        {
            var visited = new bool[n];
            var distance = new float[n];
            
            visited.Populate(false);
            distance.Populate(int.MaxValue);

            distance[startIndex] = 0;
            var queue = new SortedLinkedList<DijkstraTuple>();
            queue.Add(new DijkstraTuple(startIndex, 0));

            while (queue.Count != 0)
            {
                var index = queue.First().Index;
                var minValue = queue.First().CostToNode;
                var currentNode = graph.Find(index);
                
                queue.RemoveFirst();

                visited[index] = true;
                if(distance[index] < minValue)
                    continue;
                
                foreach (var neighbor in currentNode.Edges)
                {
                    if (neighbor.Index < 0)
                        continue;
                    
                    if(!neighbor.HasEdge(currentNode))
                        continue;
                    
                    if(visited[neighbor.Index])
                        continue;
                    
                    var newDistance = distance[index] + neighbor.GetEdgeWeight(currentNode);
                    if (!(newDistance < distance[neighbor.Index])) 
                        continue;
                    
                    distance[neighbor.Index] = newDistance;
                    queue.Add(new DijkstraTuple(neighbor.Index, newDistance));
                }
            }
            return distance;
        }
        public float[] GetDistances(Graph<T> graph, int[] invalidNodes, int n, int startIndex)
        {
            var distances = GetDistances(graph, n, startIndex);
            foreach (var index in invalidNodes)
            {
                distances[index] = int.MaxValue;
            }
            return distances;
        }

        ///<summary>
        /// Calculates movementPaths from a node in the graph to every other node
        ///</summary>
        ///<param name="graph">
        /// Graph of nodes
        ///</param>
        ///<param name="n">
        /// Size of the graph in number of nodes
        ///</param>
        ///<param name="startIndex">
        /// The index of the starting node
        ///</param>
        ///<returns>
        ///</returns>
        public (float[], int[]) GetPaths(Graph<T> graph, int n, int startIndex)
        {
            var visited = new bool[n];
            var distance = new float[n];
            var previous = new int[n];
            
            visited.Populate(false);
            distance.Populate(int.MaxValue);
            previous.Populate(-1);

            distance[startIndex] = 0;
            var queue = new SortedLinkedList<DijkstraTuple>();
            queue.Add(new DijkstraTuple(startIndex, 0));
            
            while (queue.Count != 0)
            {
                var index = queue.First().Index;
                var minValue = queue.First().CostToNode;
                var currentNode = graph.Find(index);
                queue.RemoveFirst();

                visited[index] = true;
                if(distance[index] < minValue)
                    continue;
                
                foreach (var neighbor in currentNode.Edges)
                {
                    if (neighbor.Index < 0)
                    {
                        continue;
                    }
                    
                    if(!neighbor.HasEdge(currentNode))
                        continue;
                    
                    if(visited[neighbor.Index])
                        continue;
                    
                    var newDistance = distance[index] + neighbor.GetEdgeWeight(currentNode);
                    if (!(newDistance < distance[neighbor.Index])) 
                        continue;
                    
                    previous[neighbor.Index] = index;
                    distance[neighbor.Index] = newDistance;
                    queue.Add(new DijkstraTuple(neighbor.Index, newDistance));
                }
            }
            return (distance, previous);
        }
        
        ///<summary>
        /// Calculates movementPaths from a node in the graph to every other node
        ///</summary>
        ///<param name="graph">
        /// Graph of nodes
        ///</param>
        ///<param name="invalidNodes">
        /// Set of indices that will be set to max cost (not pathed)
        ///</param>
        ///<param name="n">
        /// Size of the graph in number of nodes
        ///</param>
        ///<param name="startIndex">
        /// The index of the starting node
        ///</param>
        ///<returns>
        ///</returns>
        public (float[], int[]) GetPaths(Graph<T> graph, int[] invalidNodes, int n, int startIndex)
        {
            var visited = new bool[n];
            var distance = new float[n];
            var previous = new int[n];
            
            visited.Populate(false);
            distance.Populate(int.MaxValue);
            previous.Populate(-1);

            distance[startIndex] = 0;
            var queue = new SortedLinkedList<DijkstraTuple>();
            queue.Add(new DijkstraTuple(startIndex, 0));
            
            foreach (var invalidNode in invalidNodes)
            {
                visited[invalidNode] = true;
            }
            
            while (queue.Count != 0)
            {
                var index = queue.First().Index;
                var minValue = queue.First().CostToNode;
                var currentNode = graph.Find(index);
                queue.RemoveFirst();

                visited[index] = true;
                if(distance[index] < minValue)
                    continue;
                
                foreach (var neighbor in currentNode.Edges)
                {
                    if (neighbor.Index < 0)
                    {
                        continue;
                    }
                    
                    if(!neighbor.HasEdge(currentNode))
                        continue;
                    
                    if(visited[neighbor.Index])
                        continue;
                    
                    var newDistance = distance[index] + neighbor.GetEdgeWeight(currentNode);
                    if (!(newDistance < distance[neighbor.Index])) 
                        continue;
                    
                    previous[neighbor.Index] = index;
                    distance[neighbor.Index] = newDistance;
                    queue.Add(new DijkstraTuple(neighbor.Index, newDistance));
                }
            }
            return (distance, previous);
        }

        
        ///<summary>
        /// Calculates movementPaths from a node in the graph to another node
        ///</summary>
        ///<param name="graph">
        /// Graph of nodes
        ///</param>
        ///<param name="n">
        /// Size of the graph in number of nodes
        ///</param>
        ///<param name="startIndex">
        /// The index of the starting node
        ///</param>
        ///<param name="endIndex">
        /// The index of the end node
        ///</param>
        ///<returns>
        /// List of node indices 
        ///</returns>
        public List<int> GetPath(Graph<T> graph, int n, int startIndex, int endIndex)
        {
            var paths = GetPaths(graph, n, startIndex);
            var path = new List<int>();
            if (Math.Abs(paths.Item1[endIndex] - int.MaxValue) < float.Epsilon)
                return path;

            for (var i = endIndex; i != -1; i = paths.Item2[i])
            {
                path.Add(i);
            }
            path.Reverse();
            return path;
        }

        ///<summary>
        /// Retrieves movementPaths from a path-tuple
        ///</summary>
        ///<param name="paths">
        /// Path tuple of available movementPaths
        ///</param>
        ///<param name="reverse">
        /// The index of the end node
        ///</param>
        ///<returns>
        /// List of node indices 
        ///</returns>
        public Func<int, List<int>> GetPathStrategy((float[], int[]) paths, bool reverse = true)
        {
            return (endIndex) =>
            {
                var path = new List<int>();
                if (Math.Abs(paths.Item1[endIndex] - int.MaxValue) < float.Epsilon)
                    return path;

                for (var i = endIndex; i != -1; i = paths.Item2[i])
                {
                    path.Add(i);
                }
                
                if(reverse)
                    path.Reverse();
                
                return path;
            };
        }
    }
}