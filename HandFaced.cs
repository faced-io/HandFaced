using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement; 
using System.Collections;
using System.Collections.Generic;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.UnityUtils;
using DlibFaceLandmarkDetector; 

namespace OpenCVForUnityExample
{
    /// <summary>
    /// Hand Pose Estimation Example
    /// Referring to https://www.youtube.com/watch?v=KuGpOxOcpds.
    /// </summary>
    [RequireComponent(typeof(WebCamTextureToMatHelper), typeof(ImageOptimizationHelper))]
    public class HandFaced : MonoBehaviour
    {

        public static bool enableDownScale = true;

        ImageOptimizationHelper imageOptimizationHelper;

        /// <summary>
        /// The number of fingers text.
        /// </summary>
        public static int numberOfFingers = 0;

        /// <summary>
        /// The threashold slider.
        /// </summary>
        public static int threshholdDetect = 8700;

        /// <summary>
        /// The texture.
        /// </summary>
        Texture2D texture;

        /// <summary>
        /// The BLOB color hsv.
        /// </summary>
        Scalar blobColorHsv;

        ///// <summary>
        ///// The BLOB color rgba.
        ///// </summary>
        //Scalar blobColorRgba;

        /// <summary>
        /// The detector.
        /// </summary>
        ColorBlobDetector detector;

        /// <summary>
        /// The spectrum mat.
        /// </summary>
        Mat spectrumMat;

        /// <summary>
        /// Indicates whether is color selected.
        /// </summary>
        public static bool isColorSelected = false;

        /// <summary>
        /// The spectrum size.
        /// </summary>
        Size SPECTRUM_SIZE;

        /// <summary>
        /// The contour color.
        /// </summary>
        Scalar CONTOUR_COLOR;

        /// <summary>
        /// The contour color white.
        /// </summary>
        Scalar CONTOUR_COLOR_WHITE;
         

        /// <summary>
        /// The webcam texture to mat helper.
        /// </summary>
        WebCamTextureToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The stored touch point.
        /// </summary>
        Point storedTouchPoint;

        /// <summary>
        /// The FPS monitor.
        /// </summary>
        FpsMonitor fpsMonitor;

        // Use this for initialization
        void Start()
        {
            fpsMonitor = GetComponent<FpsMonitor>();

            imageOptimizationHelper = gameObject.GetComponent<ImageOptimizationHelper>();

            webCamTextureToMatHelper = gameObject.GetComponent<WebCamTextureToMatHelper>();

#if UNITY_ANDROID && !UNITY_EDITOR
            // Avoids the front camera low light issue that occurs in only some Android devices (e.g. Google Pixel, Pixel2).
            webCamTextureToMatHelper.avoidAndroidFrontCameraLowLightIssue = true;
#endif
            webCamTextureToMatHelper.Initialize();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized()
        {
            Debug.Log("OnWebCamTextureToMatHelperInitialized");

            Mat webCamTextureMat = webCamTextureToMatHelper.GetMat();
            grayMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC1);
            texture = new Texture2D(webCamTextureMat.cols(), webCamTextureMat.rows(), TextureFormat.RGBA32, false);

            gameObject.GetComponent<Renderer>().material.mainTexture = texture;

            gameObject.transform.localScale = new Vector3(webCamTextureMat.cols(), webCamTextureMat.rows(), 1);

            Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

            if (fpsMonitor != null)
            {
                fpsMonitor.Add("width", webCamTextureMat.width().ToString());
                fpsMonitor.Add("height", webCamTextureMat.height().ToString());
                fpsMonitor.Add("orientation", Screen.orientation.ToString());
                fpsMonitor.consoleText = "Please touch the area of the open hand.";
            }


            float width = webCamTextureMat.width();
            float height = webCamTextureMat.height();

            float widthScale = (float)Screen.width / width;
            float heightScale = (float)Screen.height / height;
            if (widthScale < heightScale)
            {
                Camera.main.orthographicSize = (width * (float)Screen.height / (float)Screen.width) / 2;
            }
            else
            {
                Camera.main.orthographicSize = height / 2;
            }

            detector = new ColorBlobDetector();
            spectrumMat = new Mat();
            //blobColorRgba = new Scalar (255);
            blobColorHsv = new Scalar(255);
            SPECTRUM_SIZE = new Size(200, 64);
            CONTOUR_COLOR = new Scalar(255, 0, 0, 255);
            CONTOUR_COLOR_WHITE = new Scalar(255, 255, 255, 255);

            Initdlib();
        }

