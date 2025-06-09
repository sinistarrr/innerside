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
		public float CrouchTimeout = 0.2f;

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
		[Tooltip("Duration of limited movement during sliding")]
		public float SlidingDuration = 1.0f;
		[Tooltip("Tweak this value for more/less friction when sliding")]
		public float SlideFriction = 4.0f;

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
		private float _slideSpeed = 0f;
		private Vector3 _previousDir = Vector3.zero;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;
		private float _crouchTimeoutDelta;
		private float _slidingTimer = 0f;

		private bool _isReversing = false;
		private bool _isSliding = false;
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
		private const float _speedOffset = 0.1f;

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

			// calculate input and direction
			Vector3 inputDirection = GetInputDirection();
			float directionChangeValue = Vector3.Dot(_previousDir, inputDirection);
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			// --- Sliding logic ---
			if (HandleSliding())
				return;

			// handle reversal (early return if reversing)
			if (HandleReversal(inputDirection, directionChangeValue))
				return;

			// handle movement (acceleration/deceleration)
			HandleMovement(ref inputDirection, currentHorizontalSpeed);

			// handle crouching
			HandleCrouching(currentHorizontalSpeed);

			// update animator and move character
			UpdateAnimatorAndMove(inputDirection);

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

		private Vector3 GetInputDirection()
		{
			Vector3 dir = transform.right * _input.move.x + transform.forward * _input.move.y;
			return dir.normalized;
		}

		private bool HandleReversal(Vector3 inputDirection, float directionChangeValue)
		{
			if (!_isReversing && directionChangeValue < 0 && _speed > 0.1f && inputDirection != Vector3.zero)
			{
				_isReversing = true;
			}

			if (_isReversing)
			{
				float newDirectionChange = Vector3.Dot(_previousDir, inputDirection);

				if (_speed <= 0.05f || inputDirection == Vector3.zero || newDirectionChange >= 0)
				{
					_isReversing = false;
					if (inputDirection != Vector3.zero)
						_previousDir = inputDirection;
				}
				else
				{
					float customDecelerationRate = SpeedChangeRate * SharpTurnDeceleration;
					_speed -= customDecelerationRate * Time.deltaTime;
					_speed = Mathf.Max(_speed, 0);

					Vector3 velocityDir = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).normalized;
					inputDirection = velocityDir;

					_controller.Move(inputDirection * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

					Vector3 reversalRelativeVelocity = transform.InverseTransformDirection(_controller.velocity);
					_animator.SetFloat("VelocityX", reversalRelativeVelocity.x / SprintSpeed, Damping, Time.deltaTime);
					_animator.SetFloat("VelocityZ", reversalRelativeVelocity.z / SprintSpeed, Damping, Time.deltaTime);
					_animator.SetFloat("JumpHorizontalVelocity", _speed / SprintSpeed, Damping, Time.deltaTime);

					return true;
				}
			}
			return false;
		}

		private void HandleMovement(ref Vector3 inputDirection, float currentHorizontalSpeed)
		{
			float targetSpeed = Crouching ? CrouchSpeed : _input.sprint ? SprintSpeed : MoveSpeed;
			float inputMagnitude = !IsCurrentDeviceMouse ? _input.move.magnitude : 1f;

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
				else if (!IsMovingForward())
				{
					targetSpeed = inputMagnitude * StrafeSpeed;
				}
				else
				{
					if (targetSpeed > (SprintSpeed - _speedOffset))
					{
						targetSpeed = inputMagnitude * SprintSpeed;
					}
					else
					{
						targetSpeed = inputMagnitude * MoveSpeed;
					}
				}
			}

			if (currentHorizontalSpeed < targetSpeed - _speedOffset || currentHorizontalSpeed > targetSpeed + _speedOffset)
			{
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// Custom deceleration when no input and not reversing
			if (_input.move == Vector2.zero && currentHorizontalSpeed > 0 && !_isReversing)
			{
				float decelerationRate = SpeedChangeRate / 100.0f;
				_speed -= decelerationRate * Time.deltaTime;
				_speed = Mathf.Max(_speed, 0);
				inputDirection = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).normalized;
			}
		}

		private void HandleCrouching(float currentHorizontalSpeed)
		{
			if (_input.crouch && !_input.jump && _crouchTimeoutDelta <= 0.0f)
			{
				_crouchTimeoutDelta = CrouchTimeout;
				Debug.Log("Crouch button pressed");
				if (!Crouching)
				{
					Crouching = true;
					_controller.height = _crouchedHeight;
					_controller.center = new Vector3(_controller.center.x, _crouchedYCenter, _controller.center.z);
					if (currentHorizontalSpeed > CrouchSpeed + _speedOffset)
					{
						_isSliding = true;
						_slidingTimer = SlidingDuration;
						_slideSpeed = currentHorizontalSpeed; // initialize slide speed
					}
				}
				else
				{
					Crouching = false;
					_controller.height = _standingHeight;
					_controller.center = new Vector3(_controller.center.x, _standingYCenter, _controller.center.z);
				}
				_input.crouch = false;
			}

			if (_crouchTimeoutDelta >= 0.0f)
			{
				_crouchTimeoutDelta -= Time.deltaTime;
			}
		}

		private bool HandleSliding()
		{
			if (_isSliding)
			{
				_slidingTimer -= Time.deltaTime;

				// gradually reduce slide speed to simulate friction
				_slideSpeed = Mathf.MoveTowards(_slideSpeed, 0f, SlideFriction * Time.deltaTime);

				Vector3 slideDirection = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).normalized;

				// move in the sliding direction at current slide speed
				_controller.Move(slideDirection * (_slideSpeed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

				// update animator here
				Vector3 slideRelativeVelocity = transform.InverseTransformDirection(_controller.velocity);
				_animator.SetFloat("VelocityX", slideRelativeVelocity.x / SprintSpeed, Damping, Time.deltaTime);
				_animator.SetFloat("VelocityZ", slideRelativeVelocity.z / SprintSpeed, Damping, Time.deltaTime);
				_animator.SetFloat("JumpHorizontalVelocity", _slideSpeed / SprintSpeed, Damping, Time.deltaTime);

				if (_slidingTimer <= 0f || _slideSpeed <= 0.1f)
				{
					_isSliding = false;
				}
				return true; // sliding handled, skip rest of Move()
			}
			return false; // not sliding, continue with Move()
		}

		private void UpdateAnimatorAndMove(Vector3 inputDirection)
		{
			Vector3 relativeVelocity = transform.InverseTransformDirection(_controller.velocity);

			_animator.SetFloat("VelocityX", relativeVelocity.x / SprintSpeed, Damping, Time.deltaTime);
			_animator.SetFloat("VelocityZ", relativeVelocity.z / SprintSpeed, Damping, Time.deltaTime);
			_animator.SetFloat("JumpHorizontalVelocity", _speed / SprintSpeed, Damping, Time.deltaTime);
			_animator.SetBool("IsCrouching", Crouching);

			_controller.Move(inputDirection * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

			if (inputDirection != Vector3.zero && !_isReversing)
				_previousDir = inputDirection;
		}
	}
}