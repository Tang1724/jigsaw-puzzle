using UnityEngine;

public class PassageMarker : MonoBehaviour
{
    [Header("连接信息")]
    public PuzzlePiece puzzle1;
    public PuzzlePiece puzzle2;
    
    [System.NonSerialized]
    public ConnectionPoint connectionPoint1;
    [System.NonSerialized]
    public ConnectionPoint connectionPoint2;
    
    void Start()
    {
        CreateVisualMarker();
    }
    
    void CreateVisualMarker()
    {
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.color = new Color(0, 1, 0, 0.5f);
        renderer.sortingOrder = 1;
        
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        renderer.sprite = sprite;
        
        transform.localScale = new Vector3(1.5f, 0.5f, 1);
    }
    
    public void Initialize(PuzzlePiece p1, PuzzlePiece p2, ConnectionPoint cp1, ConnectionPoint cp2)
    {
        puzzle1 = p1;
        puzzle2 = p2;
        connectionPoint1 = cp1;
        connectionPoint2 = cp2;
    }
}