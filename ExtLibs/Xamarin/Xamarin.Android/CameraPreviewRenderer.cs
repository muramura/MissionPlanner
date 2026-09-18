using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Content;
using MissionPlanner.Controls;
using MissionPlanner.Droid;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using AndroidCamera = Android.Hardware.Camera2.CameraDevice;
using AndroidLog = Android.Util.Log;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(CameraPreviewRenderer))]

namespace MissionPlanner.Droid
{
    public class CameraPreviewRenderer : ViewRenderer<CameraPreview, FrameLayout>, TextureView.ISurfaceTextureListener
    {
        private readonly Context _context;
        private TextureView _textureView;
        private TextView _statusTextView;
        private FrameLayout _rootLayout;

        private CameraManager _cameraManager;
        private CameraDevice _cameraDevice;
        private CameraCaptureSession _captureSession;
        private CaptureRequest.Builder _previewRequestBuilder;

        private HandlerThread _backgroundThread;
        private Handler _backgroundHandler;

        private bool _isCameraOpening;
        private bool _isDisposed;
        private string _cameraId;

        public CameraPreviewRenderer(Context context) : base(context)
        {
            _context = context;
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement != null)
            {
                if (Control == null)
                {
                    InitializeNativeViews();
                    SetNativeControl(_rootLayout);
                }

                UpdateCameraState();
            }
        }

        private void InitializeNativeViews()
        {
            _rootLayout = new FrameLayout(_context)
            {
                LayoutParameters = new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent)
            };

