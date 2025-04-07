using UnityEngine;

public class MoveLeftRight : MonoBehaviour
{
    public float moveSpeed = 5f;  // The speed at which the object moves
    public float moveRange = 5f;  // The range (left to right) the object can move

    private float startPosX;

    void Start()
    {
        // Save the starting position of the object
        startPosX = transform.position.x;
    }

    void Update()
    {
        // Move the object left and right in a sine wave pattern
        float newPosX = startPosX + Mathf.Sin(Time.time * moveSpeed) * moveRange;

        // Update the object's position
        transform.position = new Vector3(newPosX, transform.position.y, transform.position.z);
    }
}
