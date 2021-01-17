using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System;
using System.Net;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEngine;


public class CloudFaceManager : MonoBehaviour
{
    [Tooltip("Service location for Face API.")]
    public string faceServiceLocation = "westcentralus";

    [Tooltip("Subscription key for Face API.")]
    public string faceSubscriptionKey = "6a90195774a34021b63cc3a46b73a4d0";

    //	[Tooltip("Whether to recognize the emotions of the detected faces, or not.")]
    //	public bool recognizeEmotions = false;

    [Tooltip("Service location for Emotion API.")]
    public string emotionServiceLocation = "northeurope";

    [Tooltip("Subscription key for Emotion API.")]
    public string emotionSubscriptionKey;

    [HideInInspector]
    public List<Face> faces;  // the detected faces
    public Vector3[] cameraPositionSourceVector = new Vector3[4];

    public Vector3 normalizedFace;
    public float fov;
    public Vector3 facePosition;
    private WWW wwwData;


    //private const string ServiceHost = "https://api.projectoxford.ai/face/v1.0";
     // 7 günlük deneme süresi adresi
    private const string FaceServiceHost = "https://[location].api.cognitive.microsoft.com/face/v1.0";
   // private const string FaceServiceHost = "https://merve.cognitiveservices.azure.com/face/v1.0";
  
    private const string EmotionServiceHost = "https://[location].api.cognitive.microsoft.com/emotion/v1.0";

    private static CloudFaceManager instance = null;
    private bool isInitialized = false;
    bool isDone = false;
    string newJson;
    Texture2D tex;

    float frustumHeight;

    void Start()
    {
        instance = this;

        if (string.IsNullOrEmpty(faceSubscriptionKey))
        {
            throw new Exception("Please set your face-subscription key.");
        }

        isInitialized = true;
    }

    /// <summary>
    /// Gets the FaceManager instance.
    /// </summary>
    /// <value>The FaceManager instance.</value>
    public static CloudFaceManager Instance
    {
        get
        {
            return instance;
        }
    }


    /// <summary>
    /// Determines whether the FaceManager is initialized.
    /// </summary>
    /// <returns><c>true</c> if the FaceManager is initialized; otherwise, <c>false</c>.</returns>
    public bool IsInitialized()
    {
        return isInitialized;
    }



    /// <summary>
    /// Detects the faces in the given image.
    /// </summary>
    /// <returns>List of detected faces.</returns>
    /// <param name="imageBytes">Image bytes.</param>
    public IEnumerator DetectFaces(Texture2D texImage, GameObject cube)

    {
        tex = new Texture2D(16, 16, TextureFormat.PVRTC_RGBA4, false);
        byte[] imageBytes = texImage.EncodeToPNG();
        string jpgFile = Application.persistentDataPath + "/yuzalgilama" + ".jpg";
        Debug.Log(jpgFile);
        System.IO.File.WriteAllBytes(jpgFile, imageBytes);
        tex.LoadRawTextureData(imageBytes);
        tex.Apply();

        faces = null;

        if (string.IsNullOrEmpty(faceSubscriptionKey))
        {
            throw new Exception("The face-subscription key is not set.");
        }

        // detect faces
        string faceServiceHost = FaceServiceHost.Replace("[location]", faceServiceLocation);
        string requestUrl = string.Format("{0}/detect?returnFaceId={1}&returnFaceLandmarks={2}&returnFaceAttributes={3}", faceServiceHost, true, false, "age,gender,smile,headPose");

        Dictionary<string, string> headers = new Dictionary<string, string>();
        headers.Add("Ocp-Apim-Subscription-Key", faceSubscriptionKey);
        headers.Add("Content-Type", "application/octet-stream");
        yield return StartCoroutine(WaitForDownload(requestUrl, imageBytes, headers));
        // DrawFaceRects(texImage, faces, FaceDetectionUtils.FaceColors, true, cube);

    }

