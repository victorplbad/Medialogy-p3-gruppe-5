using UnityEngine;

public class GameResponse : MonoBehaviour
{
    public void OnClassificationComplete(
        string detectedClass,
        float confidence)
    {
        Debug.Log("Game received: " + detectedClass);

        if (detectedClass == "car")
        {
            Debug.Log("Car detected!");
            // Trigger car-related game logic
        }
        else if (detectedClass == "bike")
        {
            Debug.Log("Bike detected!");
            // Trigger bike-related game logic
        }
        else if (detectedClass == "person")
        {
            Debug.Log("Person detected!");
            // Trigger person-related game logic
        }
        else
        {
            Debug.Log("Unknown object");
        }
    }
}