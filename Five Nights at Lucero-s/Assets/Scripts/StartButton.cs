using UnityEngine;
using UnityEngine.SceneManagement;

public class StartButton : MonoBehaviour
{
    // This method will be called when the button is clicked
    public void StartGame(string RoomScene)
    {
        SceneManager.LoadScene(RoomScene);
    }
}
