using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class Dabu_PathNode
{
    public Vector2 position;
    public List<int> connectedSegmentIndices = new();
}

[System.Serializable]
public class Dabu_PathSegment
{
    public int startNodeIndex;
    public int endNodeIndex;
    public bool isBidirectional = true;
    public bool isActive = true;
}

public class Dabu_PuzzleGraph : MonoBehaviour
{
    [Header("路径节点")]
    public List<Dabu_PathNode> nodes = new();

    [Header("路径线段")]
    public List<Dabu_PathSegment> segments = new();

    private void OnDrawGizmos()
    {
        if (nodes == null || segments == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 worldPos = transform.TransformPoint(nodes[i].position);
            Gizmos.DrawSphere(worldPos, 0.08f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(worldPos + Vector3.up * 0.1f, $"N{i}");
#endif
        }

        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            if (s.startNodeIndex >= nodes.Count || s.endNodeIndex >= nodes.Count) continue;

            Vector3 a = transform.TransformPoint(nodes[s.startNodeIndex].position);
            Vector3 b = transform.TransformPoint(nodes[s.endNodeIndex].position);

            Gizmos.color = s.isActive ? Color.yellow : Color.gray;
            Gizmos.DrawLine(a, b);

#if UNITY_EDITOR
            Vector3 mid = (a + b) / 2f;
            UnityEditor.Handles.Label(mid + Vector3.up * 0.1f, $"S{i}");
#endif
        }
    }
}