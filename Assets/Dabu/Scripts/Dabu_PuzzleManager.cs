using System.Collections.Generic;
using UnityEngine;

public class Dabu_PuzzleManager : MonoBehaviour
{
    public static Dabu_PuzzleManager Instance { get; private set; }

    private List<Dabu_PuzzleGraph> allPuzzles = new();
    public List<Vector2> mergedNodePositions = new();
    public List<Dabu_PathSegment> mergedSegments = new();
    public Dictionary<int, List<int>> nodeToSegments = new(); // node index -> list of segment indices

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterPuzzle(Dabu_PuzzleGraph puzzle)
    {
        if (!allPuzzles.Contains(puzzle))
        {
            allPuzzles.Add(puzzle);
            MergePuzzle(puzzle);
        }
    }

    private void MergePuzzle(Dabu_PuzzleGraph puzzle)
    {
        var transform = puzzle.transform;
        var localNodes = puzzle.nodes;

        // 映射 local node index → global merged index
        Dictionary<int, int> localToGlobalIndex = new();

        // 先处理所有节点
        for (int i = 0; i < localNodes.Count; i++)
        {
            Vector2 worldPos = transform.TransformPoint(localNodes[i].position);
            int globalIndex = FindOrAddNode(worldPos);
            localToGlobalIndex[i] = globalIndex;
        }

        // 再处理所有 segment
        foreach (var segment in puzzle.segments)
        {
            if (!localToGlobalIndex.ContainsKey(segment.startNodeIndex) || !localToGlobalIndex.ContainsKey(segment.endNodeIndex))
                continue;

            int globalA = localToGlobalIndex[segment.startNodeIndex];
            int globalB = localToGlobalIndex[segment.endNodeIndex];

            var newSegment = new Dabu_PathSegment
            {
                startNodeIndex = globalA,
                endNodeIndex = globalB,
                isBidirectional = segment.isBidirectional,
                isActive = segment.isActive
            };

            int segIndex = mergedSegments.Count;
            mergedSegments.Add(newSegment);

            // 正确地注册连接关系
            if (!nodeToSegments.ContainsKey(globalA)) nodeToSegments[globalA] = new List<int>();
            if (!nodeToSegments.ContainsKey(globalB)) nodeToSegments[globalB] = new List<int>();

            nodeToSegments[globalA].Add(segIndex);
            if (newSegment.isBidirectional)
                nodeToSegments[globalB].Add(segIndex);
        }
    }

    private int FindOrAddNode(Vector2 worldPos)
    {
        int existingIndex = FindExistingNodeIndex(worldPos);
        if (existingIndex != -1)
        {
            return existingIndex;
        }

        int newIndex = mergedNodePositions.Count;
        mergedNodePositions.Add(worldPos);
        nodeToSegments[newIndex] = new();
        return newIndex;
    }

    private int FindExistingNodeIndex(Vector2 pos)
    {
        for (int i = 0; i < mergedNodePositions.Count; i++)
        {
            if (Vector2.Distance(mergedNodePositions[i], pos) < 0.01f)
                return i;
        }
        return -1;
    }
    
    public void RebuildMergedGraph()
    {
        mergedNodePositions.Clear();
        mergedSegments.Clear();
        nodeToSegments.Clear();

        // 重新注册所有拼图
        foreach (var puzzle in FindObjectsOfType<Dabu_PuzzleGraph>())
        {
            MergePuzzle(puzzle);
        }
        
        // 处理所有拼图之间的连接
        ConnectAllPuzzles();
    }
    
    private void ConnectAllPuzzles()
    {
        var allPuzzleGraphs = FindObjectsOfType<Dabu_PuzzleGraph>();
        
        for (int i = 0; i < allPuzzleGraphs.Length; i++)
        {
            var puzzleA = allPuzzleGraphs[i];
            var transformA = puzzleA.transform;
            
            for (int j = i + 1; j < allPuzzleGraphs.Length; j++)
            {
                var puzzleB = allPuzzleGraphs[j];
                var transformB = puzzleB.transform;
                
                // 检查两个拼图之间的节点连接
                for (int nodeAIndex = 0; nodeAIndex < puzzleA.nodes.Count; nodeAIndex++)
                {
                    Vector2 worldPosA = transformA.TransformPoint(puzzleA.nodes[nodeAIndex].position);
                    
                    for (int nodeBIndex = 0; nodeBIndex < puzzleB.nodes.Count; nodeBIndex++)
                    {
                        Vector2 worldPosB = transformB.TransformPoint(puzzleB.nodes[nodeBIndex].position);
                        
                        if (Vector2.Distance(worldPosA, worldPosB) < 0.01f)
                        {
                            // 找到这两个节点在合并图中的索引
                            int indexA = FindExistingNodeIndex(worldPosA);
                            int indexB = FindExistingNodeIndex(worldPosB);
                            
                            if (indexA != -1 && indexB != -1 && indexA != indexB)
                            {
                                // 检查是否已经存在这个连接
                                bool connectionExists = false;
                                if (nodeToSegments.ContainsKey(indexA))
                                {
                                    foreach (int segIndex in nodeToSegments[indexA])
                                    {
                                        var segment = mergedSegments[segIndex];
                                        if ((segment.startNodeIndex == indexA && segment.endNodeIndex == indexB) ||
                                            (segment.startNodeIndex == indexB && segment.endNodeIndex == indexA))
                                        {
                                            connectionExists = true;
                                            break;
                                        }
                                    }
                                }
                                
                                if (!connectionExists)
                                {
                                    var segment = new Dabu_PathSegment
                                    {
                                        startNodeIndex = indexA,
                                        endNodeIndex = indexB,
                                        isBidirectional = true,
                                        isActive = true
                                    };
                                    int segIndex = mergedSegments.Count;
                                    mergedSegments.Add(segment);

                                    if (!nodeToSegments.ContainsKey(indexA)) nodeToSegments[indexA] = new List<int>();
                                    if (!nodeToSegments.ContainsKey(indexB)) nodeToSegments[indexB] = new List<int>();

                                    nodeToSegments[indexA].Add(segIndex);
                                    nodeToSegments[indexB].Add(segIndex);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
    public void RefreshMergedNodePositions()
    {
        // 重新计算所有节点的世界位置
        for (int i = 0; i < mergedNodePositions.Count; i++)
        {
            Vector2 nodeWorldPos = mergedNodePositions[i];
            
            // 找到这个节点属于哪个拼图
            foreach (var puzzle in FindObjectsOfType<Dabu_PuzzleGraph>())
            {
                var transform = puzzle.transform;
                for (int j = 0; j < puzzle.nodes.Count; j++)
                {
                    Vector2 localPos = puzzle.nodes[j].position;
                    Vector2 worldPos = transform.TransformPoint(localPos);
                    
                    if (Vector2.Distance(worldPos, nodeWorldPos) < 0.01f)
                    {
                        // 更新节点位置
                        mergedNodePositions[i] = worldPos;
                        break;
                    }
                }
            }
        }
    }
}
