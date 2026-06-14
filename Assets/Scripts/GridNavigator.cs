using UnityEngine;

public class GridNavigator : MonoBehaviour
{
    private Vector3 targetPosition;
    private bool isMoving = false;
    
    [Header("Movement Settings")]
    public float moveSpeed = 10f; 
    public float gridSize = 1f; // The size of each square in your grid (usually 1f)
    
    [Header("Movement Rules")]
    public int maksMenzil = 1;

    void Start()
    {
        // On start, immediately snap to the nearest valid grid coordinate.
        transform.position = CalculateGridPosition(transform.position);
        targetPosition = transform.position;
    }

    public void MoveInDirection(Vector3 direction)
    {
        Vector3 target = transform.position + direction * gridSize;
        MoveTo(target);
    }

    public void MoveTo(Vector3 clickedPoint)
    {
        if (isMoving) return; 

        // 1. Map the raw click position to our rigid grid coordinates.
        Vector3 potentialTarget = CalculateGridPosition(clickedPoint);

        // 2. Optional: Ensure we aren't trying to move to the exact same square.
        if (potentialTarget == transform.position) return;

        // --- RANGE CHECK ---
        // Calculate the Manhattan distance based on our grid size
        int gridXDist = Mathf.Abs(Mathf.RoundToInt((transform.position.x - potentialTarget.x) / gridSize));
        int gridZDist = Mathf.Abs(Mathf.RoundToInt((transform.position.z - potentialTarget.z) / gridSize));
        
        if (gridXDist > maksMenzil || gridZDist > maksMenzil)
        {
            Debug.Log($"Target is too far! Max range: {maksMenzil} squares. Distance: X={gridXDist}, Z={gridZDist}");
            return; 
        }
        // ------------------------

        targetPosition = potentialTarget;
        isMoving = true;
    }

    void Update()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
            {
                // Ensure perfect alignment upon arrival
                transform.position = targetPosition; 
                isMoving = false;
            }
        }
    }

    // A dedicated helper function to correctly calculate the grid center point
    private Vector3 CalculateGridPosition(Vector3 rawPosition)
    {
        return new Vector3(
            Mathf.Round(rawPosition.x / gridSize) * gridSize,
            transform.position.y, // Maintain the object's original Y height
            Mathf.Round(rawPosition.z / gridSize) * gridSize
        );
    }
}