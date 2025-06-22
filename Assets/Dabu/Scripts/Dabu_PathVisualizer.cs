using UnityEngine;
using System.Collections.Generic;

public class Dabu_PathVisualizerRuntime : MonoBehaviour
{
    public Material lineMaterial;
    private List<LineRenderer> lineRenderers = new();
    private Dabu_PuzzleManager manager;

    void Start()
    {
        manager = Dabu_PuzzleManager.Instance;
        if (manager == null) return;

        foreach (var segment in manager.mergedSegments)
        {
            GameObject lineGO = new GameObject("SegmentLine");
            lineGO.transform.SetParent(transform);

            LineRenderer lr = lineGO.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.material = lineMaterial;
            lr.startWidth = lr.endWidth = 0.05f;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;

            // ✅ 设置渲染图层，确保不被遮挡
            lr.sortingLayerName = "Default";     // 改成你希望用的 Sorting Layer 名字
            lr.sortingOrder = 10;                // 越大越靠前

            lineRenderers.Add(lr);
        }
    }

    void Update()
    {
        if (manager == null) return;

        manager.RefreshMergedNodePositions(); // 🔁 每帧刷新 node 世界位置

        for (int i = 0; i < lineRenderers.Count && i < manager.mergedSegments.Count; i++)
        {
            var segment = manager.mergedSegments[i];
            if (!segment.isActive)
            {
                lineRenderers[i].enabled = false;
                continue;
            }

            Vector2 start = manager.mergedNodePositions[segment.startNodeIndex];
            Vector2 end = manager.mergedNodePositions[segment.endNodeIndex];

            lineRenderers[i].enabled = true;
            lineRenderers[i].SetPosition(0, start);
            lineRenderers[i].SetPosition(1, end);
        }
    }
    
    
}