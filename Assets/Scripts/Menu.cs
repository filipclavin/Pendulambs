using UnityEngine;

public class Menu : MonoBehaviour
{
    public void Navigate(Canvas target)
    {
        target.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }
}
