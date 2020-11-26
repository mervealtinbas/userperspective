/*==============================================================================
Copyright (c) 2019 PTC Inc. All Rights Reserved.

Copyright (c) 2010-2014 Qualcomm Connected Experiences, Inc.
All Rights Reserved.
Confidential and Proprietary - Protected under copyright and other laws.
==============================================================================*/

using UnityEngine;
using Vuforia;
using System.Collections;
using UnityEngine.Video;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine.UI;
using System.Net;
using System.IO;
using System.Threading;
using UnityEngine.Assertions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.Runtime.InteropServices;



/// <summary>
/// A custom handler that implements the ITrackableEventHandler interface.
///
/// Changes made to this file could be overwritten when upgrading the Vuforia version.
/// When implementing custom event handler behavior, consider inheriting from this class instead.
/// </summary>
[RequireComponent(typeof(Camera))]
public class DefaultTrackableEventHandler : MonoBehaviour, ITrackableEventHandler
{
    #region PROTECTED_MEMBER_VARIABLES

    protected TrackableBehaviour mTrackableBehaviour;
    protected TrackableBehaviour.Status m_PreviousStatus;
    protected TrackableBehaviour.Status m_NewStatus;
    public CloudFaceManager faceManager;
    public float[] matrix = new float[16];
    Matrix4x4 homographyMatrix;
    private Material meshMaterial;
    private Vector3[] source = new Vector3[4];
    private Vector3[] cameraPositionSource = new Vector3[4];
    private Vector3[] cameraPositionDestination = new Vector3[4];
    private Vector3[] destination = new Vector3[4];
    private Vector3 arCameraPosition = new Vector3();
    private Vector3 facePosition = new Vector3();
    public Transform target;
    private Matrix4x4 MVP = new Matrix4x4();
    public Camera camera;
    private WWW wwwData;
    string newJson;
    bool isDone = false;
    SelectionManager manager;
    Color32[] data;
    private ReflectionProbe probeComponent = null;
    RenderTexture m_InputTexture;
    private Cubemap cubemap = null;
    Texture2D texCamShot = null;
    Texture2D output = null;
    private int renderId = -1;
    [Tooltip("Service location for Face API.")]
    public string faceServiceLocation = "westcentralus";
    public RawImage rawimage;


    [Tooltip("Subscription key for Face API.")]
    public string faceSubscriptionKey = "9c227419bf5b4f2abd9fd610ee6b5def";

    //	[Tooltip("Whether to recognize the emotions of the detected faces, or not.")]
    //	public bool recognizeEmotions = false;

    [Tooltip("Service location for Emotion API.")]
    public string emotionServiceLocation = "westcentralus";

    [Tooltip("Subscription key for Emotion API.")]
    public string emotionSubscriptionKey;


    //private const string ServiceHost = "https://api.projectoxford.ai/face/v1.0";
    private const string FaceServiceHost = "https://[location].api.cognitive.microsoft.com/face/v1.0";
    private const string EmotionServiceHost = "https://[location].api.cognitive.microsoft.com/emotion/v1.0";

    private static CloudFaceManager instance = null;
    private bool isInitialized = false;
    WebCamTexture frontWebcamTexture;
    WebCamTexture backWebcamTexture;
    private Material renderTexture;
    Renderer _renderer;
    //private Vuforia.Image.YUYV mPixelFormat = Vuforia.Image.PIXEL_FORMAT.UNKNOWN_FORMAT;
    [HideInInspector]
    public List<Face> faces;
    #region PRIVATE_MEMBERS
    private PIXEL_FORMAT mPixelFormat = PIXEL_FORMAT.UNKNOWN_FORMAT;
    private bool mAccessCameraImage = true;
    private bool mFormatRegistered = false;
    byte[] pixels = null;
    Vuforia.Image image;
    public RenderTexture rt;
    GameObject cube;
    GameObject cube2;

    GameObject rawImage;
    string frontCamName = null;
    string backCamName = null;
    public int resWidth = 2550;
    public int resHeight = 3300;
    public Camera camera1;
    public float faceX;
    private float nextActionTime = 0.0f;
    public float period = 1.0f;

    #endregion // PRIVATE_MEMBERS


    #endregion // PROTECTED_MEMBER_VARIABLES

    #region UNITY_MONOBEHAVIOUR_METHODS

