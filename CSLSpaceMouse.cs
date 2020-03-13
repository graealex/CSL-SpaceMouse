using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using ICities;
using TDxInput;
using UnityEngine;

namespace CSLSpaceMouse
{
	public class CSLSpaceMouse : ThreadingExtensionBase, IUserMod
	{
		public string Name => "Space Mouse camera controls";

		public string Description => "Use a 3Dconnexion SpaceMouse® to control the camera in Cities: Skylines";

		public CSLSpaceMouse()
		{
			Log("Instantiated");
		}

		private Sensor sensor;
		private Device device;

		public Vector3 GetTranslation()
		{
			return (sensor == null ?
						Vector3.zero :
						new Vector3(
							(float)sensor.Translation.X,
							(float)sensor.Translation.Y,
							-(float)sensor.Translation.Z));
		}

		public Vector3 GetRotation()
		{
			return (sensor == null ?
				Vector3.zero :
				new Vector3(
					(float)sensor.Rotation.X,
					(float)sensor.Rotation.Y,
					-(float)sensor.Rotation.Z));
		}

		public Quaternion GetRotationQuaternion()
		{
			return (sensor == null ?
						Quaternion.identity :
						Quaternion.AngleAxis(
							(float)sensor.Rotation.Angle,
							new Vector3(
								-(float)sensor.Rotation.X,
								-(float)sensor.Rotation.Y,
								(float)sensor.Rotation.Z)));
		}

		public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
		{
			GameObject mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			if (mainCamera == null) return;

			CameraController cameraController = mainCamera.GetComponent<CameraController>();
			if (cameraController == null) return;

			// Get current target positions for the camera controller
			// This isn't the "look at" point, but the target of the camera smooting and animation
			Vector3 targetPos = cameraController.m_targetPosition;
			Vector2 targetAngle = cameraController.m_targetAngle;
			float targetSize = cameraController.m_targetSize;

			// Get a rotation quaternion for the current camera rotation, because
			// the translation needs to be in the direction of the camera
			Quaternion targetCameraRotation = Quaternion.Euler(0, targetAngle.x, 0);

			// Get the translation vectors from the SpaceMouse
			Vector3 translation = GetTranslation() * realTimeDelta * targetSize / 350f;

			// Rotate the translation in the direction of the camera pointing
			Vector3 translationRotated = targetCameraRotation * translation;

			// Get the rotation vectors from the SpaceMouse
			Vector3 rotation = GetRotation() * realTimeDelta * 70f;

			// Only manipulate if actually required, as we clear the current target object
			if ((rotation.magnitude > 0.01f) || (translation.magnitude > 0.01f))
			{
				// Clear current "fly to" target
				cameraController.ClearTarget();

				// Add the rotated and scaled translation to the target position
				cameraController.m_targetPosition = targetPos + translationRotated;

				// Calculate the azimuth (left/right)
				// Cities: Skylines clamps value to -179.9 ... 179.9, so we do the same
				float azimuth = targetAngle.x;
				azimuth -= rotation.y;
				if (azimuth >= 180f) azimuth -= 360f;
				if (azimuth <= -180f) azimuth += 360f;

				// Calculate the elevation (up/down)
				float elevation = targetAngle.y;
				elevation -= rotation.x / 2f;
				if (elevation >= 180f) elevation -= 360f;
				if (elevation <= -180f) elevation += 360f;

				// Set new camera angle to animate to
				cameraController.m_targetAngle = new Vector2(azimuth, elevation);

				// This is a bit janky, but there are no other ways besides completely rewiring
				// the camera controller with Detours, so we take it
				// Up/down axis on the Space Mouse brings us nearer to the ground or up into the sky
				cameraController.m_targetSize += translation.y / 4f;
			}
		}

		#region ThreadingExtensionBase

		/// <summary>
		/// Called by the game after this instance is created.
		/// </summary>
		/// <param name="threading">The threading.</param>
		public override void OnCreated(IThreading threading)
		{
			base.OnCreated(threading);
			Log("Created extension");

			try
			{
				if (device == null)
				{
					Log("Creating device");
					device = new Device();
					sensor = device.Sensor;
				}
				if (!device.IsConnected)
				{
					Log("Connecting device");
					device.Connect();
				}
			}
			catch (COMException ex)
			{
				Log(ex.ToString());
			}
		}

		/// <summary>
		/// Called by the game before this instance is about to be destroyed.
		/// </summary>
		public override void OnReleased()
		{
			try
			{
				if (device != null)
				{
					if (!device.IsConnected)
					{
						Log("Disconnecting device");
						device.Disconnect();
					}
					Log("Releasing device");
					device = null;
				}
			}
			catch (COMException ex)
			{
				Log(ex.ToString());
			}

			Log("Released extension");
			base.OnReleased();
		}
		#endregion

		#region Logging
		/// <summary>
		/// Writes a message to the debug logs. "CSLSpaceMouse" tag
		/// and timestamp are automatically prepended.
		/// </summary>
		/// <param name="message">Message.</param>
		public static void Log(String message)
		{
			String time = DateTime.Now.ToUniversalTime().ToString("yyyyMMdd' 'HHmmss'.'fff");
			message = $"{time}: {message}";
			try
			{
				UnityEngine.Debug.Log("[CSLSpaceMouse] " + message);
			}
			catch (NullReferenceException)
			{
				//Happens if Unity logger isn't set up yet
			}
		}
		#endregion
	}
}