            _textureView = new TextureView(_context)
            {
                LayoutParameters = new FrameLayout.LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent),
                SurfaceTextureListener = this
            };
            _rootLayout.AddView(_textureView);

            _statusTextView = new TextView(_context)
            {
                LayoutParameters = new FrameLayout.LayoutParams(LayoutParams.WrapContent, LayoutParams.WrapContent)
                {
                    Gravity = GravityFlags.Center
                },
                Text = "Starting Camera...",
                TextSize = 11,
                Visibility = ViewStates.Visible
            };
            _statusTextView.SetTextColor(Android.Graphics.Color.LightGray);
            _rootLayout.AddView(_statusTextView);
        }

        protected override void OnElementPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            if (e.PropertyName == CameraPreview.IsCameraActiveProperty.PropertyName)
            {
                UpdateCameraState();
            }
        }

        private void UpdateCameraState()
        {
            if (Element == null) return;

            if (Element.IsCameraActive)
            {
                if (_textureView != null && _textureView.IsAvailable)
                {
                    OpenCamera(_textureView.Width, _textureView.Height);
                }
            }
            else
            {
                CloseCamera();
            }
        }

        #region TextureView.ISurfaceTextureListener

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            ConfigureTransform(width, height);
            if (Element != null && Element.IsCameraActive)
            {
                OpenCamera(width, height);
            }
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            ConfigureTransform(width, height);
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            CloseCamera();
            return true;
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            if (_statusTextView != null && _statusTextView.Visibility != ViewStates.Gone)
            {
                Post(() =>
                {
                    if (_statusTextView != null)
                    {
                        _statusTextView.Visibility = ViewStates.Gone;
                    }
                });
            }
        }

        #endregion

        #region Camera Management

        private void StartBackgroundThread()
        {
            if (_backgroundThread != null) return;

            _backgroundThread = new HandlerThread("CameraBackgroundThread");
            _backgroundThread.Start();
            _backgroundHandler = new Handler(_backgroundThread.Looper);
        }

        private void StopBackgroundThread()
        {
            if (_backgroundThread == null) return;

            try
            {
                _backgroundThread.QuitSafely();
                _backgroundThread.Join();
            }
            catch (Exception ex)
            {
                AndroidLog.Warn("CameraPreviewRenderer", "StopBackgroundThread error: " + ex.Message);
            }
            finally
            {
                _backgroundThread = null;
                _backgroundHandler = null;
            }
        }

        private void OpenCamera(int width, int height)
        {
            if (_isCameraOpening || _cameraDevice != null || _isDisposed) return;

            if (ContextCompat.CheckSelfPermission(_context, Android.Manifest.Permission.Camera) != Android.Content.PM.Permission.Granted)
            {
                SetStatusText("Camera permission required");
                Element?.NotifyError("Camera permission required");
                return;
            }

            StartBackgroundThread();

            try
            {
                _isCameraOpening = true;
                _cameraManager = (CameraManager)_context.GetSystemService(Context.CameraService);
                if (_cameraManager == null)
                {
                    SetStatusText("CameraManager not available");
                    _isCameraOpening = false;
                    return;
                }

                _cameraId = SelectCameraId(_cameraManager);
                if (string.IsNullOrEmpty(_cameraId))
                {
                    SetStatusText("No camera found");
                    _isCameraOpening = false;
                    return;
                }

                _cameraManager.OpenCamera(_cameraId, new CameraStateCallback(this), _backgroundHandler);
            }
            catch (Exception ex)
            {
                AndroidLog.Error("CameraPreviewRenderer", "OpenCamera exception: " + ex);
                SetStatusText("Camera error: " + ex.Message);
                Element?.NotifyError(ex.Message);
                _isCameraOpening = false;
            }
        }

        private string SelectCameraId(CameraManager manager)
        {
            var desiredFacing = (Element != null && Element.CameraFacing == CameraFacingOption.Front)
                ? LensFacing.Front
                : LensFacing.Back;

            foreach (var id in manager.GetCameraIdList())
            {
                var characteristics = manager.GetCameraCharacteristics(id);
                var facing = (LensFacing)(int)characteristics.Get(CameraCharacteristics.LensFacing);
                if (facing == desiredFacing)
                {
                    return id;
                }
            }

            // Fallback to first available camera
            var allIds = manager.GetCameraIdList();
            return allIds.Length > 0 ? allIds[0] : null;
        }

        private void CloseCamera()
        {
            try
            {
                if (_captureSession != null)
                {
                    _captureSession.Close();
                    _captureSession.Dispose();
                    _captureSession = null;
                }

                if (_cameraDevice != null)
                {
                    _cameraDevice.Close();
                    _cameraDevice.Dispose();
                    _cameraDevice = null;
                }
            }
            catch (Exception ex)
            {
                AndroidLog.Warn("CameraPreviewRenderer", "CloseCamera error: " + ex.Message);
            }
            finally
            {
                _isCameraOpening = false;
                StopBackgroundThread();
            }
        }

        private void ConfigureTransform(int viewWidth, int viewHeight)
        {
            if (_textureView == null || _context == null) return;

            var windowManager = _context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            if (windowManager == null) return;

            var rotation = windowManager.DefaultDisplay.Rotation;
            var matrix = new Matrix();
            var viewRect = new RectF(0, 0, viewWidth, viewHeight);

            if (rotation == SurfaceOrientation.Rotation90 || rotation == SurfaceOrientation.Rotation270)
            {
                var bufferRect = new RectF(0, 0, viewHeight, viewWidth);
                var centerX = viewRect.CenterX();
                var centerY = viewRect.CenterY();
                bufferRect.Offset(centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
                matrix.SetRectToRect(viewRect, bufferRect, Matrix.ScaleToFit.Fill);

                var degrees = rotation == SurfaceOrientation.Rotation90 ? 90 : 270;
                matrix.PostRotate(-degrees, centerX, centerY);
            }

            _textureView.SetTransform(matrix);
        }

        private void SetStatusText(string message)
        {
            Post(() =>
            {
                if (_statusTextView != null)
                {
                    _statusTextView.Text = message;
                    _statusTextView.Visibility = ViewStates.Visible;
                }
            });
        }

        #endregion

        #region Camera Callbacks

        private class CameraStateCallback : CameraDevice.StateCallback
        {
            private readonly CameraPreviewRenderer _renderer;

            public CameraStateCallback(CameraPreviewRenderer renderer)
            {
                _renderer = renderer;
            }

            public override void OnOpened(CameraDevice camera)
            {
                _renderer._isCameraOpening = false;
                _renderer._cameraDevice = camera;
                _renderer.CreateCameraPreviewSession();
            }

            public override void OnDisconnected(CameraDevice camera)
            {
                _renderer._isCameraOpening = false;
                camera.Close();
                _renderer._cameraDevice = null;
            }

            public override void OnError(CameraDevice camera, CameraError error)
            {
                _renderer._isCameraOpening = false;
                camera.Close();
                _renderer._cameraDevice = null;
                _renderer.SetStatusText($"Camera device error ({error})");
                _renderer.Element?.NotifyError($"Camera error {error}");
            }
        }

        private void CreateCameraPreviewSession()
        {
            if (_cameraDevice == null || _textureView == null || !_textureView.IsAvailable || _isDisposed) return;

            try
            {
                var texture = _textureView.SurfaceTexture;
                if (texture == null) return;

                // Pick optimal preview resolution (default to standard 640x480 or 1280x720 for preview)
                texture.SetDefaultBufferSize(640, 480);
                var surface = new Surface(texture);

                _previewRequestBuilder = _cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
                _previewRequestBuilder.AddTarget(surface);
                _previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousVideo);

                var outputSurfaces = new List<Surface> { surface };

                _cameraDevice.CreateCaptureSession(outputSurfaces, new CaptureSessionCallback(this), _backgroundHandler);
            }
            catch (Exception ex)
            {
                AndroidLog.Error("CameraPreviewRenderer", "CreateCameraPreviewSession error: " + ex);
                SetStatusText("Preview error: " + ex.Message);
            }
        }

        private class CaptureSessionCallback : CameraCaptureSession.StateCallback
        {
            private readonly CameraPreviewRenderer _renderer;

            public CaptureSessionCallback(CameraPreviewRenderer renderer)
            {
                _renderer = renderer;
            }

            public override void OnConfigured(CameraCaptureSession session)
            {
                if (_renderer._cameraDevice == null || _renderer._isDisposed) return;

                _renderer._captureSession = session;
                try
                {
                    _renderer._previewRequestBuilder.Set(CaptureRequest.ControlMode, (int)ControlMode.Auto);
                    session.SetRepeatingRequest(_renderer._previewRequestBuilder.Build(), null, _renderer._backgroundHandler);
                }
                catch (Exception ex)
                {
                    AndroidLog.Error("CameraPreviewRenderer", "SetRepeatingRequest error: " + ex);
                    _renderer.SetStatusText("Capture error: " + ex.Message);
                }
            }

            public override void OnConfigureFailed(CameraCaptureSession session)
            {
                _renderer.SetStatusText("Session config failed");
                _renderer.Element?.NotifyError("Failed to configure capture session");
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _isDisposed = true;
                CloseCamera();

                if (_textureView != null)
                {
                    _textureView.SurfaceTextureListener = null;
                    _textureView.Dispose();
                    _textureView = null;
                }

                if (_statusTextView != null)
                {
                    _statusTextView.Dispose();
                    _statusTextView = null;
                }

                if (_rootLayout != null)
                {
                    _rootLayout.Dispose();
                    _rootLayout = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