    #region MONOBEHAVIOUR_METHODS
    protected virtual void Start()
    {
        /*  GameObject probeGameObject = new GameObject("Default Reflection Probe");
         probeGameObject.transform.position = new Vector3(0, 0, 0);
         probeComponent = probeGameObject.AddComponent<ReflectionProbe>() as ReflectionProbe;
         probeComponent.resolution = 256;
         probeComponent.size = new Vector3(1, 1, 1);
         probeComponent.cullingMask = 0;
         probeComponent.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
         probeComponent.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
         probeComponent.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
         probeComponent.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing; */

        GameObject probeGameObject = GameObject.Find("Reflection Probe");
        probeComponent = probeGameObject.GetComponent<ReflectionProbe>();
        cubemap = new Cubemap(probeComponent.resolution, TextureFormat.RGBAFloat, false);
        mTrackableBehaviour = GetComponent<TrackableBehaviour>();
        if (mTrackableBehaviour)
        {
            mTrackableBehaviour.RegisterTrackableEventHandler(this);
        }

#if UNITY_EDITOR
        mPixelFormat = PIXEL_FORMAT.GRAYSCALE; // Need Grayscale for Editor
#else
        mPixelFormat = PIXEL_FORMAT.RGB888; // Use RGB888 for mobile
#endif
        VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStarted);
        //VuforiaARController.Instance.RegisterTrackablesUpdatedCallback(OnTrackablesUpdated);
        var webCamDevices = WebCamTexture.devices;
        foreach (var camDevice in webCamDevices)
        {
            /*  if(camDevice.isFrontFacing){
                frontCamName = camDevice.name;
            }else{
                backCamName = camDevice.name;
            }  */
            if (camDevice.name == "Integrated Camera")
            {
                backCamName = camDevice.name;
            }
            else
            {
                frontCamName = camDevice.name;
            }
        }