        void Initdlib()
        {
          //  dlibShapePredictorFileName = DlibFaceLandmarkDetectorExample.dlibShapePredictorFileName;
#if UNITY_WEBGL && !UNITY_EDITOR
            getFilePath_Coroutine = Utils.getFilePathAsync (dlibShapePredictorFileName, (result) => {
                getFilePath_Coroutine = null;

                dlibShapePredictorFilePath = result;
                Run ();
            });
            StartCoroutine (getFilePath_Coroutine);
#else
            dlibShapePredictorFilePath = Utils.getFilePath(dlibShapePredictorFileName);
            Run();
#endif 

        }

        void Run()
        {
            if (string.IsNullOrEmpty(dlibShapePredictorFilePath))
            {
                Debug.LogError("shape predictor file does not exist. Please copy from “DlibFaceLandmarkDetector/StreamingAssets/” to “Assets/StreamingAssets/” folder. ");
            }

            faceLandmarkDetector = new FaceLandmarkDetector(dlibShapePredictorFilePath);
        }

        /// <summary>
        /// The dlib shape predictor file name.
        /// </summary>
        string dlibShapePredictorFileName = "sp_human_face_68.dat";

        /// <summary>
        /// The dlib shape predictor file path.
        /// </summary>
        string dlibShapePredictorFilePath;

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed()
        {
            Debug.Log("OnWebCamTextureToMatHelperDisposed");

            if (spectrumMat != null)
            {
                spectrumMat.Dispose();
                spectrumMat = null;
            }
            if (texture != null)
            {
                Texture2D.Destroy(texture);
                texture = null;
            }
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode)
        {
            Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

        // Update is called once per frame
        void Update()
        {
#if ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR)
            //Touch
            int touchCount = Input.touchCount;
            if (touchCount == 1)
            {
                Touch t = Input.GetTouch (0);
                if(t.phase == TouchPhase.Ended && !EventSystem.current.IsPointerOverGameObject (t.fingerId)) {
                    storedTouchPoint = new Point (t.position.x, t.position.y);
                    //Debug.Log ("touch X " + t.position.x);
                    //Debug.Log ("touch Y " + t.position.y);
                }
            }
#else
            //Mouse
            if (Input.GetMouseButtonUp(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                storedTouchPoint = new Point(Input.mousePosition.x, Input.mousePosition.y);
                //Debug.Log ("mouse X " + Input.mousePosition.x);
                //Debug.Log ("mouse Y " + Input.mousePosition.y);
            }
#endif

            if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {

                if (!enableSkipFrame || !imageOptimizationHelper.IsCurrentFrameSkipped())
                {

                    Mat rgbaMat = webCamTextureToMatHelper.GetMat();
                    mRgba = new Mat();
                    if (storedTouchPoint != null)
                    {
                        ConvertScreenPointToTexturePoint(storedTouchPoint, storedTouchPoint, gameObject, rgbaMat.cols(), rgbaMat.rows());
                        OnTouch(rgbaMat, storedTouchPoint);
                        storedTouchPoint = null;
                    }

                    FaceLM(rgbaMat);

                    HandPoseEstimationProcess(rgbaMat);


                    Utils.fastMatToTexture2D(rgbaMat, texture);

                    //                Imgproc.putText (rgbaMat, "Please touch the area of the open hand.", new Point (5, rgbaMat.rows () - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar (255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
                }
                
            }
        }
        public bool enableSkipFrame = true;
        List<Vector2> facePoints; Mat grayMat;

        void FaceLM(Mat rgbaMat)
        {
            Mat downScaleRgbaMat = null;
            float DOWNSCALE_RATIO = 1.0f;
            if (enableDownScale)
            {
                downScaleRgbaMat = imageOptimizationHelper.GetDownScaleMat(rgbaMat);
                DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
            }
            else
            {
                downScaleRgbaMat = rgbaMat;
                DOWNSCALE_RATIO = 1.0f;
            }

            OpenCVForUnityUtils.SetImage(faceLandmarkDetector, downScaleRgbaMat);
         //   Imgproc.cvtColor(downScaleRgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
            List<UnityEngine.Rect> detectionResult = faceLandmarkDetector.Detect();

            if (detectionResult.Count > 0)
            {
                if (enableDownScale)
                {
                    for (int i = 0; i < detectionResult.Count; ++i)
                    {
                        var rect = detectionResult[i];
                        detectionResult[i] = new UnityEngine.Rect(
                            rect.x * DOWNSCALE_RATIO,
                            rect.y * DOWNSCALE_RATIO,
                            rect.width * DOWNSCALE_RATIO,
                            rect.height * DOWNSCALE_RATIO);
                    }
                }

                OpenCVForUnityUtils.SetImage(faceLandmarkDetector, rgbaMat);

                //detect landmark points
                facePoints = faceLandmarkDetector.DetectLandmark(detectionResult[0]);

                OpenCVForUnityUtils.DrawFaceLandmark(rgbaMat, facePoints, new Scalar(0, 255, 0, 255), 1,false   ,true);

                
            }
        }

        FaceLandmarkDetector faceLandmarkDetector;

        Mat mRgba;

        private void HandPoseEstimationProcess(Mat rgbaMat)
        {

           // rgbaMat.copyTo(mRgba);
            float DOWNSCALE_RATIO = 1.0f;
            if (enableDownScale)
            {
                mRgba = imageOptimizationHelper.GetDownScaleMat(rgbaMat);
                DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
            }
            else
            {
                // mRgba = rgbaMat;
                rgbaMat.copyTo(mRgba);
                DOWNSCALE_RATIO = 1.0f;
            }
             
             // Imgproc.blur(mRgba, mRgba, new Size(5,5));
            Imgproc.GaussianBlur(mRgba, mRgba, new Size(3, 3), 1, 1);
           // Imgproc.medianBlur(mRgba, mRgba, 3);
             

            if (!isColorSelected)
                return;

            List<MatOfPoint> contours = detector.GetContours();
            detector.Process(mRgba);

            //            Debug.Log ("Contours count: " + contours.Count);

            if (contours.Count <= 0)
            {
                return;
            }

            RotatedRect rect = Imgproc.minAreaRect(new MatOfPoint2f(contours[0].toArray()));

            double boundWidth = rect.size.width;
            double boundHeight = rect.size.height;
            int boundPos = 0;

            for (int i = 1; i < contours.Count; i++)
            {
                rect = Imgproc.minAreaRect(new MatOfPoint2f(contours[i].toArray()));
                if (rect.size.width * rect.size.height > boundWidth * boundHeight)
                {
                    boundWidth = rect.size.width;
                    boundHeight = rect.size.height;
                    boundPos = i;
                }
            }

            MatOfPoint contour = contours[boundPos];

            OpenCVForUnity.CoreModule.Rect boundRect = Imgproc.boundingRect(new MatOfPoint(contour.toArray()));
            Imgproc.rectangle(mRgba, boundRect.tl(), boundRect.br(), CONTOUR_COLOR_WHITE, 2, 8, 0);

            //            Debug.Log (
            //                " Row start [" + 
            //                    (int)boundRect.tl ().y + "] row end [" +
            //                    (int)boundRect.br ().y + "] Col start [" +
            //                    (int)boundRect.tl ().x + "] Col end [" +
            //                    (int)boundRect.br ().x + "]");


            double a = boundRect.br().y - boundRect.tl().y;
            a = a * 0.7;
            a = boundRect.tl().y + a;

            //            Debug.Log (" A [" + a + "] br y - tl y = [" + (boundRect.br ().y - boundRect.tl ().y) + "]");

           // Imgproc.rectangle(mRgba, boundRect.tl(), new Point(boundRect.br().x, a), CONTOUR_COLOR, 2, 8, 0);

            MatOfPoint2f pointMat = new MatOfPoint2f();
            Imgproc.approxPolyDP(new MatOfPoint2f(contour.toArray()), pointMat, 3, true);
            contour = new MatOfPoint(pointMat.toArray());

            MatOfInt hull = new MatOfInt();
            MatOfInt4 convexDefect = new MatOfInt4();
            Imgproc.convexHull(new MatOfPoint(contour.toArray()), hull);

            if (hull.toArray().Length < 3)
                return;

            Imgproc.convexityDefects(new MatOfPoint(contour.toArray()), hull, convexDefect);

            List<MatOfPoint> hullPoints = new List<MatOfPoint>();
            List<Point> listPo = new List<Point>();
            for (int j = 0; j < hull.toList().Count; j++)
            {
                listPo.Add(contour.toList()[hull.toList()[j]] * DOWNSCALE_RATIO);
            }

            /*
            MatOfPoint e = new MatOfPoint();
            e.fromList(listPo);
            hullPoints.Add(e);

            List<Point> listPoDefect = new List<Point>();

           if (convexDefect.rows() > 0)
            {
                List<int> convexDefectList = convexDefect.toList();
                List<Point> contourList = contour.toList();
                for (int j = 0; j < convexDefectList.Count; j = j + 4)
                {
                    Point farPoint = contourList[convexDefectList[j + 2]];
                    int depth = convexDefectList[j + 3];
                    if (depth > threshholdDetect && farPoint.y < a)
                    {
                        listPoDefect.Add(contourList[convexDefectList[j + 2]]);
                        Imgproc.line(rgbaMat, farPoint, listPo[convexDefectList[j + 2]], new Scalar(255, 0, 0, 255),1,1);
                    }
                    //                    Debug.Log ("convexDefectList [" + j + "] " + convexDefectList [j + 3]);
                }
            }*/


            //            Debug.Log ("hull: " + hull.toList ());
            //            if (convexDefect.rows () > 0) {
            //                Debug.Log ("defects: " + convexDefect.toList ());
            //            }

            //Imgproc.drawContours (rgbaMat, hullPoints, -1, CONTOUR_COLOR, 3);
             
            for (int p=0;p <   listPo.Count;p++)
            {
                if (p % 2 == 0)
                {
                    Imgproc.circle(rgbaMat, listPo[p], 6, new Scalar(255, 0, 0, 255), -1);
                    // Imgproc.putText(rgbaMat,p.ToString(),listPo[p],1,1,new Scalar(255,0,0,255));

                    // check if close

                    List<Point> fLMscaled = OpenCVForUnityUtils.ConvertVector2ListToPointList(facePoints);

                    for (int q = 0; q < fLMscaled.Count; q++) {
                        if (ifLessThanDPoint(listPo[p],fLMscaled[q],8))
                        {
                            //Point1 = listPo[p];
                            //Point2 = fLMscaled[q];
                            handPoint = p;
                            facePoint = q;
                            print(Point1 + " " + Point2);
                        }
                    }

                    if (p == handPoint && facePoint!=0)
                    {
                        Point1 = listPo[p];
                        Point2 = fLMscaled[facePoint];
                        Imgproc.line(rgbaMat, Point1, Point2, new Scalar(255, 255, 255, 255));
                    }

                }

            }


            


            //            int defectsTotal = (int)convexDefect.total();
            //            Debug.Log ("Defect total " + defectsTotal);

          /*  numberOfFingers = listPoDefect.Count;
            if (numberOfFingers > 5)
                numberOfFingers = 5;/

            //            Debug.Log ("numberOfFingers " + numberOfFingers);

            //            Imgproc.putText (rgbaMat, "" + numberOfFingers, new Point (rgbaMat.cols () / 2, rgbaMat.rows () / 2), Imgproc.FONT_HERSHEY_PLAIN, 4.0, new Scalar (255, 255, 255, 255), 6, Imgproc.LINE_AA, false);

             
         /*   foreach (Point p in listPoDefect) {

                Point tempp = GetNearestL(p, listPo);
                tempp = ConvertDownscale(tempp, DOWNSCALE_RATIO);
                Point p2 = ConvertDownscale(p, DOWNSCALE_RATIO);

                Imgproc.circle (rgbaMat, tempp, 6, new Scalar (0, 0, 255, 255), -1);
                Imgproc.circle(rgbaMat, p2, 6, new Scalar(255, 0, 255, 255), -1);
            }*/

        }

        int handPoint; int facePoint;
        public Point Point1; public Point Point2;

        Point ConvertDownscale(Point p, float d)
        {
            return p * d;
        }

        Point GetNearestL(Point p,List<Point> l)
        {
            float smallest = 1000; int n = 0;
            for(int i = 0; i < l.Count; i++)
            {
                float s = pointdistance(p, l[i]);
                if (s < smallest)
                {
                    if (p.y < l[i].y)
                    {
                        smallest = s;
                        n = i;
                    }
                }
            }
            return l[n];
        }

        bool ifLessThanDPoint(Point a,Point b,float t)
        {
            if(pointdistance(a,b)<t)return true;
            return false;
        }

        float pointdistance(Point a,Point b)
        {
            return Mathf.Abs(Vector2.Distance(Point2Vector2(a), Point2Vector2(b)));
        }

        Vector2 Point2Vector2(Point p)
        {
            return new Vector2((float)p.x, (float)p.y);
        }

        private void OnTouch(Mat img, Point touchPoint)
        {
            int cols = img.cols();
            int rows = img.rows();

            int x = (int)touchPoint.x;
            int y = (int)touchPoint.y;

            //Debug.Log ("Touch image coordinates: (" + x + ", " + y + ")");

            if ((x < 0) || (y < 0) || (x > cols) || (y > rows))
                return;

            OpenCVForUnity.CoreModule.Rect touchedRect = new OpenCVForUnity.CoreModule.Rect();

            touchedRect.x = (x > 5) ? x - 5 : 0;
            touchedRect.y = (y > 5) ? y - 5 : 0;

            touchedRect.width = (x + 5 < cols) ? x + 5 - touchedRect.x : cols - touchedRect.x;
            touchedRect.height = (y + 5 < rows) ? y + 5 - touchedRect.y : rows - touchedRect.y;

            using (Mat touchedRegionRgba = img.submat(touchedRect))
            using (Mat touchedRegionHsv = new Mat())
            {
                Imgproc.cvtColor(touchedRegionRgba, touchedRegionHsv, Imgproc.COLOR_RGB2HSV_FULL);

                // Calculate average color of touched region
                blobColorHsv = Core.sumElems(touchedRegionHsv);
                int pointCount = touchedRect.width * touchedRect.height;
                for (int i = 0; i < blobColorHsv.val.Length; i++)
                    blobColorHsv.val[i] /= pointCount;

                //blobColorRgba = ConverScalarHsv2Rgba (blobColorHsv);            
                //Debug.Log ("Touched rgba color: (" + mBlobColorRgba.val [0] + ", " + mBlobColorRgba.val [1] +
                //  ", " + mBlobColorRgba.val [2] + ", " + mBlobColorRgba.val [3] + ")");

                detector.SetHsvColor(blobColorHsv);

                Imgproc.resize(detector.GetSpectrum(), spectrumMat, SPECTRUM_SIZE);

                isColorSelected = true;
            }
        }

        private Scalar ConverScalarHsv2Rgba(Scalar hsvColor)
        {
            Scalar rgbaColor;
            using (Mat pointMatRgba = new Mat())
            using (Mat pointMatHsv = new Mat(1, 1, CvType.CV_8UC3, hsvColor))
            {
                Imgproc.cvtColor(pointMatHsv, pointMatRgba, Imgproc.COLOR_HSV2RGB_FULL, 4);
                rgbaColor = new Scalar(pointMatRgba.get(0, 0));
            }

            return rgbaColor;
        }

        /// <summary>
        /// Converts the screen point to texture point.
        /// </summary>
        /// <param name="screenPoint">Screen point.</param>
        /// <param name="dstPoint">Dst point.</param>
        /// <param name="texturQuad">Texture quad.</param>
        /// <param name="textureWidth">Texture width.</param>
        /// <param name="textureHeight">Texture height.</param>
        /// <param name="camera">Camera.</param>
        private void ConvertScreenPointToTexturePoint(Point screenPoint, Point dstPoint, GameObject textureQuad, int textureWidth = -1, int textureHeight = -1, Camera camera = null)
        {
            if (textureWidth < 0 || textureHeight < 0)
            {
                Renderer r = textureQuad.GetComponent<Renderer>();
                if (r != null && r.material != null && r.material.mainTexture != null)
                {
                    textureWidth = r.material.mainTexture.width;
                    textureHeight = r.material.mainTexture.height;
                }
                else
                {
                    textureWidth = (int)textureQuad.transform.localScale.x;
                    textureHeight = (int)textureQuad.transform.localScale.y;
                }
            }

            if (camera == null)
                camera = Camera.main;

            Vector3 quadPosition = textureQuad.transform.localPosition;
            Vector3 quadScale = textureQuad.transform.localScale;

            Vector2 tl = camera.WorldToScreenPoint(new Vector3(quadPosition.x - quadScale.x / 2, quadPosition.y + quadScale.y / 2, quadPosition.z));
            Vector2 tr = camera.WorldToScreenPoint(new Vector3(quadPosition.x + quadScale.x / 2, quadPosition.y + quadScale.y / 2, quadPosition.z));
            Vector2 br = camera.WorldToScreenPoint(new Vector3(quadPosition.x + quadScale.x / 2, quadPosition.y - quadScale.y / 2, quadPosition.z));
            Vector2 bl = camera.WorldToScreenPoint(new Vector3(quadPosition.x - quadScale.x / 2, quadPosition.y - quadScale.y / 2, quadPosition.z));

            using (Mat srcRectMat = new Mat(4, 1, CvType.CV_32FC2))
            using (Mat dstRectMat = new Mat(4, 1, CvType.CV_32FC2))
            {
                srcRectMat.put(0, 0, tl.x, tl.y, tr.x, tr.y, br.x, br.y, bl.x, bl.y);
                dstRectMat.put(0, 0, 0, 0, quadScale.x, 0, quadScale.x, quadScale.y, 0, quadScale.y);

                using (Mat perspectiveTransform = Imgproc.getPerspectiveTransform(srcRectMat, dstRectMat))
                using (MatOfPoint2f srcPointMat = new MatOfPoint2f(screenPoint))
                using (MatOfPoint2f dstPointMat = new MatOfPoint2f())
                {
                    Core.perspectiveTransform(srcPointMat, dstPointMat, perspectiveTransform);

                    dstPoint.x = dstPointMat.get(0, 0)[0] * textureWidth / quadScale.x;
                    dstPoint.y = dstPointMat.get(0, 0)[1] * textureHeight / quadScale.y;
                }
            }
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy()
        {
           
            if (faceLandmarkDetector != null)
                faceLandmarkDetector.Dispose();

            webCamTextureToMatHelper.Dispose();

            if (detector != null)
                detector.Dispose();
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick()
        {
            SceneManager.LoadScene("OpenCVForUnityExample");
        }

        /// <summary>
        /// Raises the play button click event.
        /// </summary>
        public void OnPlayButtonClick()
        {
            webCamTextureToMatHelper.Play();
        }

        /// <summary>
        /// Raises the pause button click event.
        /// </summary>
        public void OnPauseButtonClick()
        {
            webCamTextureToMatHelper.Pause();
        }

        /// <summary>
        /// Raises the stop button click event.
        /// </summary>
        public void OnStopButtonClick()
        {
            webCamTextureToMatHelper.Stop();
        }

        /// <summary>
        /// Raises the change camera button click event.
        /// </summary>
        public void OnChangeCameraButtonClick()
        {
            webCamTextureToMatHelper.requestedIsFrontFacing = !webCamTextureToMatHelper.IsFrontFacing();
        }
    }
}
