using System;
using NUnit.Framework;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

	[Space(10)]
	[Tooltip("If the character landed hard after a fall.")]
	public bool HardLanding = false;
	[Tooltip("Minimum fall speed to trigger a hard landing.")]
	public float HardFallThreshold = 8.0f;
	[Tooltip("Multiplier for movement control while in the air (0 = no control, 1 = full control)")]
	public float AirControl = 0.3f;
	[Tooltip("Multiplier for deceleration while in the air (0 = no deceleration, 1 = normal deceleration)")]
	public float AirDeceleration = 0.1f;
	[Tooltip("How quickly you can change direction in the air (0 = locked, 1 = instant)")]
	public float AirDirectionLerp = 0.1f;


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
	// [Tooltip("Duration of the hard landing roll in seconds")]
	// public float RollDuration = 1.0f;
	[Tooltip("Multiplier for roll speed relative to sprint speed")]
	public float RollSpeedMultiplier = 1.2f;

	[Header("Other")]
	public float TransitionSpeed = 10f; // Speed of stance transition


	[Header("Cinemachine")]
	[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
	public GameObject CinemachineCameraTarget;
	[Tooltip("How far in degrees can you move the camera up")]
	public float TopClamp = 90.0f;
	[Tooltip("How far in degrees can you move the camera down")]
	public float BottomClamp = -90.0f;

	// Player Stance tracking
	public enum PlayerStance
	{
		Standing,
		Crouching,
		CrouchJump
	}

	// Data structure to hold stance data
	[Serializable]
	public struct StanceData
	{
		public float Height;
		public float CenterY;
	}

	private PlayerStance _currentStance = PlayerStance.Standing;

	// stance data for different player stances
	public StanceData StandingStance;
	public StanceData CrouchingStance;
	public StanceData CrouchJumpStance;

	private StanceData _targetStance;

	// cinemachine
	private float _cinemachineTargetPitch;

	// player
	private float _speed;
	private float _rotationVelocity;
	private float _verticalVelocity;
	private float _terminalVelocity = 53.0f;
	private float _slideSpeed = 0f;
	private float _rollSpeed = 0f;
	private float _rollTimer = 0f;
	private Vector3 _previousDir = Vector3.zero;
	private Vector3 _rollDirection = Vector3.zero;

	// timeout deltatime
	private float _jumpTimeoutDelta;
	private float _fallTimeoutDelta;
	// private float _crouchTimeoutDelta;
	private float _slidingTimer = 0f;

	private bool _isReversing = false;
	private bool _isSliding = false;
	private bool _isRolling = false;
	private bool _isRunJump = false;
	private bool _isCrouchJumping = false;
	private bool _isStanceTransitioning = false;
	// private bool _didJump = false;
	private float _standingHeight;
	private float _standingYCenter;
	private float _crouchedHeight;
	private float _crouchedYCenter;
	private float _maxFallSpeed = 0f;
	private bool _wasGroundedLastFrame = true;
	private Vector3 _lockedAirDirection = Vector3.zero;
	private float _lockedAirSpeed = 0f;


#if ENABLE_INPUT_SYSTEM
	private PlayerInput _playerInput;
#endif
	private CharacterController _controller;
	private PlayerInputHandler _input;
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
		_input = GetComponent<PlayerInputHandler>();
		_animator = GetComponentInChildren<Animator>();
		if (_animator == null)
		{
			Debug.LogError("Character's Animator component missing on First Person Controller");
		}
#if ENABLE_INPUT_SYSTEM
		_playerInput = GetComponent<PlayerInput>();
#else
		Debug.LogError("Player Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

		// reset our timeouts on start
		_jumpTimeoutDelta = JumpTimeout;
		_fallTimeoutDelta = FallTimeout;
		// _crouchTimeoutDelta = CrouchTimeout;

		// set the standing and crouched height and center in variables once to avoid potential calculations
		_standingHeight = _controller.height;
		_standingYCenter = _controller.center.y;
		_crouchedHeight = _controller.height / 2;
		_crouchedYCenter = _controller.center.y / 2;

		// initialize stance data
		StandingStance = new StanceData { Height = _standingHeight, CenterY = _standingYCenter };
		CrouchingStance = new StanceData { Height = _crouchedHeight, CenterY = _crouchedYCenter };
		CrouchJumpStance = new StanceData { Height = _crouchedHeight, CenterY = _standingYCenter + _crouchedYCenter };
		_targetStance = StandingStance;
	}

	private void Update()
	{
		JumpAndGravity();
		GroundedCheck();
		Move();
		HandleHardLanding();
		HandleStanceTransition();
		
		Debug.Log("current stance: " + _currentStance);
		Debug.Log("crouching: " + Crouching);
		Debug.Log("input crouch: " + _input.crouch);
	}

	private void LateUpdate()
	{
		CameraRotation();
	}

	private void GroundedCheck()
	{
		// set sphere position, with offset
		// Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);

		float capsuleBottom = transform.position.y + _controller.center.y - (_controller.height / 2f);
		Vector3 spherePosition = new Vector3(transform.position.x, capsuleBottom - GroundedOffset, transform.position.z);
		Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		_animator.SetBool("IsGrounded", Grounded);
	}

	private void SetStance(PlayerStance newStance)
	{

		if (_currentStance == newStance) return;

		switch (newStance)
		{
			case PlayerStance.Standing:
				_targetStance = StandingStance;
				break;
			case PlayerStance.Crouching:
				_targetStance = CrouchingStance;
				break;
			case PlayerStance.CrouchJump:
				_targetStance = CrouchJumpStance;
				break;
		}
		_currentStance = newStance;
		_isStanceTransitioning = true; // Start transition

	}

	private void HandleStanceTransition()
	{
		// --- Force correct stance in air ---
		if (!Grounded && Crouching && _currentStance == PlayerStance.Crouching)
		{
			SetStance(PlayerStance.CrouchJump);
		}
		
		if (!_isStanceTransitioning) return;

		// Smoothly move height and center towards target stance
		_controller.height = Mathf.MoveTowards(_controller.height, _targetStance.Height, TransitionSpeed * Time.deltaTime);
		Vector3 center = _controller.center;
		center.y = Mathf.MoveTowards(center.y, _targetStance.CenterY, TransitionSpeed * Time.deltaTime);
		_controller.center = center;

		// Check if transition is complete
		if (Mathf.Approximately(_controller.height, _targetStance.Height) &&
			Mathf.Approximately(_controller.center.y, _targetStance.CenterY))
		{
			_isStanceTransitioning = false;
		}
	}

	private bool IsInState(string stateName)
	{
		if (_animator == null) return false;
		int layer = 0;

		// Check if in transition and next state is roll
		if (_animator.IsInTransition(layer))
		{
			AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(layer);
			if (nextState.IsTag(stateName)) // Use your tag here
				return true;
		}

		// Check if current state is roll
		AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(layer);
		return currentState.IsTag(stateName);
	}

	private void HandleHardLanding()
	{
		// hard fall detection logic
		if (!Grounded)
		{
			// track the maximum downward velocity while airborne
			if (_verticalVelocity < _maxFallSpeed)
				_maxFallSpeed = _verticalVelocity;
		}
		else
		{
			// just landed
			if (!_wasGroundedLastFrame)
			{
				if (Mathf.Abs(_maxFallSpeed) > HardFallThreshold)
				{
					HardLanding = true;
					_animator.SetTrigger("HardLand");
					Debug.Log("Hard landing detected with speed: " + _maxFallSpeed);
					// add play a sound or particle effect here or stuff like that

					// Hard landing roll logic
					_isRolling = true;
					_isSliding = false;
					if (_animator != null)
					{
						_animator.SetBool("IsSliding", false); // reset sliding animation
					}
					_rollSpeed = StrafeSpeed * RollSpeedMultiplier;
					_rollDirection = transform.forward; // <-- Lock roll direction here
				}
				else
				{
					HardLanding = false;
				}
				_maxFallSpeed = 0f;
			}
			else
			{
				HardLanding = false; // reset after first frame on ground
			}
		}
		_wasGroundedLastFrame = Grounded;
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

	private bool HasHeadroom()
	{
		// Calculate where the bottom and top of the standing capsule would be
		Vector3 crouchedSphere = transform.position + Vector3.up * _controller.radius;
		Vector3 headroomSphere = crouchedSphere + Vector3.up * (_standingHeight - 2 * _controller.radius);

		// Check if the space is clear for the standing capsule
		return !Physics.CheckCapsule(
			crouchedSphere,
			headroomSphere,
			_controller.radius - 0.01f,
			GroundLayers,
			QueryTriggerInteraction.Ignore
		);
	}

	private bool HasGroundClearance()
	{
		// Calculate where the bottom and top of the standing capsule would be
		Vector3 crouchedSphere = transform.position + Vector3.up * _controller.radius;
		Vector3 groundClearanceSphere = crouchedSphere - Vector3.up * (_standingHeight - 2 * _controller.radius);

		// Check if the space is clear for the standing capsule
		return !Physics.CheckCapsule(
			crouchedSphere,
			groundClearanceSphere,
			_controller.radius - 0.01f,
			GroundLayers,
			QueryTriggerInteraction.Ignore
		);
	}

	private void Move()
	{

		if (HandleRollingMovement()) return;
		if (HandleHardLandingMovement()) return;
		if (HandleSliding()) return;

		Vector3 inputDirection = GetInputDirection();
		float directionChangeValue = Vector3.Dot(_previousDir, inputDirection);
		float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

		if (HandleReversal(inputDirection, directionChangeValue)) return;

		HandleAirLock(inputDirection);
		HandleMovement(ref inputDirection, currentHorizontalSpeed);
		HandleLanding(inputDirection);
		HandleCrouching(currentHorizontalSpeed);
		UpdateAnimatorAndMove(inputDirection);

	}

	private void JumpAndGravity()
	{
		// Prevent jumping during hard landing animation
		if (IsInState("HardLanding") || _isSliding)
		{
			return;
		}

		if (Grounded)
		{
			// _didJump = false;
			// reset the fall timeout timer
			_fallTimeoutDelta = FallTimeout;

			// stop our velocity dropping infinitely when grounded
			if (_verticalVelocity < 0.0f)
			{
				_verticalVelocity = -2f;
			}

			// Jump
			if (_input.jump && _jumpTimeoutDelta <= 0.0f && !Crouching)
			{
				// the square root of H * -2 * G = how much velocity needed to reach desired height
				_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				_isRunJump = (_speed / SprintSpeed) >= 0.1f; // true if running, false if idle
				_animator.SetTrigger("DidJump");
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
		// Get the CharacterController component (works in edit mode)
		var controller = GetComponent<CharacterController>();
		if (controller != null)
		{
			float capsuleBottom = transform.position.y + controller.center.y - (controller.height / 2f);
			Vector3 spherePosition = new Vector3(transform.position.x, capsuleBottom - GroundedOffset, transform.position.z);
			Gizmos.DrawSphere(spherePosition, GroundedRadius);
		}
	}

	private Vector3 GetInputDirection()
	{
		Vector3 dir = transform.right * _input.move.x + transform.forward * _input.move.y;
		return dir.normalized;
	}

	private bool HandleRollingMovement()
	{
		if (_isRolling && !IsInState("Roll"))
		{
			_isRolling = false;
		}
		if (_isRolling)
		{
			_controller.Move(_rollDirection * (_rollSpeed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
			UpdateAnimator(0f, 0f, 0f);
			return true;
		}
		return false;
	}

	private bool HandleHardLandingMovement()
	{
		if (IsInState("HardLanding"))
		{
			_speed = 0f;
			UpdateAnimator(0f, _rollSpeed / SprintSpeed, _rollSpeed / SprintSpeed);
			_controller.Move(new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
			return true;
		}
		return false;
	}

	private void HandleAirLock(Vector3 inputDirection)
	{
		if (!Grounded && _wasGroundedLastFrame)
		{
			_lockedAirDirection = inputDirection != Vector3.zero ? inputDirection : _previousDir;
			_lockedAirSpeed = _speed;
		}
	}

	private void HandleLanding(Vector3 inputDirection)
	{
		if (Grounded && !_wasGroundedLastFrame)
		{
			_isRunJump = false;
			if (Vector3.Dot(_lockedAirDirection, inputDirection) < 0f)
			{
				_speed = 0f;
			}

			if (Crouching && _currentStance == PlayerStance.CrouchJump)
			{
				SetStance(PlayerStance.Crouching);
			}
		}


	}

	private void UpdateAnimator(float velocityX, float velocityZ, float jumpHorizontalVelocity)
	{
		_animator.SetFloat("VelocityX", velocityX, Damping, Time.deltaTime);
		_animator.SetFloat("VelocityZ", velocityZ, Damping, Time.deltaTime);
		_animator.SetFloat("JumpHorizontalVelocity", jumpHorizontalVelocity, Damping, Time.deltaTime);
	}

	private bool HandleReversal(Vector3 inputDirection, float directionChangeValue)
	{
		// Prevent reversal logic in air
		if (!Grounded)
			return false;

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
				UpdateAnimator(reversalRelativeVelocity.x / SprintSpeed, reversalRelativeVelocity.z / SprintSpeed, _speed / SprintSpeed);

				return true;
			}
		}
		return false;
	}

	private void HandleMovement(ref Vector3 inputDirection, float currentHorizontalSpeed)
	{
		float targetSpeed = Crouching ? CrouchSpeed : _input.sprint ? SprintSpeed : MoveSpeed;
		float inputMagnitude = !IsCurrentDeviceMouse ? _input.move.magnitude : 1f;

		if (Grounded || !_isRunJump)
		{
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
			if (!Grounded)
			{
				targetSpeed *= 0.75f; // Halve speed in air
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
		else
		{
			// AIR LOCK: No control in air
			inputDirection = _lockedAirDirection;
			_speed = _lockedAirSpeed;
		}
	}

	private void HandleCrouching(float currentHorizontalSpeed)
	{
		// Handle crouch input (on ground or in air)
		if (_input.crouch && !Crouching)
		{
			Crouching = true;
			if (Grounded)
			{
				SetStance(PlayerStance.Crouching);
			}
			else
			{
				_isCrouchJumping = true;
				SetStance(PlayerStance.CrouchJump);
			}
			return;
		}

		// Handle stand up when crouch key is released and there is headroom
		if (!_input.crouch && Crouching)
		{
			if (_isCrouchJumping)
			{
				// Released crouch in air: try to stand, else go to crouch stance
				Crouching = false;
				_isCrouchJumping = false;
				if (HasGroundClearance())
				{
					SetStance(PlayerStance.Standing);
					Debug.Log("miaou miaou - THERE IS enough space to stand up");
				}
				else
				{
					SetStance(PlayerStance.Crouching);
					Debug.Log("miaou miaou - not enough space to stand up");
				}
				return;
			}
			else if (HasHeadroom())
			{
				// Released crouch on ground or after landing: stand up
				Crouching = false;
				SetStance(PlayerStance.Standing);
				return;
			}
		}

		// Only trigger sliding if crouch is pressed, not already sliding, moving fast enough, and pressing forward
		bool pressingForward = _input.move.y > 0.5f; // Adjust threshold as needed

		if (Grounded && !_input.jump && _input.crouch && !_isSliding && !_isRolling && currentHorizontalSpeed > (CrouchSpeed + 1.0f + _speedOffset) && pressingForward)
		{
			_isSliding = true;
			_slidingTimer = SlidingDuration;
			_slideSpeed = currentHorizontalSpeed;
		}

		// Safety net: if not crouching, not holding crouch, but still in crouch stance, and there is headroom, stand up
		if (!_input.crouch && !Crouching &&
			(_currentStance == PlayerStance.Crouching || _currentStance == PlayerStance.CrouchJump) &&
			HasHeadroom())
		{
			SetStance(PlayerStance.Standing);
		}

	}

	// private void SetCollider(float newHeight, float newCenterY)
	// {
	// 	_controller.height = newHeight;
	// 	_controller.center = new Vector3(_controller.center.x, newCenterY, _controller.center.z);
	// }

	// // Usage for crouch jump:
	// private void SetCrouchJumpCollider()
	// {
	// 	// Keep the top of the capsule at the same height as standing
	// 	SetCollider(_crouchedHeight, _standingYCenter + _crouchedYCenter);
	// }

	// // Usage for normal crouch:
	// private void SetCrouchCollider()
	// {
	// 	SetCollider(_crouchedHeight, _crouchedYCenter);
	// }

	// // Usage for standing:
	// private void SetStandingCollider()
	// {
	// 	SetCollider(_standingHeight, _standingYCenter);
	// }

	private bool HandleSliding()
	{
		if (_isRolling)
			return false; // Don't slide while rolling

		if (_isSliding)
		{
			_slidingTimer -= Time.deltaTime;

			// This keeps Crouching in sync with the crouch key even while sliding
			if (_input.crouch && !Crouching)
			{
				Crouching = true;
			}
			else if (!_input.crouch && Crouching)
			{
				Crouching = false;
			}

			// --- Update animator crouch state here ---
			_animator.SetBool("IsCrouching", Crouching);

			// gradually reduce slide speed to simulate friction
			_slideSpeed = Mathf.MoveTowards(_slideSpeed, 0f, SlideFriction * Time.deltaTime);

			Vector3 slideDirection = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).normalized;

			// move in the sliding direction at current slide speed
			_controller.Move(slideDirection * (_slideSpeed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

			// update animator here
			Vector3 slideRelativeVelocity = transform.InverseTransformDirection(_controller.velocity);
			UpdateAnimator(slideRelativeVelocity.x / SprintSpeed, slideRelativeVelocity.z / SprintSpeed, _slideSpeed / SprintSpeed);

			if (_slidingTimer <= 0f || _slideSpeed <= 0.1f)
			{
				_isSliding = false;
				_animator.SetBool("IsSliding", _isSliding);

				// careful : Stand up if not crouching when slide ends
				if (!Crouching)
				{
					_controller.height = _standingHeight;
					_controller.center = new Vector3(_controller.center.x, _standingYCenter, _controller.center.z);
				}
			}
			return true; // sliding handled, skip rest of Move()
		}
		return false; // not sliding, continue with Move()
	}

	private void UpdateAnimatorAndMove(Vector3 inputDirection)
	{
		Vector3 relativeVelocity = transform.InverseTransformDirection(_controller.velocity);

		UpdateAnimator(relativeVelocity.x / SprintSpeed, relativeVelocity.z / SprintSpeed, _speed / SprintSpeed);
		_animator.SetBool("IsCrouching", Crouching);
		_animator.SetBool("IsSliding", _isSliding);

		_controller.Move(inputDirection * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

		if (Grounded && inputDirection != Vector3.zero && !_isReversing)
			_previousDir = inputDirection;
	}
}