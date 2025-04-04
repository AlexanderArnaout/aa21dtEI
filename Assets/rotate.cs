using UnityEngine;

public class rotate : MonoBehaviour
{
    public float moveSpeed = 5f;       // Speed at which the cube moves
    public float rotationSpeed = 100f; // Speed at which the cube rotates (spins)
    private float direction = 1f;      // Controls the movement direction

    void Update()
    {
        // Automatic horizontal movement (left and right)
        Vector3 movement = new Vector3(direction * moveSpeed * Time.deltaTime, 0, 0);
        transform.Translate(movement);

        // Automatic rotation (spin) around the Y-axis
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);

        // Change direction when the cube reaches a certain position
        if (transform.position.x >= 5f)  // Move right limit
        {
            direction = -1f;  // Move left
        }
        else if (transform.position.x <= -5f)  // Move left limit
        {
            direction = 1f;  // Move right
        }
    }
}
