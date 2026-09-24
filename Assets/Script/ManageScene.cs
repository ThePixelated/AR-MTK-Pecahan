using UnityEngine;
using UnityEngine.SceneManagement;

public class ManageScene : MonoBehaviour
{
    public void GoToScene(string targetScene)
    {
        
        SceneManager.LoadScene(targetScene);
    }

    public void ExitApplication()
    {
        Application.Quit();
    }
}
