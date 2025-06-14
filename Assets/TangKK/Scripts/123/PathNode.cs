using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum PathDirection
{
    Horizontal,
    Vertical
}

[System.Serializable]
public class ConnectedPath
{
    public PathNode targetNode;
    public PathDirection direction;
}

public class PathNode : MonoBehaviour
{
    [Header("连接到的其他节点（含方向）")]
    public List<ConnectedPath> connectedPaths = new List<ConnectedPath>();

    [HideInInspector] public PuzzlePiece parentPiece;

    private void Awake()
    {
        // 自动找到归属的 PuzzlePiece
        parentPiece = GetComponentInParent<PuzzlePiece>();
    }

    public bool IsConnectedTo(PathNode other)
    {
        return connectedPaths.Exists(p => p.targetNode == other);
    }

    public void ConnectTo(PathNode other)
    {
        if (IsConnectedTo(other)) return;

        PathDirection dir = GetDirectionTo(other.transform.position);
        connectedPaths.Add(new ConnectedPath { targetNode = other, direction = dir });

        if (!other.IsConnectedTo(this))
        {
            other.connectedPaths.Add(new ConnectedPath { targetNode = this, direction = dir });
        }
    }

    public void DisconnectFrom(PathNode other)
    {
        connectedPaths.RemoveAll(p => p.targetNode == other);
        other.connectedPaths.RemoveAll(p => p.targetNode == this);
    }

    private PathDirection GetDirectionTo(Vector3 otherPos)
    {
        Vector3 delta = otherPos - transform.position;
        return Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
            ? PathDirection.Horizontal
            : PathDirection.Vertical;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 绘制路径连接线
        foreach (var path in connectedPaths)
        {
            if (path != null && path.targetNode != null)
            {
                Color color = path.direction == PathDirection.Horizontal ? Color.green : Color.blue;
                Handles.color = color;
                Handles.DrawAAPolyLine(5f, new Vector3[]
                {
                    transform.position,
                    path.targetNode.transform.position
                });
            }
        }

        // ✅ 显示节点名称（仅 Scene 视图）
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 12;

        Handles.Label(transform.position + Vector3.up * 0.2f, gameObject.name, style);
    }
#endif
}