        frontWebcamTexture = new WebCamTexture(frontCamName);
        frontWebcamTexture.Play();
        backWebcamTexture = new WebCamTexture(backCamName);


    }

    void Update()
    {
        if (Time.time > nextActionTime)
        {
            nextActionTime += period;
            Texture2D brightBGTexture = new Texture2D(frontWebcamTexture.width, frontWebcamTexture.height);
            brightBGTexture.SetPixels32(frontWebcamTexture.GetPixels32(), 0);
            brightBGTexture.Apply();
            StartCoroutine(zamanliCalis(brightBGTexture));
        }
    }
    #endregion // MONOBEHAVIOUR_METHODS

    protected virtual void OnDestroy()
    {
        if (mTrackableBehaviour)
            mTrackableBehaviour.UnregisterTrackableEventHandler(this);
    }

    private void OnVuforiaStarted()
    {
        // Try register camera image format
        if (CameraDevice.Instance.SetFrameFormat(mPixelFormat, true))
        {
            Debug.Log("Successfully registered pixel format " + mPixelFormat.ToString());
            mFormatRegistered = true;
        }
        else
        {
            Debug.LogError("Failed to register pixel format " + mPixelFormat.ToString() +
                "\n the format may be unsupported by your device;" +
                "\n consider using a different pixel format.");
            mFormatRegistered = false;
        }
    }
    IEnumerator OnTrackablesUpdated()

    {
        if (mFormatRegistered)
        {
            if (mAccessCameraImage)
            {
                image = CameraDevice.Instance.GetCameraImage(mPixelFormat);
                if (image != null)
                {
                    pixels = image.Pixels;
                }
            }
        }
        yield return image;
    }

    #endregion // UNITY_MONOBEHAVIOUR_METHODS

    #region PUBLIC_METHODS

    /// <summary>
    ///     Implementation of the ITrackableEventHandler function called when the
    ///     tracking state changes.
    /// </summary>
    public void OnTrackableStateChanged(
        TrackableBehaviour.Status previousStatus,
        TrackableBehaviour.Status newStatus)
    {
        m_PreviousStatus = previousStatus;
        m_NewStatus = newStatus;
        Debug.Log("Trackable " + mTrackableBehaviour.TrackableName +
                  " " + mTrackableBehaviour.CurrentStatus +
                  " -- " + mTrackableBehaviour.CurrentStatusInfo);

        if (newStatus == TrackableBehaviour.Status.DETECTED ||
            newStatus == TrackableBehaviour.Status.TRACKED ||
            newStatus == TrackableBehaviour.Status.EXTENDED_TRACKED)
        {
            OnTrackingFound();
        }
        else if (previousStatus == TrackableBehaviour.Status.TRACKED &&
                 newStatus == TrackableBehaviour.Status.NO_POSE)
        {
            OnTrackingLost();
        }
        else
        {
            // For combo of previousStatus=UNKNOWN + newStatus=UNKNOWN|NOT_FOUND
            // Vuforia is starting, but tracking has not been lost or found yet
            // Call OnTrackingLost() to hide the augmentations
            OnTrackingLost();
        }
    }

    #endregion // PUBLIC_METHODS

    #region PROTECTED_METHODS

    protected virtual void OnTrackingFound()
    {

        if (mTrackableBehaviour)

        {
            var rendererComponents = mTrackableBehaviour.GetComponentsInChildren<Renderer>(true);
            var colliderComponents = mTrackableBehaviour.GetComponentsInChildren<Collider>(true);
            var canvasComponents = mTrackableBehaviour.GetComponentsInChildren<Canvas>(true);

            // Enable rendering:
            foreach (var component in rendererComponents)
                component.enabled = true;

            // Enable colliders:
            foreach (var component in colliderComponents)
                component.enabled = true;

            // Enable canvas':
            foreach (var component in canvasComponents)
                component.enabled = true;

            cube = GameObject.Find("Cube");
            cube2 = GameObject.Find("Cube2");
            Camera camera = Camera.main;
            camera.enabled = false;

            camera1.enabled = true;

            cube.GetComponent<Renderer>().material.mainTexture = frontWebcamTexture;
            StartCoroutine(arkaKameraGoruntusuAl());
            cube2.GetComponent<Renderer>().material.mainTexture = texCamShot;
            Texture2D brightBGTexture = new Texture2D(frontWebcamTexture.width, frontWebcamTexture.height);
            brightBGTexture.SetPixels32(frontWebcamTexture.GetPixels32(), 0);
            brightBGTexture.Apply();
            StartCoroutine(zamanliCalis(brightBGTexture));

        }
    }
    IEnumerator arkaKameraGoruntusuAl()
    {

        image = CameraDevice.Instance.GetCameraImage(mPixelFormat);
        texCamShot = new Texture2D(image.Width, image.Height, TextureFormat.RGB24, false);
        texCamShot.Resize(128, 128);
        image.CopyToTexture(texCamShot);
        Color[] pixels = texCamShot.GetPixels();
        Color[] pixelsFlipped = new Color[pixels.Length];
        for (int i = 0; i < image.Height; i++)
        {
            Array.Copy(pixels, i * image.Width, pixelsFlipped, (image.Height - i - 1) * image.Width, image.Width);
        }

        texCamShot.SetPixels(pixelsFlipped);
        texCamShot.Apply();
        yield return texCamShot;
    }
    IEnumerator zamanliCalis(Texture2D brightBGTexture)
    {
        yield return StartCoroutine(KameraPozisyonariniBul(brightBGTexture));
        /* yield return StartCoroutine(ExampleCoroutine());
        yield return StartCoroutine(KameraPozisyonariniBul(1)); */

        // arCamera.transform.position = Vector3.Lerp(arCameraPosition, facePosition, Time.time);

        cameraPositionSource[0].x = 0;
        cameraPositionSource[0].y = 0;
        cameraPositionSource[1].x = backWebcamTexture.width;
        cameraPositionSource[1].y = 0;
        cameraPositionSource[2].x = 0;
        cameraPositionSource[2].y = backWebcamTexture.height;
        cameraPositionSource[3].x = backWebcamTexture.width;
        cameraPositionSource[3].y = backWebcamTexture.height;


        //FindHomography(cameraPositionSource, cameraPositionDestination);
        Vector3 targetLocation = new Vector3(faceX, 0, -5);
        Vector3 vectorToTarget = -targetLocation;
        camera1.transform.localPosition = targetLocation;
        /*  Quaternion rot = Quaternion.LookRotation(forward: vectorToTarget.normalized, upwards: -Vector3.up);
         camera1.transform.localRotation=rot; */
        // camera1.transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, turnSpeed * Time.deltaTime);

        //cube.transform.localScale = ExtractScale(homographyMatrix);
        // cube.transform.rotation = ExtractRotation(homographyMatrix);
        //cube.transform.position = ExtractPosition(homographyMatrix);
        // cube2.transform.position = ExtractPosition(homographyMatrix);

        //TODO camera posizyonu ile yüz pozisyonu arasındaki homography matrisini bulacaz..
        //meshMaterial = GameObject.Find("Cube").GetComponent<Renderer>().material;
        /*  meshMaterial.SetVector("matrixRow_1", new Vector4(matrix[0], matrix[4], matrix[8], matrix[12]));
         meshMaterial.SetVector("matrixRow_2", new Vector4(matrix[1], matrix[5], matrix[9], matrix[13]));
         meshMaterial.SetVector("matrixRow_3", new Vector4(matrix[2], matrix[6], matrix[10], matrix[14]));
         meshMaterial.SetVector("matrixRow_4", new Vector4(matrix[3], matrix[7], matrix[11], matrix[15]));  */



    }
    public static Quaternion ExtractRotation(Matrix4x4 matrix)
    {
        Vector3 forward;
        forward.x = matrix.m02;
        forward.y = matrix.m12;
        forward.z = matrix.m22;

        Vector3 upwards;
        upwards.x = matrix.m01;
        upwards.y = matrix.m11;
        upwards.z = matrix.m21;

        return Quaternion.LookRotation(forward, upwards);
    }

    public static Vector3 ExtractPosition(Matrix4x4 matrix)
    {
        Vector3 position;
        position.x = matrix.m03;
        position.y = matrix.m13;
        position.z = matrix.m23;
        return position;
    }

    public static Vector3 ExtractScale(Matrix4x4 matrix)
    {
        Vector3 scale;
        scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
        scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
        scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
        return scale;
    }
    void FindHomography(Vector3[] fromCorners, Vector3[] toCorners)
    {
        double[][] arr = new double[8][];

        for (int i = 0; i != fromCorners.Length; ++i)
        {
            var p1 = fromCorners[i];
            var p2 = toCorners[i];
            arr[i * 2] = new double[] { -p1.x, -p1.y, -1, 0, 0, 0, p2.x * p1.x, p2.x * p1.y, p2.x };
            arr[i * 2 + 1] = new double[] { 0, 0, 0, -p1.x, -p1.y, -1, p2.y * p1.x, p2.y * p1.y, p2.y };
        }

        var svd = DenseMatrix.OfRowArrays(arr).Svd();
        var v = svd.VT.Transpose();

        // right singular vector
        var rsv = v.Column(v.ColumnCount - 1);

        // reshape to 3x3 matrix
        Matrix4x4 h = Matrix4x4.zero;
        h.SetRow(0, new Vector4((float)rsv[0], (float)rsv[1], (float)rsv[2], 0));
        h.SetRow(1, new Vector4((float)rsv[3], (float)rsv[4], (float)rsv[5], 0));
        h.SetRow(2, new Vector4((float)rsv[6], (float)rsv[7], (float)rsv[8], 0));
        homographyMatrix = h;
    }
    IEnumerator KameraPozisyonariniBul(Texture2D brightBGTexture)
    {
        GameObject obj = new GameObject();
        obj.AddComponent<CloudFaceManager>();
        CloudFaceManager cloudFaceManager = obj.GetComponent<CloudFaceManager>();
        /*  Texture2D brightBGTexture = new Texture2D(frontWebcamTexture.width, frontWebcamTexture.height);
        brightBGTexture.SetPixels32(frontWebcamTexture.GetPixels32(), 0);
        brightBGTexture.Apply(); */
        yield return StartCoroutine(cloudFaceManager.DetectFaces(brightBGTexture, cube));
        cameraPositionDestination = cloudFaceManager.cameraPositionSourceVector;
        faceX = cloudFaceManager.faceX;
    }

    static byte[] Color32ArrayToByteArray(Color32[] colors)
    {
        // https://stackoverflow.com/a/21575147/2496170

        if (colors == null || colors.Length == 0) return null;

        int lengthOfColor32 = Marshal.SizeOf(typeof(Color32));

        int length = lengthOfColor32 * colors.Length;

        byte[] bytes = new byte[length];

        GCHandle handle = default(GCHandle);

        try
        {
            handle = GCHandle.Alloc(colors, GCHandleType.Pinned);
            IntPtr ptr = handle.AddrOfPinnedObject();
            Marshal.Copy(ptr, bytes, 0, length);
        }
        finally
        {
            if (handle != default(GCHandle)) handle.Free();
        }

        return bytes;
    }

    IEnumerator ExampleCoroutine()
    {
        //Print the time of when the function is first called.
        Debug.Log("Started Coroutine at timestamp : " + Time.time);

        //yield on a new YieldInstruction that waits for 5 seconds.
        yield return new WaitForSeconds(3);

        //After we have waited 5 seconds print the time again.
        Debug.Log("Finished Coroutine at timestamp : " + Time.time);
    }


    /*  void FindHomography(ref Vector3[] src, ref Vector3[] dest, ref float[] homography) 
     { 
         float[,] P = new float [,]{  
         {-src[0].x, -src[0].y, -1,   0,   0,  0, src[0].x*dest[0].x, src[0].y*dest[0].x, -dest[0].x }, // h11  
         {  0,   0,  0, -src[0].x, -src[0].y, -1, src[0].x*dest[0].y, src[0].y*dest[0].y, -dest[0].y }, // h12  

         {-src[1].x, -src[1].y, -1,   0,   0,  0, src[1].x*dest[1].x, src[1].y*dest[1].x, -dest[1].x }, // h13  
         {  0,   0,  0, -src[1].x, -src[1].y, -1, src[1].x*dest[1].y, src[1].y*dest[1].y, -dest[1].y }, // h21  

         {-src[2].x, -src[2].y, -1,   0,   0,  0, src[2].x*dest[2].x, src[2].y*dest[2].x, -dest[2].x }, // h22  
         {  0,   0,  0, -src[2].x, -src[2].y, -1, src[2].x*dest[2].y, src[2].y*dest[2].y, -dest[2].y }, // h23  

         {-src[3].x, -src[3].y, -1,   0,   0,  0, src[3].x*dest[3].x, src[3].y*dest[3].x, -dest[3].x }, // h31  
         {  0,   0,  0, -src[3].x, -src[3].y, -1, src[3].x*dest[3].y, src[3].y*dest[3].y, -dest[3].y }, // h32  
         };  

         GaussianElimination(ref P,9);  

         // gaussian elimination gives the results of the equation system  
         // in the last column of the original matrix.  
         // opengl needs the transposed 4x4 matrix:  
         float[] aux_H={ P[0,8],P[3,8],0,P[6,8], // h11  h21 0 h31  
             P[1,8],P[4,8],0,P[7,8], // h12  h22 0 h32  
             0      ,      0,0,0,       // 0    0   0 0  
             P[2,8],P[5,8],0,1};      // h13  h23 0 h33  

         for(int i=0;i<16;i++) homography[i] = aux_H[i];  

     } */



    void GaussianElimination(ref float[,] A, int n)
    {
        // originally by arturo castro - 08/01/2010  
        //  
        // ported to c from pseudocode in  
        // http://en.wikipedia.org/wiki/Gaussian_elimination  

        int i = 0;
        int j = 0;
        int m = n - 1;
        while (i < m && j < n)
        {
            // Find pivot in column j, starting in row i:  
            int maxi = i;
            for (int k = i + 1; k < m; k++)
            {
                if (Mathf.Abs(A[k, j]) > Mathf.Abs(A[maxi, j]))
                {
                    maxi = k;
                }
            }
            if (A[maxi, j] != 0)
            {
                //swap rows i and maxi, but do not change the value of i  
                if (i != maxi)
                    for (int k = 0; k < n; k++)
                    {
                        float aux = A[i, k];
                        A[i, k] = A[maxi, k];
                        A[maxi, k] = aux;
                    }
                //Now A[i,j] will contain the old value of A[maxi,j].  
                //divide each entry in row i by A[i,j]  
                float A_ij = A[i, j];
                for (int k = 0; k < n; k++)
                {
                    A[i, k] /= A_ij;
                }
                //Now A[i,j] will have the value 1.  
                for (int u = i + 1; u < m; u++)
                {
                    //subtract A[u,j] * row i from row u  
                    float A_uj = A[u, j];
                    for (int k = 0; k < n; k++)
                    {
                        A[u, k] -= A_uj * A[i, k];
                    }
                    //Now A[u,j] will be 0, since A[u,j] - A[i,j] * A[u,j] = A[u,j] - 1 * A[u,j] = 0.  
                }

                i++;
            }
            j++;
        }

        //back substitution  
        for (int k = m - 2; k >= 0; k--)
        {
            for (int l = k + 1; l < n - 1; l++)
            {
                A[k, m] -= A[k, l] * A[l, m];
                //A[i*n+j]=0;  
            }
        }
    }



    protected virtual void OnTrackingLost()
    {
        if (mTrackableBehaviour)
        {
            var rendererComponents = mTrackableBehaviour.GetComponentsInChildren<Renderer>(true);
            var colliderComponents = mTrackableBehaviour.GetComponentsInChildren<Collider>(true);
            var canvasComponents = mTrackableBehaviour.GetComponentsInChildren<Canvas>(true);

            // Disable rendering:
            foreach (var component in rendererComponents)
                component.enabled = false;

            // Disable colliders:
            foreach (var component in colliderComponents)
                component.enabled = false;

            // Disable canvas':
            foreach (var component in canvasComponents)
                component.enabled = false;
        }
    }

    #endregion // PROTECTED_METHODS
}
