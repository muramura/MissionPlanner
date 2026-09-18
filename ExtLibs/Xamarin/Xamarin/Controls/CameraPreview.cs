using System;
using Xamarin.Forms;

namespace MissionPlanner.Controls
{
    public enum CameraFacingOption
    {
        Back,
        Front
    }

    public class CameraPreview : View
    {
        public static readonly BindableProperty CameraFacingProperty =
            BindableProperty.Create(
                propertyName: nameof(CameraFacing),
                returnType: typeof(CameraFacingOption),
                declaringType: typeof(CameraPreview),
                defaultValue: CameraFacingOption.Back);

        public CameraFacingOption CameraFacing
        {
            get => (CameraFacingOption)GetValue(CameraFacingProperty);
            set => SetValue(CameraFacingProperty, value);
        }

        public static readonly BindableProperty IsCameraActiveProperty =
            BindableProperty.Create(
                propertyName: nameof(IsCameraActive),
                returnType: typeof(bool),
                declaringType: typeof(CameraPreview),
                defaultValue: true);

        public bool IsCameraActive
        {
            get => (bool)GetValue(IsCameraActiveProperty);
            set => SetValue(IsCameraActiveProperty, value);
        }

        public static readonly BindableProperty StatusMessageProperty =
            BindableProperty.Create(
                propertyName: nameof(StatusMessage),
                returnType: typeof(string),
                declaringType: typeof(CameraPreview),
                defaultValue: "Connecting camera...");

        public string StatusMessage
        {
            get => (string)GetValue(StatusMessageProperty);
            set => SetValue(StatusMessageProperty, value);
        }

        public static readonly BindableProperty IsRecordingProperty =
            BindableProperty.Create(
                propertyName: nameof(IsRecording),
                returnType: typeof(bool),
                declaringType: typeof(CameraPreview),
                defaultValue: false,
                defaultBindingMode: BindingMode.TwoWay);

        public bool IsRecording
        {
            get => (bool)GetValue(IsRecordingProperty);
            set => SetValue(IsRecordingProperty, value);
        }

        public static readonly BindableProperty ZoomFactorProperty =
            BindableProperty.Create(
                propertyName: nameof(ZoomFactor),
                returnType: typeof(float),
                declaringType: typeof(CameraPreview),
                defaultValue: 1.0f);

        public float ZoomFactor
        {
            get => (float)GetValue(ZoomFactorProperty);
            set => SetValue(ZoomFactorProperty, value);
        }

        public static readonly BindableProperty IsTorchOnProperty =
            BindableProperty.Create(
                propertyName: nameof(IsTorchOn),
                returnType: typeof(bool),
                declaringType: typeof(CameraPreview),
                defaultValue: false,
                defaultBindingMode: BindingMode.TwoWay);

        public bool IsTorchOn
        {
            get => (bool)GetValue(IsTorchOnProperty);
            set => SetValue(IsTorchOnProperty, value);
        }

        public event EventHandler<string> CameraError;
        public event EventHandler<string> RecordingFinished;
        public event EventHandler CapturePhotoRequested;
        public event EventHandler<string> PhotoCaptured;

        public void CapturePhoto()
        {
            CapturePhotoRequested?.Invoke(this, EventArgs.Empty);
        }

        public void NotifyPhotoCaptured(string filePath)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                PhotoCaptured?.Invoke(this, filePath);
            });
        }

        public void NotifyError(string message)
        {
            StatusMessage = message;
            CameraError?.Invoke(this, message);
        }

        public void NotifyRecordingFinished(string filePath)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                RecordingFinished?.Invoke(this, filePath);
            });
        }
    }
}