    private IEnumerator WaitForDownload(string requestUrl, byte[] imageBytes, Dictionary<string, string> headers)
    {
        wwwData = new WWW(requestUrl, imageBytes, headers);
        yield return wwwData;
        newJson = "{ \"faces\": " + wwwData.text + "}";
        Debug.Log(newJson);
        FaceCollection faceCollection = JsonUtility.FromJson<FaceCollection>(newJson);

        faces = faceCollection.faces;
        if (faces.Count != 0)
        {
            FaceRectangle rect = faces[0].faceRectangle;
            Debug.Log("Top:"+ rect.top);
            Debug.Log("Left:"+ rect.left);
            Debug.Log("Width:"+ rect.width);
            Debug.Log("Height:"+ rect.height);
            float x = rect.left;
            float y = rect.top;
            float roll = faces[0].faceAttributes.headPose.roll;
            //TODO 640 değeri parametrik olmalı
            normalizedFace.x = (rect.left + (rect.width / 2f) - 640) / 640;
            normalizedFace.y = (rect.top + (rect.height / 2f) - 360) / 360;
            normalizedFace.z = 1.0f*rect.width/1280f;
        }



    }

    // processes the error status in response
    private void ProcessFaceError(WWW www)
    {
        //ClientError ex = JsonConvert.DeserializeObject<ClientError>(www.text);
        ClientError ex = JsonUtility.FromJson<ClientError>(www.text);

        if (ex.error != null && ex.error.code != null)
        {
            string sErrorMsg = !string.IsNullOrEmpty(ex.error.code) && ex.error.code != "Unspecified" ?
                ex.error.code + " - " + ex.error.message : ex.error.message;
            throw new System.Exception(sErrorMsg);
        }
        else
        {
            //ServiceError serviceEx = JsonConvert.DeserializeObject<ServiceError>(www.text);
            ServiceError serviceEx = JsonUtility.FromJson<ServiceError>(www.text);

            if (serviceEx != null && serviceEx.statusCode != null)
            {
                string sErrorMsg = !string.IsNullOrEmpty(serviceEx.statusCode) && serviceEx.statusCode != "Unspecified" ?
                    serviceEx.statusCode + " - " + serviceEx.message : serviceEx.message;
                throw new System.Exception(sErrorMsg);
            }
            else
            {
                throw new System.Exception("Error " + CloudWebTools.GetStatusCode(www) + ": " + CloudWebTools.GetStatusMessage(www) + "; Url: " + www.url);
            }
        }
    }


    // draw face rectangles
    /// <summary>
    /// Draws the face rectangles in the given texture.
    /// </summary>
    /// <param name="faces">List of faces.</param>
    /// <param name="tex">Tex.</param>
    /// <param name="faceColors">List of face colors for each face</param>
    /// <param name="drawHeadPoseArrow">If true, draws arrow according to head pose of each face</param>
    public void DrawFaceRects(Texture2D tex, List<Face> faces, Color[] faceColors, bool drawHeadPoseArrow, GameObject cube)
    {
        int i = 0;
        //GameObject obj = new GameObject();
        //obj.AddComponent<CloudTexTools>();
        foreach (Face face in faces)
        {


            Color faceColor = faceColors[i % faceColors.Length];

            FaceRectangle rect = face.faceRectangle;
            CloudTexTools.DrawRect(tex, rect.left, rect.top, rect.width, rect.height, faceColor, cube);
            i = i + 1;

            if (drawHeadPoseArrow)
            {
                HeadPose headPose = face.faceAttributes.headPose;

                int cx = rect.width / 2;
                int cy = rect.height / 4;
                int arrowX = rect.left + cx;
                int arrowY = rect.top + (3 * cy);
                int radius = Math.Min(cx, cy);

                float x = arrowX + radius * Mathf.Sin(headPose.yaw * Mathf.Deg2Rad);
                float y = arrowY + radius * Mathf.Cos(headPose.yaw * Mathf.Deg2Rad);

                int arrowHead = radius / 4;
                if (arrowHead > 15) arrowHead = 15;
                if (arrowHead < 8) arrowHead = 8;

                //CloudTexTools.DrawArrow(tex, arrowX, arrowY, (int)x, (int)y, faceColor, arrowHead, 30);
            }
        }

        tex.Apply();
    }



}
