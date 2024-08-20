using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace Cap
{
    [Serializable, VolumeComponentMenu("Post-processing/Custom/CaptureFrameAndSendToCVirtualCam")]
    public sealed class CaptureFrameAndSendToCVirtualCam : CustomPostProcessVolumeComponent, IPostProcessComponent
    {

        [Tooltip("Execute on editor")] public BoolParameter ExecuteOnEditor = new BoolParameter(false);
        [Tooltip("Capture device index")] public ECaptureDeviceParameter CaptureDevice = new ECaptureDeviceParameter( ECaptureDevice.Close);
        [Tooltip("Scale image if Unity and capture resolution don't match (can introduce frame dropping, not recommended)")] public EResizeModeDeviceParameter ResizeMode =new EResizeModeDeviceParameter( EResizeMode.Disabled);
        [Tooltip("How many milliseconds to wait for a new frame until sending is considered to be stopped")] public IntParameter Timeout = new IntParameter(1000);
        [Tooltip("Mirror captured output image")] public EMirrorModeDeviceParameter MirrorMode =new EMirrorModeDeviceParameter (EMirrorMode.Disabled);
        [Tooltip("Introduce a frame of latency in favor of frame rate")] public BoolParameter DoubleBuffering = new BoolParameter(false);
        [Tooltip("Check to enable VSync during capturing")] public BoolParameter EnableVSync = new BoolParameter(false);
        [Tooltip("Set the desired render target frame rate")] public ClampedIntParameter TargetFrameRate = new ClampedIntParameter(60,1,120);
        [Tooltip("Check to disable output of warnings")] public BoolParameter HideWarnings = new BoolParameter(false);

        Interface CaptureInterface;

        public bool IsActive() => CaptureDevice.value != ECaptureDevice.Close;

        // Do not forget to add this post process in the Custom Post Process Orders list (Project Settings > Graphics > HDRP Global Settings).
        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        const string kShaderName = "Hidden/Shader/CaptureFrameAndSetToVirtualCamShader";

        public override void Setup()
        {
            QualitySettings.vSyncCount = (EnableVSync.value ? 1 : 0);
            Application.targetFrameRate = TargetFrameRate.value;
            CaptureInterface = new Interface(CaptureDevice.value);

        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            HDUtils.BlitCameraTexture(cmd, source, destination);
#if UNITY_EDITOR
            if (!ExecuteOnEditor.value)
            {
                return;
            }
#endif
            switch (CaptureInterface.SendTexture(destination, Timeout.value, DoubleBuffering.value, ResizeMode.value, MirrorMode.value))
                {
                    case ECaptureSendResult.SUCCESS: break;
                    case ECaptureSendResult.WARNING_FRAMESKIP: if (!HideWarnings.value) Debug.LogWarning("[UnityCapture] Capture device did skip a frame read, capture frame rate will not match render frame rate."); break;
                    case ECaptureSendResult.WARNING_CAPTUREINACTIVE: if (!HideWarnings.value) Debug.LogWarning("[UnityCapture] Capture device is inactive"); break;
                    case ECaptureSendResult.ERROR_UNSUPPORTEDGRAPHICSDEVICE: Debug.LogError("[UnityCapture] Unsupported graphics device (only D3D11 supported)"); break;
                    case ECaptureSendResult.ERROR_PARAMETER: Debug.LogError("[UnityCapture] Input parameter error"); break;
                    case ECaptureSendResult.ERROR_TOOLARGERESOLUTION: Debug.LogError("[UnityCapture] Render resolution is too large to send to capture device"); break;
                    case ECaptureSendResult.ERROR_TEXTUREFORMAT: Debug.LogError("[UnityCapture] Render texture format is unsupported (only basic non-HDR (ARGB32) and HDR (FP16/ARGB Half) formats are supported)"); break;
                    case ECaptureSendResult.ERROR_READTEXTURE: Debug.LogError("[UnityCapture] Error while reading texture image data"); break;
                    case ECaptureSendResult.ERROR_INVALIDCAPTUREINSTANCEPTR: Debug.LogError("[UnityCapture] Invalid Capture Instance Pointer"); break;
                }
            

        }

        public override void Cleanup()
        {
            CaptureInterface.Close();
 
        }
    }

    public class Interface
    {
        [System.Runtime.InteropServices.DllImport("UnityCapturePlugin")] extern static System.IntPtr CaptureCreateInstance(int CapNum);
        [System.Runtime.InteropServices.DllImport("UnityCapturePlugin")] extern static void CaptureDeleteInstance(System.IntPtr instance);
        [System.Runtime.InteropServices.DllImport("UnityCapturePlugin")] extern static ECaptureSendResult CaptureSendTexture(System.IntPtr instance, System.IntPtr nativetexture, int Timeout, bool UseDoubleBuffering, EResizeMode ResizeMode, EMirrorMode MirrorMode, bool IsLinearColorSpace);
        System.IntPtr CaptureInstance;

        public Interface(ECaptureDevice CaptureDevice)
        {
            CaptureInstance = CaptureCreateInstance((int)CaptureDevice);
        }

        ~Interface()
        {
            Close();
        }

        public void Close()
        {
            if (CaptureInstance != System.IntPtr.Zero) CaptureDeleteInstance(CaptureInstance);
            CaptureInstance = System.IntPtr.Zero;
        }

        public ECaptureSendResult SendTexture(Texture Source, int Timeout = 1000, bool DoubleBuffering = false, EResizeMode ResizeMode = EResizeMode.Disabled, EMirrorMode MirrorMode = EMirrorMode.Disabled)
        {
            if (CaptureInstance == System.IntPtr.Zero) return ECaptureSendResult.ERROR_INVALIDCAPTUREINSTANCEPTR;
            return CaptureSendTexture(CaptureInstance, Source.GetNativeTexturePtr(), Timeout, DoubleBuffering, ResizeMode, MirrorMode, QualitySettings.activeColorSpace == ColorSpace.Linear);
        }
    }

    [Serializable]
    public sealed class ECaptureDeviceParameter : VolumeParameter<ECaptureDevice>
    {
        /// <summary>
        /// Creates a new <see cref="BloomResolutionParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public ECaptureDeviceParameter(ECaptureDevice value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class EResizeModeDeviceParameter : VolumeParameter<EResizeMode>
    {
        /// <summary>
        /// Creates a new <see cref="BloomResolutionParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public EResizeModeDeviceParameter(EResizeMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class EMirrorModeDeviceParameter : VolumeParameter<EMirrorMode>
    {
        /// <summary>
        /// Creates a new <see cref="BloomResolutionParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public EMirrorModeDeviceParameter(EMirrorMode value, bool overrideState = false) : base(value, overrideState) { }
    }



    public enum ECaptureDevice {Close=-1, CaptureDevice1 = 0, CaptureDevice2 = 1, CaptureDevice3 = 2, CaptureDevice4 = 3, CaptureDevice5 = 4, CaptureDevice6 = 5, CaptureDevice7 = 6, CaptureDevice8 = 7, CaptureDevice9 = 8, CaptureDevice10 = 9 }
    public enum EResizeMode { Disabled = 0, LinearResize = 1 }
    public enum EMirrorMode { Disabled = 0, MirrorHorizontally = 1 }
    public enum ECaptureSendResult { SUCCESS = 0, WARNING_FRAMESKIP = 1, WARNING_CAPTUREINACTIVE = 2, ERROR_UNSUPPORTEDGRAPHICSDEVICE = 100, ERROR_PARAMETER = 101, ERROR_TOOLARGERESOLUTION = 102, ERROR_TEXTUREFORMAT = 103, ERROR_READTEXTURE = 104, ERROR_INVALIDCAPTUREINSTANCEPTR = 200 };
}