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

        public event EventHandler<string> CameraError;

        public void NotifyError(string message)
        {
            StatusMessage = message;
            CameraError?.Invoke(this, message);
        }
    }
}
