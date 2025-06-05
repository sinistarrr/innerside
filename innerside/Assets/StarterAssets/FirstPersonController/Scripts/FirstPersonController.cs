using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 6.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 8.0f;
		[Tooltip("Crouch speed of the character in m/s")]
		public float CrouchSpeed = 2.0f;
		[Tooltip("Strafe speed of the character in m/s")]
		public float StrafeSpeed = 4.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;
		[Tooltip("Deceleration when turning sharply")]
		public float SharpTurnDeceleration = 5.0f;
		[Tooltip("Character animations transitions smoothness value (Damping)")]
		public float Damping = 0.15f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;
		[Tooltip("Time required to pass before being able to crouch again. Set to 0f to instantly crouch again")]
		public float CrouchTimeout = 0.1f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Player Crouching")]
		[Tooltip("If the character is crouched or not.")]
		public bool Crouching = false;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;
		private Vector3 _previousDir = Vector3.zero;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;
		private float _crouchTimeoutDelta;

		private bool _isReversing = false;
		private float _standingHeight;
		private float _standingYCenter;
		private float _crouchedHeight;
		private float _crouchedYCenter;


#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;
		// variable to store character animator component, make sure there is no multiple animator components to avoid problems
		private Animator _animator;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
			_animator = GetComponentInChildren<Animator>();
			if (_animator == null)
			{
				Debug.LogError("Character's Animator component missing on First Person Controller");
			}
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
			_crouchTimeoutDelta = CrouchTimeout;

			// set the standing and crouched height and center in variables once to avoid potential calculations
			_standingHeight = _controller.height;
			_standingYCenter = _controller.center.y;
			_crouchedHeight = _controller.height / 2;
			_crouchedYCenter = _controller.center.y / 2;
		}

		private void Update()
		{

			JumpAndGravity();
			GroundedCheck();
			// CrouchingAndSliding();

			Move();


		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
			_animator.SetBool("IsGrounded", Grounded);
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			// set target speed based on move speed, sprint speed and if sprint is pressed
			// float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
			float targetSpeed = Crouching ? CrouchSpeed : _input.sprint ? SprintSpeed : MoveSpeed;
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
			float speedOffset = 0.1f;
			float inputMagnitude = !IsCurrentDeviceMouse ? _input.move.magnitude : 1f;

			// calculate inputDirection
			Vector3 inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			inputDirection = inputDirection.normalized;

			// calculate direction change value
			float directionChangeValue = Vector3.Dot(_previousDir, inputDirection);

			// -- HANDLING OF THE REVERSAL LOCK --
			// This is for when the player changes direction quickly, like when reversing. It will set a lock to prevent the player from reversing too quickly.
			if (!_isReversing && directionChangeValue < 0 && _speed > 0.1f && inputDirection != Vector3.zero)
			{
				_isReversing = true;
			}

			if (_isReversing)
			{
				float newDirectionChange = Vector3.Dot(_previousDir, inputDirection);

				// if we've stopped, or the player released the key, or tries a non-opposite direction, cancel reversal lock
				if (_speed <= 0.05f || inputDirection == Vector3.zero || newDirectionChange >= 0)
				{
					_isReversing = false;
					if (inputDirection != Vector3.zero)
						_previousDir = inputDirection;
				}
				else
				{
					// custom fast deceleration, always in current velocity direction
					float customDecelerationRate = SpeedChangeRate * SharpTurnDeceleration; // faster than normal, tweak as needed
					_speed -= customDecelerationRate * Time.deltaTime;
					_speed = Mathf.Max(_speed, 0);

					// decelerate in the direction of current velocity, not _previousDir
					Vector3 velocityDir = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).normalized;
					inputDirection = velocityDir;


					_controller.Move(inputDirection * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

					// update animator here
					Vector3 reversalRelativeVelocity = transform.InverseTransformDirection(_controller.velocity);
					_animator.SetFloat("VelocityX", reversalRelativeVelocity.x / SprintSpeed, Damping, Time.deltaTime);
					_animator.SetFloat("VelocityZ", reversalRelativeVelocity.z / SprintSpeed, Damping, Time.deltaTime);
					_animator.SetFloat("JumpHorizontalVelocity", _speed / SprintSpeed, Damping, Time.deltaTime);

					// SKIP the rest of Move() while reversing
					return;
				}
			}
			// -- HANDLING OF THE REVERSAL LOCK END --

			// -- MOVEMENT LOGIC --
			// If the player stopped touching any button related to horizontal movement, we set the target speed to 0.
			if (_input.move == Vector2.zero)
			{
				targetSpeed = 0.0f;
			}
			else
			{
				if (Crouching)
				{
					targetSpeed = inputMagnitude * CrouchSpeed;
				}
				// If the player is not moving forward, we set the target speed to StrafeSpeed since the player is strafing.
				else if (!IsMovingForward())
				{
					targetSpeed = inputMagnitude * StrafeSpeed;
				}
				else
				{
					// If the player is moving forward, we check if they are sprinting. And if they are, we set the target speed to SprintSpeed.
					if (targetSpeed > (SprintSpeed - speedOffset))
					{
						targetSpeed = inputMagnitude * SprintSpeed;
					}
					else
					{
						targetSpeed = inputMagnitude * MoveSpeed;
					}
				}
			}

			// Calculation of player velocity independent of world direction, in the local space of the player
			Vector3 relativeVelocity = transform.InverseTransformDirection(_controller.velocity);
			
			// We verify if the player is moving at target speed or not and adjust acceleration accordingly with Lerp.
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// This handle the case when the player is not pressing any key and is moving forward, we decelerate the player with a custom deceleration.
			// Only decelerate and overwrite inputDirection if the player is not pressing any key AND not reversing
			if (_input.move == Vector2.zero && currentHorizontalSpeed > 0 && !_isReversing)
			{
				float decelerationRate = SpeedChangeRate / 100.0f;
				_speed -= decelerationRate * Time.deltaTime;
				_speed = Mathf.Max(_speed, 0);
				inputDirection = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).normalized;
			}

			// -- END OF MOVEMENT LOGIC -- 

			// -- CROUCHING LOGIC --

			// If the player is pressing the crouch button and not jumping, we toggle on/off crouching.
			if (_input.crouch && !_input.jump && _crouchTimeoutDelta <= 0.0f)
			{
				// reset the crouch timeout timer
				_crouchTimeoutDelta = CrouchTimeout;
				Debug.Log("Crouch button pressed");
				if (!Crouching)
				{
					// Crouch logic here, e.g. change collider height, camera position, etc.
					// _controller.height = CrouchHeight; // Example
					// _mainCamera.transform.localPosition = new Vector3(0, CrouchCameraHeight, 0); // Example
					Crouching = true;
					_controller.height = _crouchedHeight;
					_controller.center = new Vector3(_controller.center.x, _crouchedYCenter, _controller.center.z);
				}
				else
				{
					// Uncrouch logic here, e.g. reset collider height, camera position, etc.
					// _controller.height = NormalHeight; // Example
					// _mainCamera.transform.localPosition = new Vector3(0, NormalCameraHeight, 0); // Example
					Crouching = false;
					_controller.height = _standingHeight;
					_controller.center = new Vector3(_controller.center.x, _standingYCenter, _controller.center.z);
				}
				// if we just crouched, we reset the crouch input to false to prevent toggling crouch again immediately
				_input.crouch = false;
			}

			// jump timeout
			if (_crouchTimeoutDelta >= 0.0f)
			{
				_crouchTimeoutDelta -= Time.deltaTime;
			}

			
			// -- END OF CROUCHING LOGIC --

			// This links the velocity of the player to the animator, so it can play the correct animations. We smooth the transitions with Damping.
			_animator.SetFloat("VelocityX", relativeVelocity.x / SprintSpeed, Damping, Time.deltaTime);
			_animator.SetFloat("VelocityZ", relativeVelocity.z / SprintSpeed, Damping, Time.deltaTime);
			_animator.SetFloat("JumpHorizontalVelocity", _speed / SprintSpeed, Damping, Time.deltaTime);

			// This links the crouching state to the animator, so it can play the correct animations.
			_animator.SetBool("IsCrouching", Crouching);

			_controller.Move(inputDirection * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

			if (inputDirection != Vector3.zero && !_isReversing)
				_previousDir = inputDirection;
			
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private bool IsMovingForward()
		{
			float diagDirNormalized = Mathf.Sqrt(2) / 2;
			float diagDirThreshold = 0.05f;
			return (_input.move.normalized.y > diagDirNormalized - diagDirThreshold) && (_input.move.normalized.x > -diagDirNormalized - diagDirThreshold) ||
					(_input.move.normalized.y > diagDirNormalized - diagDirThreshold) && (_input.move.normalized.x < diagDirNormalized + diagDirThreshold);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}