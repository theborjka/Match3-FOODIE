using UnityEngine;

public class AIContext : MonoBehaviour
{
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
         QualitySettings.vSyncCount = 0;
         Application.targetFrameRate = 100;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
