﻿using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
#if MODULE_URP_ENABLED
using UnityEngine.Rendering.Universal;
#elif MODULE_LWRP_ENABLED
using UnityEngine.Rendering.LWRP;
#endif
using UnityEngine.Scripting;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;



namespace WebcamARFoundation.Internal
{
    [Preserve]
    public sealed class WebCameraSubsystem : XRCameraSubsystem
    {
        public const string ID = "Webcam-subsystem";

#if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new WebcamProvider();
#endif




        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
#if UNITY_EDITOR
            XRCameraSubsystemCinfo cameraSubsystemCinfo = new XRCameraSubsystemCinfo
            {
                id = ID,
#if UNITY_2020_2_OR_NEWER
                providerType = typeof(WebCameraSubsystem.WebcamProvider),
                subsystemTypeOverride = typeof(WebCameraSubsystem),
#else
                implementationType = typeof(WebCameraSubsystem),
#endif

                supportsAverageBrightness = false,
                supportsAverageColorTemperature = true,
                supportsColorCorrection = false,
                supportsDisplayMatrix = true,
                supportsProjectionMatrix = true,
                supportsTimestamp = true,
                supportsCameraConfigurations = true,
                supportsCameraImage = true,
                supportsAverageIntensityInLumens = true,
                supportsFocusModes = true,
                supportsFaceTrackingAmbientIntensityLightEstimation = true,
                supportsFaceTrackingHDRLightEstimation = true,
                supportsWorldTrackingAmbientIntensityLightEstimation = true,
                supportsWorldTrackingHDRLightEstimation = false,
            };

            if (!XRCameraSubsystem.Register(cameraSubsystemCinfo))
            {
                Debug.LogErrorFormat("Cannot register the {0} subsystem", ID);
            }
            else
            {
                Debug.LogFormat("Registered the {0} subsystem", ID);
            }
#endif // UNITY_EDITOR
        }

        class WebcamProvider : Provider
        {
            static readonly int _TEXTURE_MAIN = Shader.PropertyToID("_MainTex");

            private WebCamTexture webcam;
            private Material m_CameraMaterial;
            public override Material cameraMaterial => m_CameraMaterial;

            public override bool permissionGranted => true;

            private static string ShaderName
            {
                get
                {
                    var pipeline = GraphicsSettings.renderPipelineAsset;
                    if (pipeline == null)
                    {
                        return "Unlit/WebcamBackground";
                    }
#if MODULE_URP_ENABLED
                    else if (pipeline is UniversalRenderPipelineAsset)
                    {
                        return "Unlit/ARKitURPBackground";
                    }
#elif MODULE_LWRP_ENABLED
                    else if (pipeline is LightweightRenderPipelineAsset)
                    {
                        return "Unlit/ARKitLWRPBackground";
                    }
#endif
                    Debug.LogError($"{pipeline} is not supported in ARKit");
                    return "Unlit/ARKitBackground";
                }
            }

            public WebcamProvider()
            {
                // m_CameraMaterial = CreateCameraMaterial(ShaderName);
            }

            public override void Start()
            {
                base.Start();
                if (m_CameraMaterial == null)
                {
                    m_CameraMaterial = CreateCameraMaterial(ShaderName);
                }

                var devices = WebCamTexture.devices;
                for (int i = 0; i < devices.Length; i++)
                {
                    Debug.Log($"web camera: {i} = {devices[i].name}");
                }
                string cameraName = WebCamTexture.devices[1].name;
                webcam = new WebCamTexture(cameraName, 1920, 1080);
                webcam.Play();

                Debug.Log("webcam started: name");
            }

            public override void Destroy()
            {
                webcam?.Stop();
                webcam = null;

                if (m_CameraMaterial != null)
                {
                    UnityEngine.Object.Destroy(m_CameraMaterial);
                    m_CameraMaterial = null;
                }
                base.Destroy();
            }

            public override Feature currentCamera => Feature.AnyCamera;

            public override Feature requestedCamera
            {
                get => Feature.AnyCamera;
                set => Debug.Log($"requestedCamera: {value}");
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {

                if (!Application.isPlaying
                    || webcam == null
                    || webcam.width <= 16)
                {
                    cameraFrame = default(XRCameraFrame);
                    return false;
                }

                const XRCameraFrameProperties properties =
                    XRCameraFrameProperties.Timestamp
                    // | XRCameraFrameProperties.ProjectionMatrix
                    | XRCameraFrameProperties.DisplayMatrix;

                

                cameraFrame = (XRCameraFrame)new CameraFrame()
                {
                    timestampNs = DateTime.Now.Ticks,
                    averageBrightness = 0,
                    averageColorTemperature = 0,
                    colorCorrection = default(Color),
                    // projectionMatrix = remoteFrame.projectionMatrix,
                    projectionMatrix = Matrix4x4.identity,
                    // displayMatrix = remoteFrame.displayMatrix,
                    displayMatrix = Matrix4x4.identity,
                    trackingState = TrackingState.Tracking,
                    nativePtr = new IntPtr(0),
                    properties = properties,
                    averageIntensityInLumens = 0,
                    exposureDuration = 0,
                    exposureOffset = 0,
                    mainLightIntensityLumens = 0,
                    mainLightColor = default(Color),
                    ambientSphericalHarmonics = default(SphericalHarmonicsL2),
                    cameraGrain = default(XRTextureDescriptor),
                    noiseIntensity = 0,
                };

                // Debug.Log(cameraFrame);
                return true;
            }

            public override bool autoFocusEnabled => true;

            public override bool autoFocusRequested
            {
                get => true;
                set => Debug.Log($"autoFocusRequested: {value}");
            }

            public override Feature currentLightEstimation => Feature.AnyLightEstimation;
            public override Feature requestedLightEstimation
            {
                get => Feature.AnyLightEstimation;
                set => Debug.Log($"requestedLightEstimation: {value}");
            }

            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor, Allocator allocator)
            {
                if (!Application.isPlaying)
                {
                    return new NativeArray<XRTextureDescriptor>(0, allocator);
                }

                if (webcam == null || webcam.width <= 16)
                {
                    return new NativeArray<XRTextureDescriptor>(0, allocator);
                }

                var arr = new NativeArray<XRTextureDescriptor>(1, allocator);
                arr[0] = new TextureDescriptor(webcam, _TEXTURE_MAIN);

                return arr;
            }

        }
    }
}