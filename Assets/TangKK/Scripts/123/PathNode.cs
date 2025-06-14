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

    [Header("线条设置")]
    public Material lineMaterial;
    public Material dashedLineMaterial;
    public float lineWidth = 0.1f;
    public Color lineColor = Color.green;
    public Color dashedLineColor = Color.gray;
    public int sortingOrder = 100;

    [Header("连接状态")]
    public bool isActive = true;

    [HideInInspector]
    public LineRenderer lineRenderer;
}

public class PathNode : MonoBehaviour
{
    [Header("连接的路径")]
    public List<ConnectedPath> connectedPaths = new List<ConnectedPath>();

    [HideInInspector] public PuzzlePiece parentPiece;
    private List<LineRenderer> myLineRenderers = new List<LineRenderer>();

    public int GroupID => parentPiece != null ? parentPiece.initialGroupID : -1;

    private void Awake()
    {
        AssignParentPiece();
    }

    private void Start()
    {
        CreatePathLines();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AssignParentPiece();
    }
#endif

    private void AssignParentPiece()
    {
        parentPiece = FindClosestPuzzlePiece();
        if (parentPiece == null)
        {
            Debug.LogWarning($"[PathNode] {name} 无法找到所属拼图块");
        }
    }

    private PuzzlePiece FindClosestPuzzlePiece()
    {
        Transform current = transform.parent;
        while (current != null)
        {
            PuzzlePiece piece = current.GetComponent<PuzzlePiece>();
            if (piece != null) return piece;
            current = current.parent;
        }
        return null;
    }

    public void CreatePathLines()
    {
        ClearPathLines();

        foreach (var path in connectedPaths)
        {
            if (path.targetNode == null) continue;
            CreateLineTo(path.targetNode, path);
        }
    }

    private void CreateLineTo(PathNode targetNode, ConnectedPath path)
    {
        GameObject lineObj = new GameObject($"Line_{name}_to_{targetNode.name}");
        lineObj.transform.SetParent(transform);

        LineRenderer line = lineObj.AddComponent<LineRenderer>();

        UpdateLineAppearance(line, path);

        line.positionCount = 2;
        line.useWorldSpace = true;
        line.sortingOrder = path.sortingOrder;
        line.SetPosition(0, transform.position);
        line.SetPosition(1, targetNode.transform.position);

        path.lineRenderer = line;
        myLineRenderers.Add(line);
    }

    private void UpdateLineAppearance(LineRenderer line, ConnectedPath path)
    {
        if (path.isActive)
        {
            line.material = path.lineMaterial != null ? path.lineMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = path.lineColor;
            line.endColor = path.lineColor;
        }
        else
        {
            line.material = path.dashedLineMaterial != null ? path.dashedLineMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = path.dashedLineColor;
            line.endColor = path.dashedLineColor;
        }

        line.startWidth = path.lineWidth;
        line.endWidth = path.lineWidth;
    }

    void Update()
    {
        for (int i = 0; i < connectedPaths.Count; i++)
        {
            var path = connectedPaths[i];
            if (path.lineRenderer != null && path.targetNode != null)
            {
                path.lineRenderer.SetPosition(0, transform.position);
                path.lineRenderer.SetPosition(1, path.targetNode.transform.position);
            }
        }
    }

    public void ClearPathLines()
    {
        foreach (var line in myLineRenderers)
        {
            if (line != null)
            {
                if (Application.isPlaying)
                    Destroy(line.gameObject);
                else
                    DestroyImmediate(line.gameObject);
            }
        }
        myLineRenderers.Clear();
    }

    public bool IsConnectedTo(PathNode other)
    {
        return connectedPaths.Exists(p => p.targetNode == other);
    }

    public void ConnectTo(PathNode other)
    {
        if (other == null || other == this) return;

        if (this.GroupID != other.GroupID)
        {
            Debug.LogWarning($"[PathNode] ❌ 不同组无法连接: {name}(组 {GroupID}) → {other.name}(组 {other.GroupID})");
            return;
        }

        if (IsConnectedTo(other)) return;

        PathDirection dir = GetDirectionTo(other.transform.position);

        ConnectedPath newPath = new ConnectedPath
        {
            targetNode = other,
            direction = dir,
            isActive = true
        };

        connectedPaths.Add(newPath);

        if (!other.IsConnectedTo(this))
        {
            other.connectedPaths.Add(new ConnectedPath
            {
                targetNode = this,
                direction = dir,
                isActive = true
            });
        }

        CreatePathLines();
        other.CreatePathLines();
    }

    public void SetPathActive(PathNode targetNode, bool active)
    {
        foreach (var path in connectedPaths)
        {
            if (path.targetNode == targetNode)
            {
                path.isActive = active;
                if (path.lineRenderer != null)
                    UpdateLineAppearance(path.lineRenderer, path);
                break;
            }
        }

        foreach (var path in targetNode.connectedPaths)
        {
            if (path.targetNode == this)
            {
                path.isActive = active;
                if (path.lineRenderer != null)
                    UpdateLineAppearance(path.lineRenderer, path);
                break;
            }
        }
    }

    public void RefreshPathLines()
    {
        CreatePathLines();
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
        Color[] colors = { Color.green, Color.cyan, Color.yellow, Color.magenta, Color.red };
        int colorIndex = Mathf.Abs(GroupID) % colors.Length;

        foreach (var path in connectedPaths)
        {
            if (path != null && path.targetNode != null)
            {
                Handles.color = path.isActive ? colors[colorIndex] : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                Handles.DrawAAPolyLine(3f, transform.position, path.targetNode.transform.position);
            }
        }

        GUIStyle style = new GUIStyle
        {
            normal = { textColor = Color.white },
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };

        Handles.Label(transform.position + Vector3.up * 0.25f, $"组 {GroupID}", style);
    }
#endif
}