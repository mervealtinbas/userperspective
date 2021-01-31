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
    Vector3[] homographyMatrix;
    private Material meshMaterial;
    private Material meshMaterial2;
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

    public ImageTargetPos arCam;


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
    GameObject cube3;
    GameObject plane;
    GameObject imageTarget;

    GameObject rawImage;
    string frontCamName = null;
    string backCamName = null;
    public int resWidth = 2550;
    public int resHeight = 3300;
    public Camera camera1;
    public Vector3 normalizedFace;

    public Vector3 normalizedPosition;

    public float fov;
    private float nextActionTime = 0.0f;
    public float period = 1.0f;
    Matrix4x4 homographyMatrixx;

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
        arCam = GameObject.Find("ARCamera").GetComponent<ImageTargetPos>();
        var webCamDevices = WebCamTexture.devices;
        foreach (var camDevice in webCamDevices)
        {
            /* if(camDevice.isFrontFacing){
               frontCamName = camDevice.name;
           }else{
               backCamName = camDevice.name;
           } */
            if (camDevice.name == "Integrated Camera")
            {
                backCamName = camDevice.name;
            }
            else
            {
                frontCamName = camDevice.name;
            }
        }
        cube = GameObject.Find("Cube");
         cube.SetActive(false);
        cube2 = GameObject.Find("Cube2");
         cube2.SetActive(false);
         plane=GameObject.Find("Plane");
        //cube3 = GameObject.Find("Cube3");
        frontWebcamTexture = new WebCamTexture(frontCamName);
        backWebcamTexture = new WebCamTexture(backCamName);
        backWebcamTexture.Play();
        /* cube.GetComponent<Renderer>().material.mainTexture = frontWebcamTexture;
        frontWebcamTexture.Play(); */
    }

    void Update()
    {
            //TODO ölçleri bilgisayar kamerasına ve boyutlarına göre ayarla
            normalizedPosition = arCam.normalizedPosition;
            // telefon ölçüleri
           /*  float cameraDistance = 2.0f;
            float aspect = Screen.width / Screen.height;
            float deviceWith = 0.07f;
            float deviceHeight = 0.12f;
            float faceWidth = 0.15f;
            float frontCameraFov = 74.0f;
             float backCameraFov = 70.0f; */

            // bilgisayar ölçüleri
            float cameraDistance = 2.0f;
            float aspect = Screen.width / Screen.height;
            float deviceWith = 0.30f;
            float deviceHeight = 0.15f;
            float faceWidth = 0.15f;
            float frontCameraFov = 60.0f;
            float backCameraFov = 90.0f;

            // yarım metre ön kameraya uzaksak
            float zFactor = faceWidth / (Mathf.Tan(frontCameraFov * 0.5f * Mathf.Deg2Rad));
            // kullanıcının kameraya olan uzaklığı
            float eyeZ = 0.5f * (zFactor / normalizedPosition.z);
            float eyeX = aspect * eyeZ * (Mathf.Atan(-normalizedPosition.x * (Mathf.Tan(frontCameraFov * 0.5f * Mathf.Deg2Rad))));
            float eyeY = eyeZ * (Mathf.Atan(normalizedPosition.y * (Mathf.Tan(frontCameraFov * 0.5f * Mathf.Deg2Rad))));
            float userDistance = cameraDistance + eyeZ;
            // user left
            float ul = eyeX + (-deviceWith / 2.0f - eyeX) * (userDistance) / (eyeZ);
            //user right
            float ur = eyeX + (deviceWith / 2.0f - eyeX) * (userDistance) / (eyeZ);
            // user top
            float ut = eyeY + (-eyeY) * (userDistance) / (eyeZ);
            //user bottom
            float ub = eyeY + (-deviceHeight - eyeY) * (userDistance) / (eyeZ);

            Mesh phoneMesh = plane.GetComponent<MeshFilter>().mesh;
            Vector2[] puv = new Vector2[phoneMesh.uv.Length];
            Array.Copy(phoneMesh.uv, puv, phoneMesh.uv.Length);

            float projectionWidth = 2 * Mathf.Tan(Mathf.Deg2Rad * backCameraFov / 2) * cameraDistance;
            puv[15] = new Vector2(ur / projectionWidth + 0.5f, ub / projectionWidth + 0.5f);
            puv[12] = new Vector2(ul / projectionWidth + 0.5f, ub / projectionWidth + 0.5f);
            puv[14] = new Vector2(ur / projectionWidth + 0.5f, ut / projectionWidth + 0.5f);
            puv[13] = new Vector2(ul / projectionWidth + 0.5f, ut / projectionWidth + 0.5f);

            //cube2.GetComponent<MeshFilter>().mesh.uv = puv;
            //plane.transform.localRotation = Quaternion.Euler(80f,15f,-10f);
            plane.GetComponent<MeshFilter>().mesh.uv = puv;
            

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

            // StartCoroutine(arkaKameraGoruntusuAl());
             plane.GetComponent<Renderer>().material.mainTexture = backWebcamTexture;
            /* cube.GetComponent<Renderer>().material.mainTexture = backWebcamTexture;
            cube2.GetComponent<Renderer>().material.mainTexture = backWebcamTexture; */
            // cube3.GetComponent<Renderer>().material.mainTexture = backWebcamTexture;
            arCam.TargetDetected();


            //StartCoroutine(cameraUpdate());


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
    WaitForSeconds delay = new WaitForSeconds(5.0f);
    IEnumerator cameraUpdate()
    {
        while (true)
        {
            StartCoroutine(arkaKameraGoruntusuAl());
            cube3.GetComponent<Renderer>().material.mainTexture = texCamShot;
            cube2.GetComponent<Renderer>().material.mainTexture = texCamShot;
            Texture2D brightBGTexture = new Texture2D(frontWebcamTexture.width, frontWebcamTexture.height);
            brightBGTexture.SetPixels32(frontWebcamTexture.GetPixels32(), 0);
            brightBGTexture.Apply();
            StartCoroutine(zamanliCalis(brightBGTexture));
            yield return delay;
        }
    }

    IEnumerator zamanliCalis(Texture2D brightBGTexture)
    {

        yield return StartCoroutine(KameraPozisyonariniBul(brightBGTexture));

        float cameraDistance = 2.0f;
        float aspect = Screen.width / Screen.height;
        float deviceWith = 0.07f;
        float deviceHeight = 0.12f;
        float faceWidth = 0.15f;
        float frontCameraFov = 74.0f;
        // yarım metre ön kameraya uzaksak
        float zFactor = faceWidth / (Mathf.Tan(frontCameraFov * 0.5f * Mathf.Deg2Rad));
        // kullanıcının kameraya olan uzaklığı
        float eyeZ = 0.5f * (zFactor / normalizedFace.z);
        float eyeX = aspect * eyeZ * (Mathf.Atan(-normalizedFace.x * (Mathf.Tan(frontCameraFov * 0.5f * Mathf.Deg2Rad))));
        float eyeY = eyeZ * (Mathf.Atan(normalizedFace.y * (Mathf.Tan(frontCameraFov * 0.5f * Mathf.Deg2Rad))));
        float userDistance = cameraDistance + eyeZ;
        // user left
        float ul = eyeX + (-deviceWith / 2.0f - eyeX) * (userDistance) / (eyeZ);
        //user right
        float ur = eyeX + (deviceWith / 2.0f - eyeX) * (userDistance) / (eyeZ);
        // user top
        float ut = eyeY + (-eyeY) * (userDistance) / (eyeZ);
        //user bottom
        float ub = eyeY + (-deviceHeight - eyeY) * (userDistance) / (eyeZ);

        Mesh phoneMesh = cube2.GetComponent<MeshFilter>().mesh;
        Vector2[] puv = new Vector2[phoneMesh.uv.Length];
        Array.Copy(phoneMesh.uv, puv, phoneMesh.uv.Length);

        float projectionWidth = 2 * Mathf.Tan(Mathf.Deg2Rad * 70 / 2) * cameraDistance;


        puv[15] = new Vector2(ur / projectionWidth + 0.5f, ub / projectionWidth + 0.5f);
        puv[12] = new Vector2(ul / projectionWidth + 0.5f, ub / projectionWidth + 0.5f);
        puv[14] = new Vector2(ur / projectionWidth + 0.5f, ut / projectionWidth + 0.5f);
        puv[13] = new Vector2(ul / projectionWidth + 0.5f, ut / projectionWidth + 0.5f);

        cube2.GetComponent<MeshFilter>().mesh.uv = puv;

        /*  puv[10] = new Vector2(ur / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
             puv[11] = new Vector2(ul / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
             puv[6] = new Vector2(ur / projectionWidth + eyeZ, ut / projectionWidth + eyeZ);
             puv[7] = new Vector2(ul / projectionWidth + eyeZ, ut / projectionWidth + eyeZ); */

        /*  puv[0] = new Vector2(ur / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
         puv[1] = new Vector2(ul / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
         puv[2] = new Vector2(ur / projectionWidth + eyeZ, ut / projectionWidth + eyeZ);
         puv[3] = new Vector2(ul / projectionWidth + eyeZ, ut / projectionWidth + eyeZ); */

        /*  puv[16] = new Vector2(ul / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
        puv[18] = new Vector2(ur / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
        puv[19] = new Vector2(ul / projectionWidth + eyeZ, ut / projectionWidth + eyeZ);
        puv[17] = new Vector2(ur / projectionWidth + eyeZ, ut / projectionWidth + eyeZ); */

        /*  puv[20] = new Vector2(ur / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
        puv[22] = new Vector2(ul / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
        puv[23] = new Vector2(ur / projectionWidth + eyeZ, ut / projectionWidth + eyeZ);
        puv[21] = new Vector2(ul / projectionWidth + eyeZ, ut / projectionWidth + eyeZ);  */

        /* puv[8] = new Vector2(ur / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
        puv[9] = new Vector2(ul / projectionWidth + eyeZ, ub / projectionWidth + eyeZ);
        puv[4] = new Vector2(ur / projectionWidth + eyeZ, ut / projectionWidth + eyeZ);
        puv[5] = new Vector2(ul / projectionWidth + eyeZ, ut / projectionWidth + eyeZ); */

        /*  puv[15] = new Vector2(2.0f, 0.0f);
         puv[12] = new Vector2(0.0f,0.0f);
         puv[14] = new Vector2(2.0f ,2.0f);
         puv[13] = new Vector2(0.0f,2.0f); */



        // pixel format
        /* float pul = (ul + frustumWidth / 2.0f) * Screen.width / frustumWidth;
        float pur = (ur + frustumWidth / 2.0f) * Screen.width / frustumWidth;
        float put = (ut + frustumHeight / 2.0f) * Screen.height / frustumHeight;
        float pub = (ub + frustumHeight / 2.0f) * Screen.height / frustumHeight;
 */
        //float userFov = Mathf.Atan(deviceWith / deviceHeight) * Mathf.Rad2Deg * 2f;
        /* float userFrustumHeight = 2.0f * userDistance * Mathf.Tan(70.f * 0.5f * Mathf.Deg2Rad);
        float deviceAspect = deviceWith/deviceHeight;
        float userFrustumWidth = userFrustumHeight * deviceAspect; */
        // B) (pul,pub) (pur,pub) (pul,put) (pur,put)
        /* cameraPositionDestination[0].x = pul;
        cameraPositionDestination[0].y = pub;
        cameraPositionDestination[1].x = pur;
        cameraPositionDestination[1].y = pub;
        cameraPositionDestination[2].x = pul;
        cameraPositionDestination[2].y = put;
        cameraPositionDestination[3].x = pur;
        cameraPositionDestination[3].y = put; */



        // matrislerin yeri değişebilir.
        /*  FindHomography(cameraPositionSource,cameraPositionDestination);
        // FindHomography(ref cameraPositionDestination, ref cameraPositionSource, ref matrix);

         float[,] cubeCurrentPosition = { { cube.transform.position.x, cube.transform.position.y, cube.transform.position.z } };
         float[,] cube2CurrentPosition = { { cube2.transform.position.x, cube2.transform.position.y, cube2.transform.position.z } };
         float[,] homographyMatrixPosition = { { homographyMatrix[0].x, homographyMatrix[0].y, homographyMatrix[0].z }, { homographyMatrix[1].x, homographyMatrix[1].y, homographyMatrix[1].z }, { homographyMatrix[2].x, homographyMatrix[2].y, homographyMatrix[2].z } };
         float[,] cubeNewPosition = new float[1, 3];
         float[,] cube2NewPosition = new float[1, 3];
         for (int i = 0; i < 1; i++)
         {
             for (int j = 0; j < 3; j++)
             {
                 cubeNewPosition[i, j] = 0;
                 cube2NewPosition[i, j] = 0;
                 for (int k = 0; k < 3; k++) {
                     cubeNewPosition[i, j] = cubeNewPosition[i, j] + cubeCurrentPosition[i, k] * homographyMatrixPosition[k, j];
                     cube2NewPosition[i, j] = cube2NewPosition[i, j] + cube2CurrentPosition[i, k] * homographyMatrixPosition[k, j];
                 }
             }
         }
         //camera1.fov=userFov;
         Vector3 newCubePosition = new Vector3(cubeNewPosition[0, 0], cubeNewPosition[0, 1], cubeNewPosition[0, 2]);
         Vector3 newCube2Position = new Vector3(cube2NewPosition[0, 0], cube2NewPosition[0, 1], cube2NewPosition[0, 2]);
         cube.transform.position = newCubePosition;
         cube2.transform.position= newCube2Position; */


        /* Vector3 targetLocation = new Vector3(faceX, 0, -5);
       Vector3 vectorToTarget = -targetLocation;
       camera1.transform.localPosition = targetLocation; */
        /* Quaternion rot = Quaternion.LookRotation(forward: vectorToTarget.normalized, upwards: -Vector3.up);
        camera1.transform.localRotation=rot; */
        // camera1.transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, turnSpeed * Time.deltaTime);
        /*  cameraPositionSource[0].x = 0;
       cameraPositionSource[0].y = 0;
       cameraPositionSource[1].x = backWebcamTexture.width;
       cameraPositionSource[1].y = 0;
       cameraPositionSource[2].x = 0;
       cameraPositionSource[2].y = backWebcamTexture.height;
       cameraPositionSource[3].x = backWebcamTexture.width;
       cameraPositionSource[3].y = backWebcamTexture.height; */

        //imageTarget.transform.position = ExtractPosition(homographyMatrix);
        //cube.transform.localScale = ExtractScale(homographyMatrix);
        // cube.transform.rotation = ExtractRotation(homographyMatrix);
        //cube.transform.position = ExtractPosition(homographyMatrix);
        // cube2.transform.position = ExtractPosition(homographyMatrix); 



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
        Vector3[] h = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f) };
        h[0] = new Vector3((float)rsv[0], (float)rsv[1], (float)rsv[2]);
        h[1] = new Vector3((float)rsv[3], (float)rsv[4], (float)rsv[5]);
        h[2] = new Vector3((float)rsv[6], (float)rsv[7], (float)rsv[8]);
        /*  h.SetRow(0, new Vector3((float)rsv[0], (float)rsv[1], (float)rsv[2]));
        h.SetRow(1, new Vector3((float)rsv[3], (float)rsv[4], (float)rsv[5]));
        h.SetRow(2, new Vector3((float)rsv[6], (float)rsv[7], (float)rsv[8]));  */
        homographyMatrix = h;
    }
    IEnumerator KameraPozisyonariniBul(Texture2D brightBGTexture)
    {
        GameObject obj = new GameObject();
        obj.AddComponent<CloudFaceManager>();
        CloudFaceManager cloudFaceManager = obj.GetComponent<CloudFaceManager>();
        yield return StartCoroutine(cloudFaceManager.DetectFaces(brightBGTexture, cube));
        normalizedFace = cloudFaceManager.normalizedFace;
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


    void FindHomography(ref Vector3[] src, ref Vector3[] dest, ref float[] homography)
    {
        float[,] P = new float[,]{
         {-src[0].x, -src[0].y, -1,   0,   0,  0, src[0].x*dest[0].x, src[0].y*dest[0].x, -dest[0].x }, // h11  
         {  0,   0,  0, -src[0].x, -src[0].y, -1, src[0].x*dest[0].y, src[0].y*dest[0].y, -dest[0].y }, // h12  

         {-src[1].x, -src[1].y, -1,   0,   0,  0, src[1].x*dest[1].x, src[1].y*dest[1].x, -dest[1].x }, // h13  
         {  0,   0,  0, -src[1].x, -src[1].y, -1, src[1].x*dest[1].y, src[1].y*dest[1].y, -dest[1].y }, // h21  

         {-src[2].x, -src[2].y, -1,   0,   0,  0, src[2].x*dest[2].x, src[2].y*dest[2].x, -dest[2].x }, // h22  
         {  0,   0,  0, -src[2].x, -src[2].y, -1, src[2].x*dest[2].y, src[2].y*dest[2].y, -dest[2].y }, // h23  

         {-src[3].x, -src[3].y, -1,   0,   0,  0, src[3].x*dest[3].x, src[3].y*dest[3].x, -dest[3].x }, // h31  
         {  0,   0,  0, -src[3].x, -src[3].y, -1, src[3].x*dest[3].y, src[3].y*dest[3].y, -dest[3].y }, // h32  
         };

        GaussianElimination(ref P, 9);

        // gaussian elimination gives the results of the equation system  
        // in the last column of the original matrix.  
        // opengl needs the transposed 4x4 matrix:  
        float[] aux_H ={ P[0,8],P[3,8],0,P[6,8], // h11  h21 0 h31  
             P[1,8],P[4,8],0,P[7,8], // h12  h22 0 h32  
             0      ,      0,0,0,       // 0    0   0 0  
             P[2,8],P[5,8],0,1};      // h13  h23 0 h33  

        for (int i = 0; i < 16; i++) homography[i] = aux_H[i];

    }



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
