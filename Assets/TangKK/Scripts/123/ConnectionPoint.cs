using UnityEngine;

[System.Serializable]
public class ConnectionPoint
{
    [Header("连接点配置")]
    public ConnectionDirection direction;
    public Vector2 localPosition;
    public ConnectionType connectionType = ConnectionType.Normal;
    
    [Header("连接状态")]
    public bool isConnected = false;
    
    [System.NonSerialized]
    public ConnectionPoint connectedPoint;
    
    [System.NonSerialized]
    public GameObject passageObject;
    
    public Vector3 GetWorldPosition(Transform puzzleTransform)
    {
        return puzzleTransform.position + (Vector3)localPosition;
    }
    
    public void Disconnect()
    {
        if (connectedPoint != null)
        {
            connectedPoint.connectedPoint = null;
            connectedPoint.isConnected = false;
        }
        
        connectedPoint = null;
        isConnected = false;
        
        if (passageObject != null)
        {
            Object.DestroyImmediate(passageObject);
            passageObject = null;
        }
    }
}

public enum ConnectionDirection
{
    Up, Down, Left, Right
}

public enum ConnectionType
{
    Normal, Door, Bridge
}

public struct ConnectionResult
{
    public bool canConnect;
    public float distance;
    public ConnectionPoint myPoint;
    public ConnectionPoint targetPoint;
    public Vector3 snapPosition;
}