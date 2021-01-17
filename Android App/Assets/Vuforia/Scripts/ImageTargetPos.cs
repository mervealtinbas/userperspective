using UnityEngine;

public class ImageTargetPos : MonoBehaviour
{
    private Transform imageTarget;
    private Camera arCam;
    private bool target_isDetected;
    private int updateCount;

    public Vector3 normalizedPosition;

    // Start is called before the first frame update
    void Start()
    {
        arCam = GetComponent<Camera>();
        imageTarget = GameObject.Find("ImageTarget").transform;
        updateCount = 1;
    }

    // Update is called once per frame
    void Update()
    {
        if (target_isDetected && updateCount >= 6) //take the position every 6 frames, results in 10 updates per second at 60 fps
        {
            Vector3 screenPos = arCam.ScreenToWorldPoint(imageTarget.position); //get normalized screenspace coords 
            updateCount = 0;
            normalizedPosition.x = screenPos.x;
            normalizedPosition.y = screenPos.y;
            normalizedPosition.z = screenPos.z;
            Debug.Log(normalizedPosition);
        }

        updateCount++;
    }

    public void TargetDetected() { target_isDetected = true; }

    public void TargetLost() { target_isDetected = false; }
}