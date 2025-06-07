using UnityEngine;

public class PlayerMoveControl : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;
    
    [Header("边界检测")]
    public LayerMask wallLayer = 1 << 9;  // Walls层
    public float checkRadius = 0.2f;
    
    private Vector2 moveInput;
    private Rigidbody2D rb;
    private PuzzlePiece currentPuzzle;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }
        
        FindCurrentPuzzle();
    }
    
    void Update()
    {
        GetInput();
    }
    
    void FixedUpdate()
    {
        Move();
    }
    
    void GetInput()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
    }
    
    void Move()
    {
        if (moveInput.magnitude > 0)
        {
            Vector2 targetPosition = rb.position + moveInput.normalized * moveSpeed * Time.fixedDeltaTime;
            
            if (CanMoveTo(targetPosition))
            {
                rb.MovePosition(targetPosition);
                CheckPuzzleTransition(targetPosition);
            }
        }
    }
    
    bool CanMoveTo(Vector2 targetPos)
    {
        if (Physics2D.OverlapCircle(targetPos, checkRadius, wallLayer))
        {
            return false;
        }
        
        if (currentPuzzle != null && !IsPositionInConnectedArea(targetPos))
        {
            return false;
        }
        
        return true;
    }
    
    bool IsPositionInConnectedArea(Vector2 pos)
    {
        if (currentPuzzle.IsPositionInside(pos))
        {
            return true;
        }
        
        foreach (var connectedPuzzle in currentPuzzle.GetConnectedPuzzles())
        {
            if (connectedPuzzle.IsPositionInside(pos))
            {
                return true;
            }
        }
        
        return false;
    }
    
    void CheckPuzzleTransition(Vector2 newPos)
    {
        var newPuzzle = FindPuzzleAtPosition(newPos);
        
        if (newPuzzle != null && newPuzzle != currentPuzzle)
        {
            if (currentPuzzle != null && currentPuzzle.IsConnectedTo(newPuzzle))
            {
                SwitchToPuzzle(newPuzzle);
            }
            else if (currentPuzzle == null)
            {
                SwitchToPuzzle(newPuzzle);
            }
        }
    }
    
    void SwitchToPuzzle(PuzzlePiece newPuzzle)
    {
        var oldPuzzle = currentPuzzle;
        currentPuzzle = newPuzzle;
        
        if (oldPuzzle != null)
        {
            Debug.Log($"玩家从拼图 {oldPuzzle.puzzleID} 移动到 {newPuzzle.puzzleID}");
        }
        else
        {
            Debug.Log($"玩家进入拼图: {newPuzzle.puzzleID}");
        }
    }
    
    void FindCurrentPuzzle()
    {
        var foundPuzzle = FindPuzzleAtPosition(transform.position);
        if (foundPuzzle != null)
        {
            SwitchToPuzzle(foundPuzzle);
        }
    }
    
    PuzzlePiece FindPuzzleAtPosition(Vector2 pos)
    {
        var allPuzzles = FindObjectsOfType<PuzzlePiece>();
        foreach (var puzzle in allPuzzles)
        {
            if (puzzle.IsPositionInside(pos))
            {
                return puzzle;
            }
        }
        return null;
    }
    
    public void ForceUpdateCurrentPuzzle()
    {
        FindCurrentPuzzle();
    }
    
    public PuzzlePiece GetCurrentPuzzle()
    {
        return currentPuzzle;
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, checkRadius);
        
        if (currentPuzzle != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(currentPuzzle.transform.position, currentPuzzle.puzzleSize);
            
            Gizmos.color = Color.cyan;
            foreach (var connectedPuzzle in currentPuzzle.GetConnectedPuzzles())
            {
                Gizmos.DrawWireCube(connectedPuzzle.transform.position, connectedPuzzle.puzzleSize);
            }
        }
    }
}